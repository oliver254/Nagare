# Plan — Migration de la présentation vers WinUI 3 natif

> Design validé le 2026-07-06. Remplace le front Blazor Server par une vraie
> application Windows. Voir ADR-0006 (WinUI 3) et ADR-0007 (BrilliantMediator).

## 1. Pourquoi

L'exécution de `Nagare.WinApp` ouvrait un **navigateur** (c'est un
`WebApplication` ASP.NET). Le besoin réel est une **application Windows** :
fenêtre native, `.exe`, F5 dans Visual Studio → la fenêtre s'ouvre.

Le pivot est peu coûteux **maintenant** : la Clean Architecture isole la
présentation, et le front ne contient que le template (aucune page métier
écrite). Chaque jour d'attente aurait renchéri l'opération.

## 2. Décisions actées

| Sujet | Décision |
|---|---|
| Framework UI | **WinUI 3** (Windows App SDK), XAML natif, Fluent |
| Packaging | **Non-empaqueté** (`WindowsPackageType=None`) — simple `.exe`, pas de MSIX, pas de certificat |
| MVVM | **CommunityToolkit.Mvvm** (`[ObservableProperty]`, `[RelayCommand]` — source-generated, zéro réflexion) |
| CQRS | **BrilliantMediator 3.0.0 + BrilliantMediator.SourceGenerator** (auto-enregistrement à la compilation) |
| MudBlazor | **Abandonné** (incompatible avec du XAML natif) |
| Cible | `net10.0-windows10.0.19041.0` |

## 3. Impact par couche

| Couche | Impact |
|---|---|
| `Nagare.Domain` | **Inchangé** — zéro dépendance, ignore l'UI |
| `Nagare.Application` | **Retouché** : commands/queries implémentent `ICommand<T>`/`IQuery<T>` ; handlers `HandleAsync` → `Handle` ; `Abstractions/Handlers.cs` (interfaces maison) supprimé. Ports et coordinateur inchangés. |
| `Nagare.Infrastructure` | **Inchangé** — ffmpeg, Data Protection/DPAPI, repos JSON |
| `Nagare.ViewModels` | **Nouveau projet** (non prévu au plan initial) — les trois ViewModels, en `net10.0` sans aucune dépendance WinUI, donc testables (§4) |
| `Nagare.WinApp` | **Réécrit** en WinUI 3 — ne contient plus que du XAML et ce qui en dépend |

### Point d'architecture assumé

Ceci introduit une dépendance `Nagare.Application → BrilliantMediator.Abstractions`.
C'est une dépendance sur des **interfaces de messagerie**, pas sur de
l'infrastructure — exactement ce que fait MediatR. Assumée par pragmatisme.
L'alternative puriste (conserver nos interfaces et adapter en Infrastructure)
coûterait une couche d'indirection pour un bénéfice théorique.

## 4. Structure cible de la présentation

```
Nagare.WinApp/                        ← tout le XAML, et lui seul
  App.xaml(.cs)          Host builder (Microsoft.Extensions.Hosting) + DI + fenêtre
  MainWindow.xaml(.cs)   Shell : NavigationView → Dashboard / Profiles / Channels
  Views/                 DashboardPage, ProfilesPage, ChannelsPage  (XAML)
  Services/              FilePickerService (choix du .mp4), UiDispatcher, MainWindowContext
  Converters/            ValueConverters.cs

Nagare.ViewModels/                    ← projet séparé, net10.0, SANS dépendance WinUI
  DashboardViewModel, ProfilesViewModel, ChannelsViewModel, ViewModelBase
  Abstractions/          IUiDispatcher, IVideoFilePicker (implémentés côté WinApp)
  Shell/                 ShutdownGuard — séquencement de l'arrêt (SPEC §5)
```

**Les ViewModels vivent dans leur propre projet**, et non dans un dossier de
`Nagare.WinApp` comme prévu initialement. Raison : un projet `net10.0` sans le
moindre type XAML (`Brush`, `Visibility`, `DispatcherQueue`) est **testable en
ligne de commande**, ce qu'un projet WinUI n'est pas. Les deux abstractions qui
touchent réellement à l'UI (dispatcher, sélecteur de fichier) sont des interfaces
implémentées côté `Nagare.WinApp`. C'est ce qui rend possible les tests de
ViewModels de `tests/Nagare.UnitTests/ViewModels/`.

Packages : `Microsoft.WindowsAppSDK`, `CommunityToolkit.Mvvm`,
`BrilliantMediator`, `BrilliantMediator.SourceGenerator`.

## 5. Flux de données

