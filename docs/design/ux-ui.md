# Conception UX/UI — Nagare (WinUI 3)

> Livrable 1 du brief `docs/design/prompt-ux-ui.md`. À valider avant tout XAML.
> Audit rejoué sur le code réel : `src/Nagare.WinApp/**`, `src/Nagare.ViewModels/**`,
> `src/Nagare.Application/Streaming/GetStartPreflightQuery.cs`.

---

## 1. Audit — constat → loi violée → conséquence

| # | Constat (vérifié dans le code) | Loi violée | Conséquence pour l'utilisateur |
|---|---|---|---|
| A1 | `MainWindow.xaml` : `NavigationView` à 3 entrées **sans icône**, sans Mica, titre système nu | Jakob, Aesthetic-Usability | L'app ne ressemble à aucune app Windows 11 ; repérage par lecture seule |
| A2 | `DashboardPage.xaml` : 5 `Grid.Row` empilées, **aucun regroupement** | Common Region, Proximity, Chunking | Mur plat : rien ne dit où finit la configuration et où commence la surveillance |
| A3 | `StartCommand.CanExecute = Preflight.CanStart`, mais `EnvironmentMessage`/`MediaMessage` renvoient `null` pour `ProfileNotSelected`, `ChannelNotSelected`, `InputFileNotSelected`, `SessionAlreadyActive`, `NotChecked` | Zeigarnik, Goal-Gradient | **Trou n° 1** : bouton mort, aucune explication. 5 des 10 raisons de blocage sont muettes |
| A4 | `ViewModelBase.IsBusy` existe, **bindé nulle part** ; la sonde d'environnement lance 3 process, `ffprobe` 1 | Doherty (400 ms) | L'app paraît figée au chargement et au choix de fichier |
| A5 | `DeleteAsync` (Profils, Channels) s'exécute au clic ; **aucun `ContentDialog` dans le projet** | Postel (strict en sortie), Mental Model | Perte irréversible sans filet |
| A6 | Aucun état vide : au 1er lancement, 2 `ComboBox` vides + 2 `ListView` vides | Paradox of the Active User | L'utilisateur neuf est bloqué sans savoir qu'il doit créer profil **et** channel |
| A7 | Éditeur de profil : 15 contrôles en colonne plate jusqu'à `keyint_min`, aucun preset | Hick, Choice Overload, Miller, Tesler | Il faut choisir un `keyint_min` pour diffuser une vidéo |
| A8 | Santé = `Ellipse` colorée seule (`HealthToBrushConverter`) | Accessibilité §9, Von Restorff | Statut invisible pour un daltonien ; invisible en contraste élevé |
| A9 | 5 stats en `StackPanel Horizontal` sans libellé de groupe | Miller, Selective Attention | 5 nombres bruts alignés : rien ne se lit en 2 s |
| A10 | Journal brut : pas d'auto-scroll, pas de copie, erreurs non distinguées | Occam, Selective Attention | La ligne d'erreur ffmpeg se noie dans le flux de progression |
| A11 | `Démarrer` et `Arrêter` **côte à côte**, taille par défaut | Fitts | Arrêt accidentel d'une diffusion en cours |
| A12 | Zéro `AutomationProperties`, zéro `KeyboardAccelerator`, zéro `ToolTip` | Accessibilité §9 | Aucun parcours clavier ; lecteur d'écran muet sur les boutons icône |
| A13 | Aucun bilan à l'arrêt : la page retombe sur `StatusLabel = "Arrêtée"` et des stats à 0 | Peak-End Rule | Une diffusion se termine sur un vide ; un échec sur un vide **et** une frustration |
| A14 | InfoBar d'erreur en haut de page, loin du champ fautif | Selective Attention | Bannière apprise puis ignorée |

## 2. Principes directeurs (5)

1. **Le tableau de bord répond en 2 secondes.** *Ça diffuse ? c'est sain ? vers quoi ? avec quel fichier ?* — quatre réponses, quatre régions ceinturées.
2. **Un bouton désactivé parle toujours.** Aucune raison de blocage muette : les 10 `StartBlockReason` ont une phrase.
3. **Un seul accent par écran.** Le rouge est un fait (`speed < 1.0x`, échec, reconnexion), jamais une décoration.
4. **L'app absorbe ffmpeg, l'expert garde la main.** Presets et valeurs par défaut devant ; commande générée et journal brut toujours accessibles, jamais imposés.
5. **Rien n'interrompt une diffusion.** Pendant `Starting/Running/Reconnecting` : aucune modale, aucun vol de focus, aucune InfoBar bloquante.

