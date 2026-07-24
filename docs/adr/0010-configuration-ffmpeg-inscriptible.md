# ADR-0010 — Configuration ffmpeg inscriptible par l'utilisateur, sans User Secrets

Statut : accepté — 2026-07-24

## Contexte

Les chemins `ffmpeg`/`ffprobe` (`FfmpegOptions`, section `Nagare:Ffmpeg`) n'ont que
deux sources : `appsettings.json`, livré à côté de l'exe, et les **User Secrets**
(`AddUserSecrets<App>()` dans `App.xaml.cs`). Les User Secrets exigent le SDK .NET
et le dossier du projet : ils sont **inatteignables depuis un exe distribué**, et
hors de portée d'un utilisateur qui n'est pas contributeur.

Conséquence observable : quand ffmpeg n'est pas sur le `PATH`, le tableau de bord
affiche « ffmpeg est introuvable. Renseignez son chemin dans la configuration de
l'application » — en désignant une configuration qu'**aucun écran n'atteint**. Le
préflight bloque le démarrage et l'application est sans issue.

L'application sait pourtant déjà écrire sous `%APPDATA%\Nagare` (ADR-0004) :
`profiles.json`, `targets.json`, keyring DPAPI. Il n'y manquait que sa propre
configuration.

## Décision

### 1. `settings.json`, écrit par l'application

Nouveau fichier `%APPDATA%\Nagare\settings.json`, sous la racine de
`NagareStorageOptions` (`ResolvedRoot`) — même dossier que `profiles.json` et
`targets.json` — écrit par l'application depuis un écran **Paramètres**
(détecter / parcourir / tester / enregistrer).

Écriture **atomique** (fichier temporaire + `File.Replace`), exactement comme
`JsonFileStore` (ADR-0004) : une écriture interrompue ne laisse pas un fichier
tronqué qui empêcherait le démarrage suivant.

Le fichier ne contient que **des chemins**. Aucune clé n'y entre jamais : les clés
de stream restent chiffrées par DPAPI (ADR-0005). La frontière « configuration en
clair » / « secrets chiffrés » est inchangée — elle change seulement de fichier.

### 2. Suppression complète des User Secrets

`AddUserSecrets<App>()`, `<UserSecretsId>` et le `PackageReference`
`Microsoft.Extensions.Configuration.UserSecrets` disparaissent. Deux mécanismes de
configuration locale, dont un réservé à qui possède le SDK **et** les sources,
c'est un de trop — et c'est justement celui qui ne marche pas pour l'utilisateur.

### 3. Précédence : l'utilisateur prime sur le défaut livré

