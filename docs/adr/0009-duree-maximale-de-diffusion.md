# ADR-0009 — Durée maximale de diffusion et arrêt programmé

Statut : accepté — 2026-07-23

## Contexte

Lot 1 de la feature « planification de stream » (`docs/product/stream-scheduling.md`,
US-0) : au lancement manuel, l'utilisateur peut fixer une **durée maximale**. À
l'échéance, la diffusion s'arrête proprement et l'UI dit que l'arrêt est
**automatique (durée atteinte)**, par opposition à un arrêt manuel. Sans durée,
rien ne change.

Hors périmètre absolu de cet ADR : planification différée, persistance,
`ScheduledStream`, statuts de planification (Lot 2). Le Lot 1 doit néanmoins
faire émerger la **mécanique d'arrêt programmé** que le Lot 2 réutilisera — sans
la généraliser d'avance.

Trois contraintes cadrent la décision :

1. **`Nagare.Domain` n'a aucune dépendance** — pas même `Microsoft.Extensions.*`,
   donc pas de `TimeProvider`. L'agrégat `StreamSession` n'a **aucune horloge**
   pour décider. Il lit bien `DateTimeOffset.UtcNow`, mais uniquement pour
   **horodater** ses événements : horodater n'est pas décider.
2. **ADR-0008 est non négociable** : le coordinateur est une boucle séquentielle
   (mailbox) sans verrou, seule écrivain de l'agrégat, et **la boucle n'attend
   jamais** — tout délai est planifié à l'extérieur et repose un message.
3. La machine à états (ARCHITECTURE §2.4) couvre déjà `Reconnecting → Stopped` :
   l'arbitrage D du cadrage (« l'arrêt programmé gagne sur le backoff ») ne
   demande **aucune transition nouvelle**.

Trois questions étaient ouvertes : où vit la durée, comment se distingue la
raison d'arrêt, et comment le déclencheur temporel cohabite avec les trois
barrières de l'ADR-0008.

## Décision

### 1. La durée est un **invariant du domaine**, l'échéance un **état d'application**

L'agrégat porte l'**intention** ; le coordinateur porte l'**instant**.

```csharp
// Nagare.Domain.Sessions.StreamSession
public static readonly TimeSpan MaxAllowedDuration = TimeSpan.FromHours(24);
public TimeSpan? MaxDuration { get; }        // null = diffusion sans limite

public static StreamSession Launch(ProfileId profileId, ChannelId channelId,
    string inputFilePath, ReconnectPolicy policy, TimeSpan? maxDuration = null);
```

Invariants (`DomainException` sinon, comme `EncodingSettings` E1–E8 et
`ReconnectPolicy`) :

| # | Invariant | Raison |
|---|---|---|
| S1 | `maxDuration` présente ⇒ `> TimeSpan.Zero` | une fenêtre nulle ou négative n'a pas de sens (US-0 : saisie refusée) |
| S2 | `maxDuration` présente ⇒ `<= MaxAllowedDuration` (24 h) | garde-fou anti-faute de frappe, pas une limite produit — **confirmé par le propriétaire (aucun usage réel > 24 h) ; se lève en une constante si besoin** |

`MaxAllowedDuration` est **exposée par le domaine** : l'UI borne son champ de
saisie avec cette constante, exactement comme les ComboBox lisent
`EncodingSettings.PresetsFor` — la borne affichée et la borne appliquée ne
peuvent pas diverger. **Aucune validation métier n'est réécrite côté UI**
(ARCHITECTURE §7) : ce qui passe malgré tout lève une `DomainException`.

Cette `DomainException` est **affichée par le ViewModel, traduite en français**
pour le seul invariant de durée — le seul qu'un écran par ailleurs valide puisse
encore déclencher (la saisie borne le reste). C'est une **traduction, pas une
seconde règle** : le seuil lu côté UI est celui du domaine
(`StreamSession.MaxAllowedDuration`), donc les deux ne peuvent pas diverger, et
toute autre `DomainException` conserve son texte. Même principe que la traduction
des `StartBlockReason` du préflight en phrases françaises.

L'agrégat ne **calcule ni n'évalue** l'échéance. L'instant de fin
(`PlannedEndsAt = démarrage + MaxDuration`) est calculé et détenu par le
`StreamSessionCoordinator`, qui a déjà `TimeProvider` injecté — donc testable au
`FakeTimeProvider`, comme le backoff.

Symétrie assumée avec `ReconnectPolicy` : dans les deux cas le domaine **valide
et détient** une borne choisie par l'utilisateur, et le coordinateur
l'**applique** contre une horloge.

