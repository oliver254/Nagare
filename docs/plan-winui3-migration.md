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
| `Nagare.WinApp` | **Réécrit** en WinUI 3 |

### Point d'architecture assumé

Ceci introduit une dépendance `Nagare.Application → BrilliantMediator.Abstractions`.
C'est une dépendance sur des **interfaces de messagerie**, pas sur de
l'infrastructure — exactement ce que fait MediatR. Assumée par pragmatisme.
L'alternative puriste (conserver nos interfaces et adapter en Infrastructure)
coûterait une couche d'indirection pour un bénéfice théorique.

## 4. Structure cible de `Nagare.WinApp`

```
App.xaml(.cs)          Host builder (Microsoft.Extensions.Hosting) + DI + fenêtre
MainWindow.xaml(.cs)   Shell : NavigationView → Dashboard / Profiles / Channels
Views/                 DashboardPage, ProfilesPage, ChannelsPage  (XAML)
ViewModels/            DashboardViewModel, ProfilesViewModel, ChannelsViewModel (+ éditeurs)
Services/              FilePickerService (choix du .mp4), UiDispatcher
```

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

### Phase 1 — Combler le trou des tests *(indépendante du pivot, parallélisable)*

`Nagare.UnitTests` ne contient **aucun test**. À écrire :

- golden test du `FfmpegCommandBuilder` (commande exacte, caractère pour caractère) ;
- version masquée de la commande (clé jamais en clair) ;
- `StreamKeyScrubber` ;
- machine à états `StreamSession` (transitions autorisées, rejets, événements émis) ;
- invariants E1–E8 des value objects ;
- `FfmpegStatsParser` (`InternalsVisibleTo` déjà en place).

Ces tests portent sur Domain/Infrastructure : ils sont **insensibles** au pivot UI.

### Phase 2 — Application → BrilliantMediator

- Commands/queries implémentent `ICommand<T>` / `IQuery<T>`.
- Handlers : `HandleAsync(…)` → `Handle(…)`.
- Suppression de `Abstractions/Handlers.cs`.
- `AddBrilliantMediator().AddGeneratedHandlers().Build()` + `UseBrilliantMediator()`.
- **Critère de sortie** : build vert, tests de Phase 1 toujours verts.

### Phase 3 — `Nagare.WinApp` en WinUI 3

- Suppression de `Components/`, de MudBlazor, de l'hébergement ASP.NET
  (`Microsoft.NET.Sdk.Web` → `Microsoft.NET.Sdk`).
- Nouveau csproj (TFM Windows, `UseWinUI`, `WindowsPackageType=None`).
- `App.xaml.cs` : Host builder, `AddNagareApplication()`, `AddNagareInfrastructure()`,
  BrilliantMediator, ouverture de `MainWindow`.
- `MainWindow` + `NavigationView`.
- **Critère de sortie** : F5 → la fenêtre s'ouvre, aucun navigateur.

### Phase 4 — Vues & ViewModels

1. **Channels** : CRUD, clé en `PasswordBox` (jamais réaffichée).
2. **Profiles** : CRUD, invariants d'encodage remontés en erreurs de validation.
3. **Dashboard** : choix du fichier (`FilePickerService`), du profil, du channel ;
   **aperçu de la commande ffmpeg générée, clé masquée** (exigence de la spec) ;
   start / stop.

### Phase 5 — Temps réel

Abonnement à `ISessionMonitor`, marshalling `DispatcherQueue`, ring buffer 500
lignes, throttle stats 1/s, `ListView` virtualisée, badge de statut
(Starting / Running / Reconnecting / Stopped / Failed) et indicateur de santé
(`speed < 1.0x` → alerte).

### Phase 6 — Vérification

`dotnet build` + `dotnet test` verts, exécution manuelle réelle (nécessite
ffmpeg/ffprobe installés — **absents du PATH aujourd'hui**), puis audit
`reviewer` (Clean Arch, DDD, CQRS, non-fuite de la clé).

## 8. Risques

| # | Risque | Parade |
|---|---|---|
| ~~R1~~ | ~~Pas de template WinUI 3 ⇒ csproj manuel~~ | ✅ **Levé** : spike validé (build vert + fenêtre native). Config exacte en §7 phase 0. Repli WPF écarté. |
| R2 | `FileOpenPicker` en non-empaqueté exige l'interop HWND WinRT | Connu, documenté, ~5 lignes |
| R3 | UI figée par le débit de logs ffmpeg | Ring buffer borné + throttle (§5) — non négociables |
| R4 | ffmpeg/ffprobe absents du PATH | Sans impact sur build/tests ; bloque seulement l'exécution réelle |

## 9. Ce qui ne change pas

Le **modèle du domaine** (docs/domain-model.md) et les **5 arbitrages validés**
(session unique, échec initial sans backoff, `bufsize ≥ bitrate`, sessions non
persistées, clé masquée) restent intégralement en vigueur. La feature
**planification de stream** (docs/product/stream-scheduling.md) reste en
itération 2, inchangée.
