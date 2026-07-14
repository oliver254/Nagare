# ADR-0008 — Synchronisation du coordinateur : boucle séquentielle (mailbox)

Statut : accepté — 2026-07-06

## Contexte

`StreamSessionCoordinator` mute l'agrégat `StreamSession` depuis **trois threads
différents** :

| Origine | Chemin |
|---|---|
| Appelant (UI / handler CQRS) | `StartAsync`, `StopAsync` |
| Thread lecteur de `stderr` ffmpeg | `OnStatsReceived`, `OnOutputLine` |
| Callback de sortie du process | `OnExited` → `HandleExitAsync` (fire-and-forget) |

La synchronisation actuelle repose sur un `SemaphoreSlim _gate`. Un audit a
révélé **trois défauts**, plus deux fuites de cycle de vie. Tous sont
**pré-existants** ; aucune UI ne pilote encore le coordinateur, donc l'exposition
utilisateur est nulle — mais ils sont bloquants avant de brancher le dashboard.

### (a) Le verrou est tenu pendant tout le backoff

`HandleExitAsync` prend le `_gate` et le garde jusqu'à son `finally`, en
traversant `await Task.Delay(delay, cts.Token)` dans `ScheduleReconnectAsync`.
Conséquences :

- `StopAsync` attend le `_gate` **avant** de pouvoir appeler `_sessionCts.Cancel()` :
  l'arrêt utilisateur est **bloqué jusqu'à 32 s** (politique par défaut ; 60 s si
  `MaxDelay` est configuré plus haut). **Viole la SPEC §5** (« annulation via
  `CancellationToken` »).
- Pire : à l'expiration du délai, ffmpeg est **relancé** (le token n'est toujours
  pas annulé) et **publie brièvement sur Twitch** avant que `StopAsync` n'obtienne
  enfin le verrou et le tue. Inacceptable pour un outil de diffusion.
- Le `catch (OperationCanceledException)` de `ScheduleReconnectAsync` est donc
  **du code mort** : le seul `Cancel()` est lui-même derrière le verrou.

### (b) L'échec de démarrage d'une relance est avalé

Le `catch (Exception ex)` de `HandleExitAsync` logge et absorbe la
`Win32Exception` que lève `process.Start()` si le binaire ffmpeg est absent,
déplacé ou verrouillé par un antivirus au moment de la relance. Résultat : plus
de runner, plus d'événement `Exited`, plus de timer → **session zombie en
`Reconnecting` à vie**. C'est exactement la classe de bug corrigée en `41856d9`,
ressurgie par un autre chemin.

`StreamSession.MarkFailed` accepte pourtant déjà `Reconnecting` — c'est testé, et
**jamais appelé en production**.

### (c) Les stats mutent l'agrégat hors verrou

`OnStatsReceived` appelle `MarkRunning()` **sans prendre le `_gate`**, depuis le
thread lecteur. Les lecteurs `stderr` vidant leur buffer de façon asynchrone, une
ligne de stats **du process déjà mort** peut arriver après `Exited` et déclencher
une **fausse `SessionRecovered`**, remettant `ReconnectAttempts` à **0**. Le budget
de tentatives n'est alors jamais épuisé : **la session n'atteint jamais `Failed`**,
ce qui annule le correctif précédent. Et si le statut change entre le test et
l'appel, la `DomainException` remonte **sans `catch`** sur le thread lecteur →
**crash du process**.

### Fuites de cycle de vie

- `StopAsync` n'appelle que `CleanupRunnerAsync()` : `_sessionCts` n'est **jamais
  `Dispose()`** et se fait écraser au `StartAsync` suivant → une fuite de CTS par session.
- `_logs` n'est **pas vidé** au `StartAsync` : une nouvelle session affiche les
  lignes de la précédente.

---

## Décision

**Une boucle de traitement séquentielle (mailbox), sans aucun verrou.**

Tous les stimuli deviennent des **messages** postés dans un
`System.Threading.Channels.Channel<CoordinatorMessage>` (non borné, *single
reader*). **Une seule boucle de fond** les consomme, et elle est le **seul
écrivain** de `_session`, `_runner`, `_command`, `_lastStats`.