### 2. La raison d'arrêt est un paramètre de la transition, portée par l'événement

```csharp
public enum SessionStopReason { Manual, DurationElapsed }

public void Stop(SessionStopReason reason);        // pas de valeur par défaut
public SessionStopReason? StopReason { get; }      // null tant que non arrêtée

public sealed record SessionStopped(SessionId Id, SessionStopReason Reason,
    DateTimeOffset OccurredAt) : IDomainEvent;
```

Invariant supplémentaire, et c'est lui qui empêche `MaxDuration` d'être une
donnée morte :

| # | Invariant | Raison |
|---|---|---|
| S3 | `Stop(DurationElapsed)` exige `MaxDuration is not null` | une session sans limite ne peut pas être arrêtée « pour durée atteinte » — un timer périmé qui atteindrait cette session est un bug, et il échoue bruyamment |

`MarkFailed` ne renseigne **pas** `StopReason` : un échec n'est pas un arrêt.
`StopReason` reste `null` en `Failed`, et la distinction reste lisible.

### 3. Le déclencheur : un message de plus dans la mailbox, taggé par le `SessionId`

Strictement le patron `ScheduleReconnect` de l'ADR-0008 : armé **hors** de la
boucle, lié au `_sessionCts`, il ne fait que **reposter un message**.

```csharp
private sealed record DurationElapsed(SessionId SessionId) : CoordinatorMessage;
```

**Le message porte le `SessionId`, PAS l'epoch — et c'est le point le plus
important de cet ADR.** L'epoch est une génération de **runner** : il est
incrémenté à chaque sortie de ffmpeg (`HandleExitedAsync`), à chaque stop et à
chaque fin de session. Une échéance armée au lancement et taggée par l'epoch
serait déclarée périmée dès la **première reconnexion** : l'arrêt automatique ne
se produirait jamais — précisément dans le cas que l'arbitrage D est censé
couvrir. Le `SessionId` est stable sur toute la vie de la session ; c'est le seul
discriminant correct ici.

**Armement : au lancement** (`HandleStartAsync`), après l'affectation de
`_sessionCts` et **avant** le premier `DrainEvents`, pour que le tout premier
snapshot publié porte déjà `PlannedEndsAt`. Pas au passage `Running` : US-0
promet « maintenant + N », le temps de démarrage de ffmpeg fait partie de la
fenêtre, et un armement à chaque `MarkRunning` ferait **glisser** la fenêtre à
chaque reconnexion.

**Traitement**, dans l'ordre :

1. Message d'une **autre session** ou session déjà **terminale** → ignoré.
   (C'est la barrière « epoch » de l'ADR-0008, transposée à l'échelle session.)
2. `_sessionCts.IsCancellationRequested` → **abandon** : un stop manuel est déjà
   en vol, on lui laisse la main et la raison reste celle de l'utilisateur.
   C'est la **barrière 3** de l'ADR-0008, mot pour mot : le délai qui expire
   juste avant le `StopAsync` place son message **devant** le stop dans la FIFO.
3. Sinon : annulation des délais en cours (barrière 1 — c'est ce qui **abandonne
   le backoff**), incrément d'epoch (barrière 2), arrêt du runner s'il existe,
   `session.Stop(DurationElapsed)`, drain des événements, fin de session.

Les étapes 3 sont **partagées avec l'arrêt manuel** (une méthode privée
`StopSessionAsync(session, reason, ct)`) : les deux chemins d'arrêt appliquent
les mêmes barrières, par construction et non par discipline.

**La boucle n'attend jamais** : `Task.Delay(duration, _time, cts.Token)` +
`ContinueWith` qui écrit le message, comme `ScheduleReconnect`.

**Arbitrage D, applicable dès le Lot 1** : si l'échéance tombe en
`Reconnecting`, il n'y a pas de runner (déjà nettoyé) mais un `Task.Delay` de
backoff en vol. L'étape 3.1 l'annule, l'étape 3.2 périme le `ReconnectDue`
éventuellement déjà en file, et `Reconnecting → Stopped` — transition **déjà
autorisée** — clôt la session. Aucun ffmpeg n'est relancé après l'échéance.

`EndSessionAsync` **annule** désormais `_sessionCts` avant de le `Dispose()` :
sans cela, une session avortée pouvait laisser un timer de plusieurs heures en
vol. Le `SessionId` le rendait déjà inoffensif ; il reste une fuite de ressource
dans une application qui peut tourner des jours.

### 4. Contrat d'entrée : `TimeSpan?`, explicite à la frontière applicative