**Ce que ces principes excluent :** pas de tableau de bord configurable, pas de graphiques de bitrate, pas d'onglets dans le dashboard, pas d'assistant multi-étapes, pas de thème maison, pas de notification système (app non empaquetée, §7.5 du brief).

## 3. Tokens — aucune valeur en dur

**Espacement (grille 4).** Marge de page `24` · padding interne de carte `16` · écart entre cartes `16` · rythme vertical des champs `12` · écart de contrôles en ligne `8`.

**Typographie** — styles WinUI existants, aucun style inventé :
`TitleTextBlockStyle` (titre de page) · `SubtitleTextBlockStyle` (titre de carte) · `BodyStrongTextBlockStyle` (libellé fort, valeur de stat) · `BodyTextBlockStyle` (texte) · `CaptionTextBlockStyle` (unité, aide, chemin). Monospace `Consolas` réservée à **deux** surfaces : aperçu de commande et journal.

**Couleurs sémantiques** — `ThemeResource` uniquement :

| Rôle | Premier plan | Fond (InfoBar / badge) |
|---|---|---|
| Succès (`Running` sain) | `SystemFillColorSuccessBrush` | `SystemFillColorSuccessBackgroundBrush` |
| Alerte (`Reconnecting`, `speed < 1`) | `SystemFillColorCautionBrush` | `SystemFillColorCautionBackgroundBrush` |
| Erreur (`Failed`, environnement KO) | `SystemFillColorCriticalBrush` | `SystemFillColorCriticalBackgroundBrush` |
| Information (`Starting`) | `SystemFillColorAttentionBrush` | `SystemFillColorAttentionBackgroundBrush` |
| Neutre (`Stopped`, aucune session) | `TextFillColorSecondaryBrush` | `LayerFillColorDefaultBrush` |

**Surfaces.** Carte = `CardBackgroundFillColorDefaultBrush` + bordure 1 px `CardStrokeColorDefaultBrush` + `CornerRadius="{ThemeResource OverlayCornerRadius}"`. Contrôles : `ControlCornerRadius`. Fenêtre : Mica (`SystemBackdrop`). **Aucune ombre sur les cartes** — la ceinture suffit.

**Iconographie.** `SymbolIcon` avec l'énumération `Symbol` partout où elle couvre le besoin (vérifiée à la compilation : `Play`, `Stop`, `Add`, `Edit`, `Delete`, `Copy`, `OpenFile`, `Refresh`, `Video`, `Setting`). Là où elle ne suffit pas (icônes du rail), `FontIcon` **Segoe Fluent Icons** avec un glyphe vérifié dans la police installée au moment de l'implémentation — aucun codepoint n'est figé ici.

## 4. Écran par écran

### 4.1 Shell

```
┌──────────────────────────────────────────────────────────────┐
│ 流 Nagare                                          – ▢ ✕     │  titre custom, Mica
├────────────────┬─────────────────────────────────────────────┤
│ ▤ Tableau de   │                                             │
│   bord      ●  │                                             │
│ ▤ Profils      │              (Frame)                        │
│ ▤ Channels     │                                             │
│ · · · · · · ·  │  ← emplacement réservé « Planifications »   │
└────────────────┴─────────────────────────────────────────────┘
```

Changements : icônes sur les 3 entrées, `SystemBackdrop="MicaBackdrop"`, wordmark **流 Nagare** en `PaneHeader`, `IsPaneToggleButtonVisible` conservé. *(`ExtendsContentIntoTitleBar` et la barre de titre personnalisée sont **reportés** : le wordmark tient dans le volet, et étendre le contenu dans la barre de titre demande de reprendre les zones de glisser et les boutons système — hors périmètre ici.)* La 4ᵉ entrée n'est **pas** ajoutée (elle n'existe pas) : le rail est simplement dimensionné pour l'accueillir.