```
StartRequested(profileId, channelId, path, TaskCompletionSource<SessionId>)
StopRequested(TaskCompletionSource)
StatsReceived(epoch, stats)
ProcessExited(epoch, exitCode)
ReconnectDue(epoch)
```

`StartAsync`/`StopAsync` postent leur message avec un `TaskCompletionSource` et
l'attendent : les handlers CQRS gardent une API asynchrone qui propage
correctement le résultat ou l'exception.

Plus de `SemaphoreSlim`, plus de section critique, **plus aucune course possible
par construction** : l'agrégat n'est plus touché que depuis un unique thread
logique.

### ⚠️ La boucle ne doit JAMAIS attendre le backoff

C'est le piège qui annulerait tout le bénéfice. Si la boucle faisait
`await Task.Delay(backoff)`, elle ne pourrait pas traiter un `StopRequested`
pendant l'attente — **on retomberait exactement sur le défaut (a)**, mailbox ou pas.

Le délai est donc **planifié à l'extérieur** de la boucle et **repostera** un
message :

```csharp
// dans le traitement de ProcessExited, après session.BeginReconnect(reason)
var delay = session.Policy.DelayFor(session.ReconnectAttempts);
_ = Task.Delay(delay, _sessionCts.Token)
        .ContinueWith(_ => _writer.TryWrite(new ReconnectDue(_epoch)),
                      TaskContinuationOptions.OnlyOnRanToCompletion);
```

La boucle retourne **immédiatement** lire le message suivant. Un `Stop` est donc
traité **sans délai**, quelle que soit la fenêtre de backoff en cours.

### Épochs : la fin des messages périmés

Chaque runner démarré reçoit un numéro de génération croissant (`_epoch`). Les
événements du runner (`StatsReceived`, `ProcessExited`) et le `ReconnectDue`
**portent l'époque** de leur émetteur. **La boucle ignore tout message dont
l'époque diffère de l'époque courante.**

C'est ce qui tue le défaut (c) de façon **déterministe**, sans reposer sur une
hypothèse de timing : une ligne de stats d'un process mort porte forcément une
époque périmée. On ne « court-circuite » plus une course, on la rend
**impossible à exprimer**.

### Traitement de `StopRequested`

1. Annuler `_sessionCts` → le `Task.Delay` du backoff est annulé, **le
   `ReconnectDue` ne sera jamais posté**.
2. Incrémenter `_epoch` → tout message déjà en vol est périmé et sera ignoré.
3. Arrêter/tuer le runner, `session.Stop()`, drainer les événements, nettoyer.

**Aucun ffmpeg ne peut être relancé après un `Stop`** : **trois** barrières
indépendantes l'en empêchent.

> **Mise à jour (implémentation)** : les deux barrières ci-dessus laissaient une
> fenêtre FIFO — si le délai expirait *juste avant* le `Stop`, le `ReconnectDue`
> était déjà dans la file et traité en premier (ffmpeg relancé puis tué aussitôt).
> Fermée par deux compléments :
> - `StopAsync` annule `_sessionCts` **avant même de poster** `StopRequested`
>   (depuis le thread appelant) ;
> - le traitement de `ReconnectDue` **vérifie `IsCancellationRequested`** et
>   abandonne la relance si l'annulation est demandée.
>
> Nuance assumée sur « la boucle est le seul écrivain » : cela reste vrai pour
> **l'agrégat et l'état de session** (`_session`, `_runner`, `_command`,
> `_lastStats`) ; le `_sessionCts`, lui, est annulé depuis le thread appelant —
> c'est précisément le rôle d'un `CancellationTokenSource`, conçu et thread-safe
> pour signaler entre threads. Aucun état du domaine n'est muté hors boucle.
> Ce n'est **pas** le « flag partagé » écarté plus bas : un flag maison aurait dû
> inventer sa propre sémantique de visibilité ; le CTS l'apporte nativement.

### Échec de démarrage d'une relance (défaut b)

Dans le traitement de `ReconnectDue`, si `StartRunnerAsync` lève (typiquement
`Win32Exception` : binaire introuvable), la boucle appelle
**`session.MarkFailed(reason)`** puis nettoie. Plus de zombie.