```csharp
public sealed record StartStreamCommand(ProfileId ProfileId, ChannelId ChannelId,
    string InputFilePath, TimeSpan? MaxDuration) : ICommand<SessionId>;

Task<SessionId> StartAsync(ProfileId profileId, ChannelId channelId,
    string inputFilePath, TimeSpan? maxDuration, CancellationToken ct);
```

`TimeSpan?` et non un nombre d'heures : la saisie en heures décimales (0,5 = 30
min) est une affaire d'UI ; le modèle manipule une durée. **Aucune valeur par
défaut** à la frontière applicative — un `= null` y ferait taire silencieusement
une durée saisie le jour où un appelant l'oublie. Le domaine, lui, garde son
défaut (`maxDuration = null` = sans limite) : c'est sa règle métier, pas une
commodité d'appel.

Validation **dans le domaine**, pas dans `GetStartPreflightQuery` : la borne est
un invariant de `StreamSession`, et le preflight décide de faits externes
(toolchain, fichier, session déjà active) qui ne sont pas des invariants. Deux
définitions de la même règle finissent toujours par diverger.

### 5. Projection UI : deux champs, pas un de plus

```csharp
public sealed record SessionSnapshot(SessionId Id, SessionStatus Status,
    FfmpegStats? Stats, HealthIndicator Health, int ReconnectAttempts,
    string? LastError,
    DateTimeOffset? PlannedEndsAt,        // null = diffusion sans limite
    SessionStopReason? StopReason);       // null tant que la session n'est pas arrêtée
```

- `PlannedEndsAt` : US-0 exige qu'« une heure de fin soit affichée ». Détenu par
  le coordinateur, il **survit à la réhydratation** du dashboard (naviguer et
  revenir pendant que ffmpeg tourne) — ce qu'un chronomètre local ne ferait pas.
- `StopReason` : c'est lui qui permet le « arrêt automatique (durée atteinte) »
  du bilan de session.
- **Pas de `Remaining`** : un temps restant est une valeur qui s'égrène. La
  publier obligerait le coordinateur à pousser un snapshot par seconde, contre
  le throttle qui existe justement pour l'éviter (ARCHITECTURE §4.4), et elle
  serait périmée entre deux pushes. La vue le dérive de `PlannedEndsAt` avec son
  propre timer — c'est un besoin de vue.
- **Pas de `MaxDuration`** dans le snapshot : `PlannedEndsAt is not null` répond
  déjà à « cette diffusion a-t-elle une limite ? ».

## Conséquences

- L'arrêt automatique **réutilise le chemin d'arrêt existant** : mêmes barrières,
  même nettoyage, même événement. Le Lot 2 n'aura qu'à décider *quand* armer et
  *quoi* faire de la planification — la mécanique d'arrêt est acquise, sans avoir
  été généralisée d'avance.
- **Aucune transition nouvelle** dans la machine à états : le diagramme est
  inchangé, seules les étiquettes se précisent (« stop utilisateur » devient
  « arrêt manuel ou durée atteinte »).
- `SessionStopped` et `SessionSnapshot` changent de forme : tous leurs sites de
  construction sont à mettre à jour (coordinateur, tests, helper de test du
  dashboard). Coût mécanique, une seule fois.
- `Stop()` devient `Stop(SessionStopReason)` sans valeur par défaut : le
  compilateur **exige** que chaque site d'appel dise pourquoi il arrête. C'est
  voulu — un défaut laisserait un futur arrêt automatique s'étiqueter « manuel ».
- **Une session non bornée est strictement inchangée** : aucun timer n'est armé,
  aucun message n'est posté, le chemin est le chemin d'aujourd'hui.
- **Mise en veille de la machine** : le décompte repose sur un timer .NET. Si la
  machine dort pendant la fenêtre, l'échéance peut se déclencher **en retard**,
  peu après le réveil. Risque déjà documenté par le cadrage (§8) ; aucune
  mitigation dans ce lot.
