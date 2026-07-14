# ADR-0006 — WinUI 3 natif pour la présentation (remplace Blazor Server)

Statut : accepté — 2026-07-06. **Remplace l'ADR-0002.**

## Contexte

L'ADR-0002 retenait Blazor Server. À l'usage, le défaut est rédhibitoire pour
l'utilisateur : exécuter `Nagare.WinApp` **ouvre un navigateur** (c'est un
`WebApplication` ASP.NET). Le besoin réel est une **application Windows** —
fenêtre native, `.exe`, F5 dans Visual Studio ouvre l'app.

Le moment est optimal : la Clean Architecture isole la présentation, et le front
ne contient que le template (aucune page métier écrite). Le coût du pivot est
donc quasi nul aujourd'hui, et croissant chaque jour.

Deux familles de solutions existaient :
- **Hybride** (WinUI/MAUI + `BlazorWebView`) : vraie fenêtre, mais l'UI reste du
  Razor rendu dans un WebView2 — conserve MudBlazor.
- **XAML natif** : vrais contrôles Fluent, abandonne MudBlazor.

## Décision

**WinUI 3 (Windows App SDK), XAML natif, non-empaqueté**
(`WindowsPackageType=None`), cible `net10.0-windows10.0.19041.0`.
MVVM via **CommunityToolkit.Mvvm** (source-generated, pas de réflexion).
**MudBlazor est abandonné.**

Choix du non-empaqueté : un simple `.exe`, pas de certificat, pas de cycle
install/désinstall — F5 ouvre directement la fenêtre. Le MSIX n'apporterait
(identité d'app, toasts, Store) rien dont Nagare ait besoin.

## Conséquences

- `Domain`, `Application` et `Infrastructure` sont **intacts** : seule la couche
  de présentation est réécrite. C'est le dividende de la Clean Architecture.
- **Coût réel** : le push temps réel n'est plus gratuit. Les lignes de log et les
  stats ffmpeg arrivent sur un thread de fond et doivent être marshallées vers le
  thread UI (`DispatcherQueue`), avec une collection **bornée** (ring buffer) et
  un **throttle** des stats. Sans ces garde-fous, l'UI gèle.
- On perd la propriété « le stream survit à la fermeture de l'onglet » : fermer
  la fenêtre arrête l'app (et tue ffmpeg, comme la spec l'exige déjà).
- MudBlazor et les composants Razor sont supprimés (aucune perte : rien d'écrit).

## Alternatives écartées

- **MAUI Windows** : sur Windows, MAUI rend **via WinUI 3** de toute façon. On
  paierait une couche d'abstraction et ses bugs pour zéro gain — le mobile n'a
  aucun sens pour un outil pilotant ffmpeg/NVENC sur un fichier local.
- **Blazor Hybrid** (WinUI + `BlazorWebView`) : conserverait MudBlazor et une
  vraie fenêtre, mais l'utilisateur veut de **vrais contrôles natifs**, pas un
  WebView déguisé.

## Repli

Aucun template WinUI 3 n'est installé (`dotnet new` ne propose que MAUI /
WinForms / WPF) : le csproj sera écrit à la main. Un **spike de faisabilité est
la phase 0** du plan. En cas d'échec persistant, repli sur **WPF** (template
présent, thème Fluent type WPF-UI) : l'architecture MVVM et les ViewModels sont
identiques, seul le dialecte XAML change.