### Politique d'exception

Aucune exception n'est plus **avalée en silence**. Le traitement de chaque message
est encapsulé :

- `DomainException` sur une transition ⇒ **bug du coordinateur** : log `Error` +
  `MarkFailed` + nettoyage. On échoue **bruyamment**, on ne continue pas.
- Exception d'infrastructure (démarrage du process) ⇒ `MarkFailed` + nettoyage.
- **La boucle elle-même ne meurt jamais** : elle attrape, traite, et continue à lire.

### Corrections de cycle de vie incluses

- `_sessionCts` est `Dispose()` à la fin de chaque session (et non écrasé).
- `_logs` est **vidé au démarrage** d'une nouvelle session.
- `HealthOf` prend en compte les **drops croissants** en plus de `Speed < 1.0`
  (la SPEC §6 et `HealthIndicator` l'annonçaient ; le code ne le faisait pas).

---

## Conséquences

- **Les trois défauts disparaissent par construction**, pas par correction ponctuelle.
  C'est l'argument décisif : un futur contributeur ne peut plus « oublier le verrou »,
  puisqu'il n'y en a plus.
- L'arrêt utilisateur est **immédiat**, même en plein backoff (SPEC §5 respectée).
- **Coût assumé** : une indirection de plus (messages + `TaskCompletionSource`), et une
  boucle dont il faut soigner la fermeture (`Channel.Writer.Complete()` + attente de la
  boucle dans `DisposeAsync`).
- Le coordinateur devient **enfin testable de façon déterministe** : un
  `FakeFfmpegProcessRunner` pilote `Exited`/`StatsReceived` à la demande, et l'ordre de
  traitement est garanti par la boucle. C'est indispensable — **le coordinateur n'a
  aucun test aujourd'hui**, alors que c'est précisément la couture où vivait le bug
  corrigé en `41856d9`.
- La violation du SRP relevée par l'audit (le coordinateur cumule machine à états,
  buffer de logs, cycle de vie du process et ordonnancement) **n'est pas traitée ici** :
  la sortir du même lot brouillerait le correctif. Le ring buffer de logs reste
  l'extraction la plus évidente pour plus tard.

## Alternative écartée

**Corriger le verrou** (ne jamais tenir le `_gate` à travers un `await Task.Delay` ;
relâcher avant le délai, le reprendre après en re-vérifiant l'état ; passer
`OnStatsReceived` sous le même verrou).

Écartée pour trois raisons :

1. **Mettre `OnStatsReceived` sous le verrou bloque le thread lecteur de `stderr`.**
   Un lecteur bloqué, c'est le pipe de ffmpeg qui se remplit, donc **ffmpeg qui se
   fige**. On échangerait une course contre un blocage — un mauvais marché. Le rendre
   `async` fire-and-forget réintroduirait aussitôt les problèmes d'ordre.
2. Le motif « relâcher, attendre, reprendre, re-vérifier l'état » est un **piège TOCTOU
   classique** : l'état a changé pendant le délai, et chaque re-vérification oubliée est
   un bug.
3. **Chaque futur chemin devra penser au verrou.** La discipline ne se teste pas ; la
   structure, si.

## Tests exigés (aucun n'existe aujourd'hui)

Avec un `FakeFfmpegProcessRunner` pilotable :

- une chute de flux déclenche une reconnexion, et les relances échouées **incrémentent**
  les tentatives avec un **délai croissant** ;
- à l'épuisement des tentatives → `Failed` ;
- un **`Stop` en plein backoff est traité immédiatement** et **aucun ffmpeg n'est
  relancé ensuite** (le test qui protège la SPEC §5) ;
- une **stat périmée** émise après `Exited` (époque obsolète) est **ignorée** : elle ne
  déclenche pas de fausse `SessionRecovered` et ne remet **pas** le compteur à zéro ;
- un `StartRunnerAsync` qui **lève** au moment d'une relance fait basculer la session en
  `Failed` (pas de zombie) ;
- le buffer de logs est **vide** au démarrage d'une nouvelle session.