- **Actions** : bouton → `[RelayCommand]` → `_mediator.DispatchAsync<StartStreamCommand, SessionId>(cmd)` → handler → Infrastructure.
- **Lectures** : `_mediator.SendAsync<GetChannelsQuery, IReadOnlyList<ChannelDto>>(…)`.
- **Temps réel** : `ISessionMonitor` émet depuis le thread lecteur de stderr ffmpeg ; le `DashboardViewModel` marshalle vers le thread UI via `DispatcherQueue.TryEnqueue`.

### Deux règles non négociables

Blazor Server offrait le push UI gratuitement ; en XAML il faut le construire.
Sans ces deux garde-fous, l'UI **gèle** sous le débit de ffmpeg :

1. **Logs** : `ObservableCollection` **bornée** en ring buffer (500 dernières
   lignes) + `ListView` virtualisée.
2. **Stats** (fps / bitrate / speed) : **throttle à ~1 mise à jour/seconde**.
   ffmpeg émet des stats plusieurs fois par seconde ; notifier l'UI à chaque
   ligne est le moyen le plus sûr de la tuer.

## 6. Erreurs & sécurité

- `DomainException` et erreurs process remontent au ViewModel → `InfoBar`.
- **Clé de stream** : saisie en `PasswordBox`, **jamais réaffichée**, jamais
  loggée (`StreamKeyScrubber` déjà en place — ADR-0005).
- **ffmpeg absent** : détecté au démarrage (`IFfmpegEnvironmentProbe`, déjà
  écrit) → `InfoBar` bloquante sur le Dashboard.

## 7. Plan d'exécution

### Phase 0 — Spike de faisabilité WinUI 3 — ✅ **VALIDÉE (2026-07-06)**

Aucun template WinUI 3 n'est installé sur la machine (`dotnet new` ne propose
que MAUI / WinForms / WPF) : le csproj a été **écrit à la main**.

**Résultat : `dotnet build` vert (0 erreur, 0 avertissement), l'exécutable se
lance et une vraie fenêtre native s'ouvre** (titre remonté par Windows). Le
repli WPF est **écarté**, le workload VS supplémentaire est **inutile**.

**Configuration validée — à reprendre telle quelle pour `Nagare.WinApp` :**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <Platforms>x64</Platforms>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <UseWinUI>true</UseWinUI>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="2.2.0" />
  </ItemGroup>
</Project>
```

Fichiers requis : `app.manifest` (avec `dpiAwareness = PerMonitorV2`),
`App.xaml(.cs)` (dictionnaire `XamlControlsResources`, `OnLaunched` qui active la
fenêtre) et `MainWindow.xaml(.cs)`. Le point d'entrée `Main` est **généré** par
le SDK — ne pas l'écrire.

`WindowsAppSDKSelfContained=true` est important : il évite d'exiger
l'installation séparée du runtime Windows App SDK sur la machine cible.

### Phase 1 — Combler le trou des tests — ✅ **VALIDÉE (2026-07-14)**

*(indépendante du pivot, menée en parallèle)*

`Nagare.UnitTests` était vide. Écrits depuis :

- golden test du `FfmpegCommandBuilder` (commande exacte, caractère pour caractère) ;
- version masquée de la commande (clé jamais en clair) ;
- `StreamKeyScrubber` ;
- machine à états `StreamSession` (transitions autorisées, rejets, événements émis) ;
- invariants E1–E8 des value objects ;
- `FfmpegStatsParser` (`InternalsVisibleTo` déjà en place) ;
- `StreamSessionCoordinator` et `GetStartPreflightQuery` (couche Application) ;
- les trois ViewModels — possible **uniquement** parce que `Nagare.ViewModels` est
  un projet sans dépendance WinUI (§4).

Ces tests portent sur Domain/Infrastructure : ils sont **insensibles** au pivot UI.
La suite compte **260 tests** au 2026-07-23.

### Phase 2 — Application → BrilliantMediator — ✅ **VALIDÉE (2026-07-14)**

- Commands/queries implémentent `ICommand<T>` / `IQuery<T>`.
- Handlers : `HandleAsync(…)` → `Handle(…)`.
- Suppression de `Abstractions/Handlers.cs`.
- `AddBrilliantMediator().AddGeneratedHandlers().Build()` + `UseBrilliantMediator()`.
- **Critère de sortie** : build vert, 192 tests toujours verts. ✅ Les 13 handlers sont
  enregistrés par le générateur (exactement ceux câblés à la main auparavant).

> ⚠️ **Deux défauts d'empaquetage de `BrilliantMediator.SourceGenerator` 3.0.0**, contournés dans
> `Nagare.Application.csproj` — à retirer dès qu'une version corrigée sort :
> 1. Le paquet publie la DLL du générateur dans `lib/netstandard2.0/` au lieu de
>    `analyzers/dotnet/cs/` : **Roslyn ne la charge donc pas**, et `OutputItemType="Analyzer"`
>    (valable pour un `ProjectReference`, pas pour un `PackageReference`) n'y change rien. Sans
>    contournement, `AddGeneratedHandlers()` n'existe simplement pas → erreur CS1061. Contournement :
>    `GeneratePathProperty="true"` + `<Analyzer Include="$(PkgBrilliantMediator_SourceGenerator)\…" />`,
>    avec `ExcludeAssets="all"` pour ne pas référencer une DLL netstandard2.0 à l'exécution.
> 2. Le code généré emploie l'annotation `?` (`GetSessionStatusQuery` répond `SessionSnapshot?`)
>    **sans émettre `#nullable enable`**, ce que le compilateur exige d'un fichier auto-généré →
>    CS8669, fatal ici (`TreatWarningsAsErrors`). Contournement : `NoWarn` ciblé sur CS8669, qui ne
>    peut par construction viser que du code généré.