### 4.2 Tableau de bord — état nominal (pas de diffusion)

```
Tableau de bord                                    (⟳ Actualiser : reporté)

┌─ Source ───────────────────────────────────────────────────┐
│  ┌──────────────────────────────────────────────────────┐  │
│  │   Déposez une vidéo ici, ou   [📂 Choisir un fichier…]│  │  zone drop, ≥96 px
│  └──────────────────────────────────────────────────────┘  │
│  boucle.mp4                                                │  Caption, ellipsis
│  00:04:12 · 1920×1080 · 30 fps · h264 · aac                │  BodyStrong
│  ⚠ Fichier illisible par ffprobe.                          │  Critical, inline (A14)
└────────────────────────────────────────────────────────────┘

┌─ Diffusion ────────────────────────────────────────────────┐
│  Profil d'encodage          Channel                        │
│  [ Twitch 1080p60      ▾ ]  [ Ma chaîne Twitch         ▾ ] │
│  ▸ Commande ffmpeg (clé masquée)                  [Copier] │  Expander replié
└────────────────────────────────────────────────────────────┘

┌─ Lancement ────────────────────────────────────────────────┐
│  ✓ Environnement   ✓ Fichier   ✓ Profil   ○ Channel        │  checklist (A3)
│                                                            │
│  [ ▶  Démarrer ]        Choisissez un channel de diffusion.│  accent, 40 px
└────────────────────────────────────────────────────────────┘

┌─ Journal ffmpeg ─────────────────────────────── [Copier] ──┐
│  (500 dernières lignes, Consolas 12, virtualisé)           │
└────────────────────────────────────────────────────────────┘
```

La carte **Santé** n'apparaît qu'en session (Occam : rien à surveiller hors diffusion).

### 4.3 Tableau de bord — état « en direct »

```
┌─ Santé ────────────────────────────────────────────────────┐
│  ●▶ En direct        vers Ma chaîne Twitch · boucle.mp4    │  badge icône+forme+texte
│                                                            │
│  ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌──────────┐  │
│  │ 30     │ │ 6000   │ │ 1,02x  │ │ 0      │ │ 0        │  │  StatTile (A9)
│  │ fps    │ │ kbits/s│ │ vitesse│ │ drops  │ │ reconnex.│  │
│  └────────┘ └────────┘ └────────┘ └────────┘ └──────────┘  │
│                                     [ ■  Arrêter ]         │  aligné à DROITE (A11)
└────────────────────────────────────────────────────────────┘
```

`Démarrer` et `Arrêter` **n'existent jamais en même temps** : la carte *Lancement* et la carte *Santé* occupent la même place et se remplacent selon `IsSessionActive`. `Arrêter` est donc dans la carte *Santé*, aligné à droite — et non dans *Lancement*, qui est repliée pendant la diffusion. Fitts est servi (grande cible, fin de parcours) et l'arrêt accidentel disparaît.

En session, les cartes *Source* et *Diffusion* passent en lecture seule (contrôles désactivés) : leur contenu reste la réponse à « vers quoi ? avec quel fichier ? ».

### 4.4 Tous les états

| État | Déclencheur (code) | Rendu |
|---|---|---|
| Vide (1er lancement) | `Profiles.Count == 0` ou `Channels.Count == 0` | Carte *Diffusion* remplacée par un `EmptyState` + CTA « Créer un profil » / « Créer un channel » qui **navigue** vers la page |
| Chargement | `IsBusy` | `ProgressRing` dans l'en-tête de page + cartes désactivées (A4) |
| Nominal | `Preflight.CanStart` | `Démarrer` actif, accentué, checklist 4/4 |
| Bloqué | `!Preflight.CanStart` | `Démarrer` inactif + **phrase de la raison** à côté + item de checklist en `○` |
| Environnement KO | `EnvironmentIssue is not null` | `InfoBar` Error non fermable, **en tête de la carte Lancement** (dans le flux, pas en bannière de page) |
| Démarrage | `Status == Starting` | Badge bleu « Démarrage », `ProgressRing` dans le badge, stats grisées |
| En direct | `Status == Running` | Badge vert « En direct » |
| Reconnexion | `Status == Reconnecting` | Badge ambre « Reconnexion (n) », **sans** modale ni InfoBar (Flow) |
| Alerte santé | `IsHealthWarning` | Tuiles *vitesse* et *drops* passent en `Caution`, badge en ambre |
| Échec | `Status == Failed` | Badge rouge « Échec » + `InfoBar` Error portant `LastError` |
| Arrêtée | `Status == Stopped` | **Bilan de session** dans la carte Santé (voir §7) |