- **Application fermée avant l'échéance** : la session meurt avec l'application
  (`IHostedService.StopAsync` tue ffmpeg — SPEC §5). Aucune reprise : les
  sessions ne sont pas persistées (inchangé depuis l'itération 1).
- Course bénigne assumée : si l'échéance et le clic Stop tombent dans la même
  fenêtre FIFO, **l'arrêt manuel gagne** l'étiquette (barrière 2 du traitement).
  Dans tous les cas la diffusion s'arrête — seul le libellé est en jeu.

## Alternatives écartées

- **Durée purement applicative** (coordinateur seul, agrégat inchangé). Écartée :
  `Stop(DurationElapsed)` serait alors une raison qu'**aucun invariant ne
  soutient** — une session sans limite pourrait être arrêtée « pour durée
  atteinte » sans que rien ne le relève. Le projet a déjà tranché ce type
  d'arbitrage en ouvrant `Running → Failed` plutôt que d'émettre un
  `ReconnectStarted` mensonger : les événements sont la piste d'audit de la
  session, et « pourquoi cette diffusion s'est-elle arrêtée ? » s'y répond.
- **`PlannedEndsAt` (instant absolu) dans l'agrégat.** Écartée : l'agrégat
  devrait soit lire l'horloge ambiante pour *décider* — indéterministe et
  intestable, alors que `Domain` n'a pas de `TimeProvider` et n'en aura pas —
  soit recevoir l'instant de lancement en paramètre, ce qui ferait entrer un fait
  d'horloge murale dans un agrégat qui ne s'en sert aujourd'hui que pour
  horodater. L'échéance s'évalue contre une horloge : elle appartient au
  coordinateur.
- **Un événement dédié `SessionDurationElapsed`** (option B du cadrage §7).
  Écartée : la session ne se termine qu'une fois, et elle se termine `Stopped`.
  Deux événements pour une transition — ou un événement qui n'en est pas une —
  casseraient la règle « une transition = un événement » sur laquelle repose la
  piste d'audit. La raison est un **attribut** de l'arrêt, pas un autre arrêt.
- **Raison uniquement dans la projection** (option C du cadrage §7). Écartée : le
  coordinateur devrait mémoriser un « pourquoi » à côté de l'agrégat, alors que
  l'ADR-0008 fait de l'agrégat l'état de référence. Toute lecture de l'agrégat
  (persistance du Lot 2 comprise) devrait réinventer l'information.
- **Tagger `DurationElapsed` par l'epoch**, comme `ReconnectDue`. Écartée pour la
  raison exposée en §3 : l'epoch change à chaque sortie de ffmpeg, l'échéance
  serait périmée dès la première reconnexion et l'arrêt automatique ne
  surviendrait jamais.
- **Timer dédié / hosted service « scheduler » séparé.** Écarté : un second fil
  touchant la session détruirait la propriété « un seul écrivain » de
  l'ADR-0008. Même forme que le backoff ⇒ mêmes garanties.
- **Value object `SessionDuration`.** Écarté : un unique `TimeSpan` avec deux
  bornes ne mérite pas un type. `ReconnectPolicy` le mérite (quatre champs et un
  calcul) ; ici ce serait de l'emballage.
- **`StartBlockReason.InvalidDuration` dans le preflight.** Écarté : cela
  dupliquerait un invariant du domaine dans la couche qui décide des faits
  externes. La borne exposée par le domaine (`MaxAllowedDuration`) donne à l'UI
  de quoi empêcher la saisie fautive sans réécrire la règle.

## Tests exigés

**Domaine** (`StreamSessionTests`) : durée absente ⇒ session sans limite ;
durée valide conservée ; durée ≤ 0 et durée > 24 h ⇒ `DomainException` (24 h
exactement acceptée) ; `Stop(Manual)` et `Stop(DurationElapsed)` renseignent
`StopReason` **et** `SessionStopped.Reason` ; `Stop(DurationElapsed)` refusé sur
une session sans `MaxDuration` ; `Stop(DurationElapsed)` accepté depuis
`Reconnecting` ; `MarkFailed` laisse `StopReason` à `null`.

**Coordinateur** (`StreamSessionCoordinatorTests`, `FakeTimeProvider`) : sans
durée, aucune horloge n'arrête la session (avance de 48 h) ; `PlannedEndsAt`
publié dès le premier snapshot ; l'échéance arrête la session avec
`DurationElapsed` et tue le runner ; rien ne se produit juste **avant**
l'échéance ; **échéance en `Reconnecting` ⇒ backoff abandonné, aucun ffmpeg
relancé** (arbitrage D) ; échéance après un stop manuel ⇒ ignorée, la raison
reste `Manual` ; échéance sur une session déjà `Failed` ⇒ ignorée ; échéance
d'une session **précédente** ⇒ n'arrête pas la session courante ; stop manuel
avant l'échéance ⇒ `Manual` ; durée ≤ 0 ⇒ `DomainException` et **aucun ffmpeg
lancé** ; échéance déjà en file quand le stop est posté ⇒ l'arrêt manuel gagne
(preuve par le message de log de la barrière, comme
`StopAbortedReconnectLogMessage`).