### Phase 3 — `Nagare.WinApp` en WinUI 3 — ✅ **VALIDÉE (2026-07-14)**

- Suppression de `Components/`, de MudBlazor, de l'hébergement ASP.NET
  (`Microsoft.NET.Sdk.Web` → `Microsoft.NET.Sdk`).
- Nouveau csproj (TFM Windows, `UseWinUI`, `WindowsPackageType=None`).
- `App.xaml.cs` : Host builder, `AddNagareApplication()`, `AddNagareInfrastructure()`,
  BrilliantMediator, ouverture de `MainWindow`.
- `MainWindow` + `NavigationView` → 3 pages placeholder (Dashboard / Profiles / Channels).
- **Critère de sortie** : F5 → la fenêtre s'ouvre, aucun navigateur. ✅ Constaté réellement :
  l'exécutable se lance, le process vit, `MainWindowTitle = "Nagare"`.

> **Arrêt propre de ffmpeg — le piège du thread UI.** La fermeture de la fenêtre ne peut PAS
> bloquer sur `host.StopAsync()` (`GetAwaiter().GetResult()`) : le coordinateur attend sa boucle
> mailbox sans `ConfigureAwait(false)`, sa continuation est donc postée sur le thread UI — que
> l'on vient de bloquer. **Interblocage, et ffmpeg survit à la fermeture** (violation de la SPEC §5).
> Parade retenue : `AppWindow.Closing` → `args.Cancel = true`, arrêt asynchrone de l'hôte, puis
> `Close()` réel. La boucle de messages reste vivante, donc l'arrêt aboutit.

> ⚠️ **Piège de configuration à ne pas rater.** En quittant `Microsoft.NET.Sdk.Web`, on perd
> le chargement **implicite** des `appsettings.json` *et* des **User Secrets**. Le nouveau
> host doit donc, explicitement :
> - référencer `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Configuration.Json` et
>   **`Microsoft.Extensions.Configuration.UserSecrets`** (non inclus dans le SDK classique) ;
> - appeler `AddJsonFile("appsettings.json")` et `AddUserSecrets<App>(optional: true)`
>   (`UserSecretsId = nagare-winapp-local`) ;
> - copier `appsettings.json` dans le dossier de sortie.
>
> Sans ça, `FfmpegOptions.ExecutablePath` retombe **silencieusement** sur `"ffmpeg"` résolu
> depuis le `PATH` — où il n'est **pas** sur cette machine. Le symptôme serait un « ffmpeg
> introuvable » incompréhensible alors que le binaire est bien installé.

### Phase 4 — Vues & ViewModels — ✅ **VALIDÉE (2026-07-14)**

1. **Channels** : CRUD, clé en `PasswordBox` (jamais réaffichée — le champ d'édition
   est vidé même en modification, la clé étant illisible par construction).
2. **Profiles** : CRUD, invariants d'encodage remontés en erreurs de validation.
3. **Dashboard** : choix du fichier (`FilePickerService`), du profil, du channel ;
   **aperçu de la commande ffmpeg générée, clé masquée** (exigence de la spec) ;
   start / stop.

**Critère de sortie** : les trois pages câblées, les trois ViewModels couverts par
des tests unitaires. ✅

### Phase 5 — Temps réel — ✅ **VALIDÉE (2026-07-14)**

Abonnement à `ISessionMonitor`, marshalling par `IUiDispatcher` (implémenté sur
`DispatcherQueue.TryEnqueue`), ring buffer 500 lignes, throttle stats 1/s,
`ListView` virtualisée, badge de statut (Starting / Running / Reconnecting /
Stopped / Failed) et indicateur de santé (`speed < 1.0x` → alerte).