### 4.5 Profils

```
Profils d'encodage                    [+ Nouveau] [✎ Modifier] [🗑 Supprimer]

┌──────────────────────────┐  ┌─ Édition ──────────────────────┐
│ Twitch 1080p60           │  │  Modèle  [Twitch 1080p60    ▾] │  ← presets (A7)
│ h264_nvenc · p5 · cbr …  │  │  Nom     [_____________]       │
│──────────────────────────│  │  ── Vidéo ──                   │
│ (liste, ou EmptyState)   │  │  Codec · Preset · Rate control  │
│                          │  │  ── Débit ──                   │
│                          │  │  Bitrate · Maxrate · Bufsize    │
│                          │  │  ── Audio ──                   │
│                          │  │  Codec · Bitrate · Sample rate  │
│                          │  │  ▸ Avancé                       │  ← replié
│                          │  │    GOP · keyint_min · Résolution│
│                          │  │    · fps · -re · boucle         │
│                          │  │  [Enregistrer]  [Annuler]       │
└──────────────────────────┘  └────────────────────────────────┘
```

Les **modèles** ne sont qu'un pré-remplissage des champs `Edit*` : aucune règle n'est dupliquée, le domaine valide toujours (E1–E8) et l'utilisateur reste libre de tout modifier. Modèles proposés : *Twitch 1080p60 (NVENC)*, *Twitch 1080p60 (libx264)*, *YouTube 1440p60 (NVENC)*, *Léger 720p30 (libx264)* — deux familles pour que la machine sans NVENC ait toujours une réponse.

État vide : « Aucun profil d'encodage. Créez-en un pour décrire comment votre vidéo sera encodée. » + `[Nouveau]`.

### 4.6 Channels

Même structure. La carte d'édition conserve `PasswordBox` + la phrase de réassurance existante. La liste affiche `Nom · Plateforme · URL · Clé configurée / Aucune clé` (déjà en place) ; l'absence de clé passe en `Caution` — c'est une anomalie réelle.

État vide : « Aucun channel. Créez-en un pour savoir où diffuser. » + `[Nouveau]`.

### 4.7 Confirmation de suppression (A5)

`ContentDialog` **nommant l'objet**, ouvert depuis le code-behind de la page (le `XamlRoot` est un type WinUI : il ne peut pas entrer dans `Nagare.ViewModels`), qui n'appelle `DeleteCommand` qu'après un `Primary`.

> **Supprimer « Twitch 1080p60 » ?**
> Ce profil d'encodage sera définitivement supprimé.
> `[Supprimer]` (destructive) `[Annuler]` (bouton par défaut)

## 5. Micro-copie

**Raisons de blocage — les 10, aucune muette (A3).**

| `StartBlockReason` | Phrase | Emplacement |
|---|---|---|
| `NotChecked` | Vérification en cours… | à côté de `Démarrer` |
| `FfmpegMissing` | ffmpeg est introuvable. Renseignez son chemin dans la configuration de l'application, ou ajoutez ffmpeg au PATH. | InfoBar, carte Lancement |
| `FfprobeMissing` | ffprobe est introuvable : la validation des fichiers vidéo est impossible. | InfoBar, carte Lancement |
| `NvencUnavailable` | Le profil sélectionné exige NVENC, indisponible sur cette machine. Choisissez un profil libx264. | InfoBar, carte Lancement |
| `SessionAlreadyActive` | Une diffusion est déjà en cours. | à côté de `Démarrer` |
| `ProfileNotSelected` | Choisissez un profil d'encodage. | à côté de `Démarrer` |
| `ChannelNotSelected` | Choisissez un channel de diffusion. | à côté de `Démarrer` |
| `InputFileNotSelected` | Choisissez la vidéo à diffuser. | à côté de `Démarrer` |
| `InputFileNotFound` | Fichier introuvable. | carte Source, sous le chemin |
| `InputFileUnreadable` | Fichier illisible par ffprobe. | carte Source, sous le chemin |