`settings.json` (`%APPDATA%`, écrit par l'utilisateur) l'emporte sur
`appsettings.json` (défaut livré à côté de l'exe). Remplacer l'exe et son
`appsettings.json` lors d'une mise à jour n'écrase donc pas le choix de
l'utilisateur.

### 4. La vérité runtime est un port, pas `IConfiguration`

C'est le point central de cet ADR. **Les chemins en vigueur ne transitent pas par
`IConfiguration` / `IOptions<FfmpegOptions>` au runtime.** Un singleton les porte :

```csharp
// Nagare.Application.Abstractions
public sealed record FfmpegPathSettings(string ExecutablePath, string FfprobePath)
{
    // Chaîne vide = valeur légitime : « résoudre le binaire depuis le PATH ».
    public string ResolvedFfmpeg  { get; }
    public string ResolvedFfprobe { get; }
}

public interface IFfmpegPaths
{
    FfmpegPathSettings Current { get; }
    void Apply(FfmpegPathSettings settings);
}
```

- `FfmpegPathProvider` (Infrastructure, singleton) l'implémente : **chargé au
  démarrage** depuis `settings.json`, avec repli sur `FfmpegOptions` (donc
  `appsettings.json`, donc `"ffmpeg"`/`"ffprobe"` résolus depuis le `PATH`) ;
  **muté synchronement en mémoire** à l'enregistrement, avant que l'appel
  d'enregistrement ne rende la main.
- Les trois adaptateurs qui lancent un binaire — `FfmpegEnvironmentProbe`,
  `FfprobeService`, `FfmpegProcessRunner` — consomment `IFfmpegPaths` au lieu de
  `IOptions<FfmpegOptions>`.
- Lecture et écriture partagent **une seule interface**, parce qu'elles portent un
  seul fait : « quels chemins sont en vigueur ». La discipline ISP tient par l'usage
  plutôt que par le type — aucun des trois adaptateurs n'appelle `Apply` ; seuls le
  handler d'enregistrement et l'amorçage au démarrage le font. Scinder en
  `IFfmpegPaths` / `IFfmpegPathsWriter` reste possible le jour où un adaptateur
  serait tenté d'écrire ; aujourd'hui ce serait une interface de plus pour un
  interdit qu'aucun appelant ne cherche à franchir.
- La persistance (`settings.json`) est, elle, un port **bien distinct**
  (`IFfmpegSettingsStore`) : un consommateur de chemins n'a jamais à savoir qu'un
  fichier existe.
- Ce port expose **assumément** un chemin de fichier (`SettingsFilePath`) : l'écran
  Paramètres doit pouvoir montrer à l'utilisateur **où** son choix est écrit, et un
  emplacement affichable est une information de l'interface, pas un détail de
  sérialisation. Seul l'écran le lit ; les consommateurs de chemins ffmpeg, eux,
  ignorent toujours qu'un fichier existe.

`FfmpegOptions` survit mais **change de rôle** : il ne porte plus que le défaut
livré, lu une fois au démarrage. Il n'est plus la vérité runtime et plus aucun
adaptateur ne l'injecte.

## Conséquences

- **L'application devient configurable par elle-même.** Le message d'erreur du
  préflight désigne enfin quelque chose d'atteignable : l'écran Paramètres.
- **Aucune fenêtre de chemins périmés.** Après un « Enregistrer », tout
  consommateur — sonde d'environnement, préflight, lancement de ffmpeg — lit les
  nouveaux chemins **dès le retour de l'appel**. Un « Tester » enchaîné à un
  « Enregistrer » ne peut pas rendre le verdict de l'ancienne configuration.
- **Corollaire pendant une diffusion active** : un enregistrement s'applique
  immédiatement, y compris session en cours. Le process ffmpeg déjà lancé n'est pas
  touché (les chemins sont lus au démarrage du process), mais un runner est créé
  **par lancement** — une reconnexion automatique relancerait donc un **autre
  binaire** au milieu de la même session. C'est le prix assumé de l'application
  immédiate ; l'alternative (différer jusqu'au prochain démarrage de session)
  réintroduirait exactement la fenêtre de chemins périmés que cet ADR supprime.
- **Testable sans disque** : `IFfmpegPaths` se substitue par un stub dans les tests
  des trois adaptateurs, là où `IOptions<FfmpegOptions>` imposait de passer par la
  configuration.
- Un contributeur ne configure plus ffmpeg **en ligne de commande** mais dans
  l'application. La configuration machine-locale sort du périmètre du SDK — et
  reste, comme avant, hors du dépôt.
- **Une source de configuration de plus** à connaître pour lire le code : le chemin
  effectif ne se déduit plus d'`appsettings.json` seul. C'est le prix de la
  précédence, et le port le rend explicite — un seul type répond à « quel
  ffmpeg ? ».
- `settings.json` est un fichier **en clair** : il ne doit jamais accueillir autre
  chose que des chemins. Toute donnée sensible reste soumise à l'ADR-0005.

## Alternatives écartées

- **`settings.json` en source de configuration avec `reloadOnChange: true`, et
  consommateurs en `IOptionsMonitor<FfmpegOptions>`.** Plus idiomatique .NET, et
  c'était la première intention. Écartée : le rechargement passe par un *file
  watcher* **asynchrone**. Un « Tester » enchaîné à un « Enregistrer » peut lire les
  **anciens** chemins — une course invisible en test, qui se manifeste chez
  l'utilisateur comme un bug intermittent (« il ne trouve toujours pas ffmpeg…
  ah, maintenant si »). Le provider synchrone l'élimine **par construction**, pas
  par temporisation.
- **Écrire dans l'`appsettings.json` à côté de l'exe.** Écartée : le dossier
  d'installation n'est pas garanti inscriptible, et une mise à jour de
  l'application écraserait la configuration de l'utilisateur.
- **Variable d'environnement dédiée.** Écartée : configurer une application
  graphique depuis les variables d'environnement de Windows n'est pas un parcours
  utilisateur, et le problème d'origine est précisément qu'aucun parcours
  n'existait.
- **Exiger ffmpeg sur le `PATH`.** Écartée : ni winget ni chocolatey ne le
  garantissent, et une archive décompressée à la main ne l'est jamais. La détection
  couvre ces emplacements, mais la désignation manuelle doit rester possible.
- **Embarquer un binaire ffmpeg.** Écartée : plusieurs dizaines de Mo, une licence
  (GPL/LGPL selon le build) à assumer dans la distribution, et une version figée —
  pour un problème que règle un champ de saisie.

## Tests exigés

`FfmpegPathProvider` : `settings.json` absent ⇒ valeurs de `FfmpegOptions` ;
`settings.json` présent ⇒ il **prime** sur `FfmpegOptions` ; fichier illisible ou
malformé ⇒ repli sur le défaut **sans exception au démarrage** (l'application doit
s'ouvrir pour qu'on puisse la reconfigurer) ; l'enregistrement rend les nouveaux
chemins visibles **au retour de l'appel**, pas après un délai ; l'écriture est
atomique (aucun fichier tronqué observable). Adaptateurs : ils lisent bien
`IFfmpegPaths` (stub) et non la configuration.