Deux précisions par rapport au plan initial :

- les lignes de log entrantes passent par une `ConcurrentQueue` drainée par **un
  seul** rappel UI en attente — sans cette coalescence, un `TryEnqueue` par ligne
  suffirait à saturer la file du dispatcher sous le débit de ffmpeg ;
- les changements de **statut** court-circuitent le throttle : ils sont rares et
  doivent être instantanés. Seules les stats pures sont retardées.

### Phase 6 — Vérification — ✅ **VALIDÉE (2026-07-23)**

`dotnet build` : **0 erreur, 0 avertissement**. `dotnet test` : **260 tests, 0 échec**.
`dotnet list package --vulnerable --include-transitive` : **aucun paquet vulnérable**
sur les 6 projets.

Audit `reviewer` — **aucun bloquant sur les quatre axes exigés** :

| Axe | Verdict |
|---|---|
| Non-fuite de la clé | ✅ étanche sur tous les chemins (DTO, aperçu, saisie, logs, exceptions) ; **aucun usage de `Clipboard` dans `src/`** ; un test par réflexion interdit toute propriété exposant une clé |
| Sens des dépendances | ✅ garanti **par le compilateur** — `Nagare.ViewModels` ne référence aucun paquet WinUI ; `using Nagare.Infrastructure` n'apparaît que dans le composition root |
| Garde-fous temps réel | ✅ ring buffer, throttle et coalescence corrects, couverts par des tests à dispatcher **différé** (un dispatcher inline ne prouverait rien) |
| CQRS (ADR-0007) | ✅ cohérent ; aucune mutation dans une query |

L'audit a en revanche trouvé **deux défauts sur le chemin d'arrêt** — l'endroit
même que l'encadré de la phase 3 signalait comme piégeux. **Corrigés le 2026-07-23 :**

1. **Le second clic sur la croix orphelinait ffmpeg.** Le garde
   `if (_shuttingDown) return;` sortait **sans remettre `args.Cancel = true`**. Or
   l'arrêt dure plusieurs secondes (grâce de 5 s sur ffmpeg, puis
   `HostOptions.ShutdownTimeout`) et la fenêtre reste cliquable pendant tout ce
   temps. Un utilisateur qui reclique, croyant que rien ne s'est passé, fermait la
   fenêtre pour de vrai : boucle de messages terminée, process mort **avant**
   `DisposeHostAsync()` — donc avant le `Kill(entireProcessTree)` de dernier
   recours. **ffmpeg survivait, toujours en diffusion**, ce que la SPEC §5
   interdit. Le `finally` interne, pourtant écrit pour ça, ne couvrait pas ce
   chemin-là. Correctif : **deux** drapeaux — `_shuttingDown` (« en cours,
   continuer d'annuler ») et `_readyToClose` (posé juste avant notre propre
   `Close()`). La fermeture est désormais annulée à **tous** les passages sauf le
   dernier.
2. **Le gestionnaire d'erreur d'arrêt levait à son tour.** Le `catch` résolvait
   `ILogger<App>` alors que le `finally` interne avait déjà disposé le
   `ServiceProvider` : `ObjectDisposedException` depuis un `async void`, et
   l'erreur d'origine perdue au passage. Le logger est maintenant résolu **avant**
   `StopAsync()` et conservé en variable locale.

**La relecture du correctif y a trouvé une régression, corrigée à son tour.** La
résolution du logger, déplacée hors du `catch`, avait atterri entre « arrêt en
cours » et le bloc protégé : une exception à cet endroit laissait les drapeaux dans
un état où la fenêtre s'annulait elle-même indéfiniment sans jamais atteindre le
`finally` qui la libère — application infermable **et** ffmpeg orphelin. Le
correctif d'un orphelin en recréait donc un autre.

La leçon a été tirée à la racine : le séquencement vit désormais dans
**`ShutdownGuard`** (`Nagare.ViewModels/Shell/`), une classe sans aucun type WinUI
que `Nagare.UnitTests` peut atteindre. Le rapport d'erreur y est un **délégué capté
à la construction**, donc plus aucune résolution de service ne se produit pendant un
arrêt. **8 tests** couvrent la règle, dont ceux des deux défauts réels : le second
clic pendant l'arrêt, et le rapporteur d'erreur qui lève.