Les trois messages d'environnement et les deux messages média sont **repris à l'identique** du `DashboardViewModel` actuel : ils sont déjà justes.

**Titres de carte :** Source · Diffusion · Lancement · Santé · Journal ffmpeg.
**Checklist :** Environnement · Fichier · Profil · Channel.
**Zone de dépôt :** « Déposez une vidéo ici, ou » + `Choisir un fichier…`.
**Journal :** « Journal ffmpeg (500 dernières lignes) » · `Copier`. *(Le défilement automatique est livré, mais sans bascule : il passe par `ItemsUpdatingScrollMode="KeepLastItemInView"`, coalescé par le panneau. Un interrupteur viendra le jour où quelqu'un aura besoin de le figer.)*
**Bilan d'arrêt :** « Diffusion arrêtée — 0 image perdue, 0 reconnexion. » / « Diffusion interrompue — *{LastError}* ».
**Statuts** (inchangés, ils viennent de `LabelOf`) : Aucune session · Démarrage · En cours · Reconnexion · Arrêtée · Échec. Le badge de santé affiche **« En direct »** quand `Running` — le mot du streamer (Mental Model) — et conserve `StatusLabel` pour les autres états.

## 6. Traçabilité — décision → loi

| Décision | Loi | Corrige |
|---|---|---|
| 4 cartes ceinturées (Source · Diffusion · Lancement · Santé · Journal) | Common Region, Proximity, Uniform Connectedness | A2 |
| Checklist de lancement 4 items + phrase pour les 10 raisons | Zeigarnik, Goal-Gradient | A3 |
| `Démarrer`/`Arrêter` exclusifs, même emplacement, cible ≥ 40 px, fin de parcours | Fitts | A11 |
| Badge de santé = icône + forme + mot + couleur | Accessibilité, Von Restorff | A8 |
| 5 stats en tuiles étiquetées | Miller, Chunking | A9 |
| Accent réservé au bouton primaire ; rouge réservé à `Failed`/`speed < 1`/reconnexion | Von Restorff | A2, A9 |
| Modèles de profil + section « Avancé » repliée | Hick, Choice Overload, Tesler | A7 |
| Champs groupés Vidéo · Débit · Audio · Avancé (≤ 5 par bloc) | Miller | A7 |
| `IsBusy` → `ProgressRing` ; « Démarrage » affiché avant la 1ʳᵉ stat ffmpeg | Doherty | A4 |
| `ContentDialog` nommant l'objet supprimé | Postel (strict en sortie) | A5 |
| États vides porteurs du CTA suivant | Paradox of the Active User | A6 |
| Messages critiques dans la carte concernée, pas en bannière de page | Selective Attention | A14 |
| Drag & drop d'un `.mp4` sur la carte Source | Postel (libéral en entrée) | — |
| Icônes de rail, Mica, titre custom, contrôles Fluent uniquement | Jakob, Aesthetic-Usability | A1 |
| Aucune modale ni InfoBar bloquante pendant `Starting/Running/Reconnecting` | Flow | — |
| Bilan de session à l'arrêt | Peak-End Rule | A13 |
| Carte Santé masquée hors session ; aucun graphique ajouté | Occam, Pareto | — |
| Vocabulaire « en direct », « clé de stream », « channel » ; jamais « preflight »/« snapshot » | Mental Model | — |
| Aperçu de commande et journal brut conservés, mais repliés par défaut | Tesler | — |

## 7. Accessibilité (§9 du brief)

- `AutomationProperties.Name` sur **tout** bouton icône (`Copier`) et sur la zone de dépôt, les listes et les `ProgressRing`. `Nouveau` / `Modifier` / `Supprimer` sont livrés en boutons TEXTE : leur libellé est déjà leur nom accessible, un `AutomationProperties.Name` par-dessus ne ferait que le dupliquer.
- `KeyboardAccelerator` : `Ctrl+O` choisir un fichier · `F5` démarrer · `Maj+F5` arrêter · `Ctrl+N` nouveau (Profils / Channels) · `Suppr` supprimer (avec dialogue). Parcours complet au clavier : rail → cartes → `Démarrer`.
- Statut **jamais par la couleur seule** : icône + mot + couleur (A8).
- Cibles ≥ 32 px ; boutons primaires 40 px.
- Contraste AA garanti par l'usage exclusif des brushes système (clair / sombre / contraste élevé suivent le thème). Vérification à l'écran dans les trois thèmes avant clôture.
- `ToolTip` sur les réglages experts (`GOP`, `keyint_min`, `-re`, boucle infinie) — l'explication qui évite d'ouvrir une doc.

## 8. Impact code (périmètre, sans toucher Domaine / Application / Infrastructure)

| Fichier | Nature |
|---|---|
| `src/Nagare.WinApp/Styles/*.xaml` | créé : styles de carte, tuile de stat, badge, mergés dans `App.xaml` |
| `src/Nagare.WinApp/MainWindow.xaml{,.cs}` | modifié : icônes, Mica, titre custom |
| `src/Nagare.WinApp/Views/DashboardPage.xaml{,.cs}` | modifié : cartes, checklist, tuiles, badge, drop, journal |
| `src/Nagare.WinApp/Views/{Profiles,Channels}Page.xaml{,.cs}` | modifié : groupes, Avancé replié, états vides, `ContentDialog` |
| `src/Nagare.WinApp/Converters/ValueConverters.cs` | modifié : converters d'état (visibilité inverse, sévérité, glyphe de statut) |
| `src/Nagare.ViewModels/DashboardViewModel.cs` | modifié : phrases des 10 raisons + 4 booléens de checklist + bilan de session — **traduction du verdict, aucune règle** |
| `src/Nagare.ViewModels/ProfilesViewModel.cs` | modifié : `SelectedTemplate` (pré-remplissage des champs `Edit*`) |
| `tests/Nagare.UnitTests/ViewModels/**` | ajouté : couverture des nouvelles propriétés dérivées |

`Nagare.ViewModels` reste **sans dépendance WinUI** : les converters et le `ContentDialog` restent dans `Nagare.WinApp`.

## 9. Escalations

1. **Durée de diffusion — bloquant pour le Peak-End Rule.** `SessionSnapshot` ne porte **ni heure de début ni durée** (`docs/design/prompt-ux-ui.md` §8, confirmé dans le code). Un chronomètre local serait faux à la réhydratation. → champ `StartedAt` à ajouter dans `SessionSnapshot` (Application). **En attendant**, le bilan de session est livré **sans durée** : drops, reconnexions, raison de l'arrêt.
2. **Channel et fichier d'une session réhydratée.** `SessionSnapshot` ne porte pas davantage le channel ni le fichier de la diffusion. Quitter le tableau de bord et y revenir pendant que ffmpeg tourne reconstruit un ViewModel dont la sélection est vide : la carte *Santé* ne peut alors plus répondre à « vers quoi ? avec quel fichier ? ». **En attendant**, ces deux lignes sont capturées au démarrage et simplement **repliées** quand elles sont inconnues — mieux vaut ne rien dire qu'afficher un blanc. → même correctif que le point 1, côté Application.
3. **Champ « durée » optionnel au lancement** (préparation de l'itération 2, brief §5) : non implémenté ici — il demande une commande Application. Signalé, non contourné.

## 10. Critères d'acceptation — état visé

- [x] App vierge → « Démarrer » sans documentation (états vides + CTA de navigation)
- [x] `Démarrer` désactivé ⇒ raison affichée, sans clic — **les 10 raisons**
- [x] 2 s de coup d'œil répondent à : diffuse ? sain ? vers quoi ? avec quel fichier ? *(sauf session réhydratée — §9.2)*
- [x] Toute suppression confirmée **en nommant** l'objet
- [x] Aucune action > 400 ms sans retour (`IsBusy`)
- [x] Zéro couleur en dur ; **clair / sombre / contraste élevé restent à valider à l'écran**
- [x] Diffusion complète au clavier seul
- [x] Flux de logs soutenu : UI fluide (ring buffer 500 + virtualisation + throttle 1/s **intacts**)
- [x] `Nagare.ViewModels` sans type WinUI ; `dotnet build` et `dotnet test` verts