Vérification à l'exécution : trois `WM_CLOSE` postés sans intervalle sur la fenêtre,
l'application sort en **code 0**, sans blocage et sans ffmpeg résiduel. Réserve
d'honnêteté : sans diffusion active l'arrêt est quasi instantané, ce test prouve la
réentrance mais **pas** le scénario d'orphelin de bout en bout, qui exige une vraie
session ffmpeg. C'est précisément le trou que les tests de `ShutdownGuard` comblent :
eux pilotent l'instant de la fin d'arrêt, ce qu'aucun lancement réel ne permet.

Le reste des remarques de l'audit (code mort `IsCreating`, `IsBusy` non bindé,
`LastError` non affiché, souscription tardive dans `LoadAsync`, exceptions loggées
non scrubbées) relève de l'UX ou du nettoyage et rejoint le chantier de conception
`docs/design/prompt-ux-ui.md`.

Passage réel à l'écran après la montée de version — Data Protection chiffre la clé de
stream, un build vert ne dit rien de l'exécution : l'exécutable se lance, la fenêtre
`Nagare` s'ouvre, la fermeture rend **code 0** (donc l'arrêt asynchrone de l'hôte
n'interbloque pas, cf. l'encadré de la phase 3) et **aucun process ffmpeg ne survit**.

> ⚠️ **La vérification a d'abord été bloquée en amont de la compilation.** L'avis
> NU1903 publié sur `System.Security.Cryptography.Xml` 10.0.9 — tiré transitivement
> par `Microsoft.AspNetCore.DataProtection` 10.0.9 — faisait échouer la
> **restauration** : l'audit NuGet est traité en erreur (`Directory.Build.props`),
> délibérément, parce que la clé de stream est en jeu. Ni build ni tests ne
> tournaient plus, sur un dépôt pourtant inchangé depuis son dernier état vert.
>
> Correctif : passage à `Microsoft.AspNetCore.DataProtection` 10.0.10. Le bump seul
> a déclenché une cascade de NU1605 (« passage à une version antérieure »), car les
> `Microsoft.Extensions.*` étaient épinglés de façon hétérogène — 10.0.0 dans
> `Nagare.Application` et `Nagare.ViewModels`, 10.0.9 ailleurs. **Toute la
> solution est désormais alignée sur 10.0.10.**

**Vérification bout-en-bout réelle — possible** : la commande de la spec a été
validée contre un vrai ffmpeg (exit 0, encodage NVENC, débit conforme au CBR).
Une vraie diffusion reste à tester : elle exige une clé de diffusion valide.

## 8. Risques

| # | Risque | Parade |
|---|---|---|
| ~~R1~~ | ~~Pas de template WinUI 3 ⇒ csproj manuel~~ | ✅ **Levé** : spike validé (build vert + fenêtre native). Config exacte en §7 phase 0. Repli WPF écarté. |
| ~~R2~~ | ~~`FileOpenPicker` en non-empaqueté exige l'interop HWND WinRT~~ | ✅ **Levé** : `InitializeWithWindow.Initialize(picker, hwnd)` dans `FilePickerService`, le HWND venant de `MainWindowContext`. Sans lui, le sélecteur lève `COMException 0x80070578` à l'affichage. |
| ~~R3~~ | ~~UI figée par le débit de logs ffmpeg~~ | ✅ **Levé** : ring buffer 500 + throttle 1/s + coalescence des rappels UI (§5, phase 5). Reste **non négociable** pour tout ajout ultérieur. |
| ~~R4~~ | ~~ffmpeg/ffprobe introuvables~~ | ✅ **Levé** : la commande de la spec a été **validée contre un vrai ffmpeg** (exit 0, NVENC). Le chemin se configure via les **User Secrets** (jamais dans le dépôt) quand ffmpeg n'est pas dans le `PATH`. |
| R5 | **Un avis de sécurité publié sur une dépendance casse le build d'un dépôt inchangé** — c'est l'effet voulu de l'audit NuGet traité en erreur, mais il frappe sans prévenir et bloque *avant* la compilation (survenu le 2026-07-23, cf. phase 6). | Assumé : on préfère un build rouge à une clé de stream exposée. Parade : garder les `Microsoft.Extensions.*` **alignés sur une seule version** dans toute la solution, sans quoi le moindre bump déclenche une cascade de NU1605. Un `Directory.Packages.props` (gestion centralisée des versions) rendrait l'alignement mécanique — à envisager si le cas se reproduit. |

## 9. Ce qui ne change pas

Le **modèle du domaine** (docs/domain-model.md) et les **5 arbitrages validés**
(session unique, échec initial sans backoff, `bufsize ≥ bitrate`, sessions non
persistées, clé masquée) restent intégralement en vigueur. La feature
**planification de stream** (docs/product/stream-scheduling.md) reste en
itération 2, inchangée.
