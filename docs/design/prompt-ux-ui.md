# Prompt — Conception UX/UI de Nagare (WinUI 3)

> À donner tel quel à l'agent (ou à la session) qui prend en charge l'UI.
> Rédigé le 2026-07-14, à partir du code réel (`src/Nagare.WinApp`, `src/Nagare.ViewModels`).
> Le livrable de conception attendu est `docs/design/ux-ui.md`.

---

## 0. Rôle

Tu es **designer produit + intégrateur XAML**. Tu conçois l'expérience *puis* tu
l'implémentes en WinUI 3. Tu ne touches ni au domaine, ni à l'Application, ni à
l'Infrastructure : ils sont figés et corrects.

Ta contrainte de méthode : **appliquer les *Laws of UX*** (§6). Pas les citer — les
appliquer, et pouvoir prouver chaque décision par une loi.

## 1. Le produit en une page

**Nagare** diffuse un fichier vidéo local **en boucle** vers Twitch / YouTube / un RTMP
custom, en pilotant **ffmpeg**. Application Windows locale, mono-utilisateur, sans compte,
sans réseau autre que le RTMP sortant.

L'utilisateur type : un streamer qui veut mettre une vidéo en boucle sur sa chaîne sans
lancer OBS. Il connaît le vocabulaire (bitrate, clé de stream, NVENC), il ne veut pas
apprendre une nouvelle grammaire.

Le geste central, celui qui paie tout le reste :

> choisir un fichier → choisir un profil d'encodage → choisir un channel → **Démarrer** →
> surveiller (en direct, sain ?) → **Arrêter**.

Trois concepts, et rien d'autre :

| Concept | C'est quoi | Où |
|---|---|---|
| **Profil** (`StreamProfile`) | Un réglage d'encodage réutilisable : codec, preset, rate control, bitrate/maxrate/bufsize, GOP, keyint, résolution, fps, audio, options d'entrée (`-re`, boucle infinie) | Page Profils |
| **Channel** (`Channel`) | Une destination : nom, plateforme (Twitch / YouTube / RTMP custom), URL de base, **clé de stream chiffrée** | Page Channels |
| **Session** (`StreamSession`) | Une diffusion en cours. **Une seule à la fois.** États : `Starting` → `Running` → (`Reconnecting`) → `Stopped` / `Failed` | Page Tableau de bord |

Lecture obligatoire avant de dessiner quoi que ce soit : `docs/SPEC.md`,
`docs/domain-model.md` (machine à états), `docs/adr/0006-winui3-natif.md`,
`docs/plan-winui3-migration.md` (§5 et §6 : les garde-fous temps réel et sécurité).

## 2. Le terrain technique

- **WinUI 3** (Windows App SDK 2.2.0), XAML natif Fluent, `net10.0-windows10.0.19041.0`.
- **Non empaqueté** (`WindowsPackageType=None`) : un simple `.exe`, pas d'identité MSIX.
- **MVVM** CommunityToolkit (`[ObservableProperty]`, `[RelayCommand]`), CQRS via
  BrilliantMediator.
- `TreatWarningsAsErrors` est actif : un avertissement casse le build.

Où vit quoi :

```
src/Nagare.WinApp/        XAML : App.xaml, MainWindow.xaml, Views/{Dashboard,Profiles,Channels}Page.xaml
                          Converters/ValueConverters.cs, Services/{FilePickerService,UiDispatcher,MainWindowContext}
src/Nagare.ViewModels/    DashboardViewModel, ProfilesViewModel, ChannelsViewModel, ViewModelBase
                          → projet net10.0 SANS aucune dépendance WinUI, couvert par des tests unitaires
tests/Nagare.UnitTests/ViewModels/   les tests desdits ViewModels — ils doivent rester verts
```

## 3. Ce qui existe aujourd'hui (audit factuel, constaté dans le code)

L'application **fonctionne** : les trois pages sont câblées, le temps réel marche, la clé
est protégée. Ce qui manque est **exclusivement** de l'UX et de l'UI. Constats :

1. **Shell** : `NavigationView` à trois entrées (Tableau de bord / Profils / Channels),
   **sans icônes**, sans backdrop (pas de Mica), barre de titre système par défaut.
2. **Dashboard** : un mur plat de contrôles empilés dans une `Grid` — deux `InfoBar`, deux
   `ComboBox`, un bouton « Choisir un fichier… », le chemin, le résumé média, l'aperçu de
   commande, Démarrer/Arrêter, cinq statistiques nues (`fps`, `kbits/s`, `x`, `drops`,
   `reconnexions`) alignées côte à côte, puis la console de logs. **Aucun regroupement
   visuel, aucune hiérarchie.**
3. **Bouton « Démarrer » désactivé sans dire pourquoi.** Le verdict vient de
   `GetStartPreflightQuery` ; quand la raison est « rien n'est sélectionné », le ViewModel
   renvoie délibérément `null` comme message. L'utilisateur voit donc un bouton mort et
   aucune explication. **C'est le pire trou de l'app.**
4. **Aucun retour de progression** : `ViewModelBase.IsBusy` existe et **n'est bindé nulle
   part**. Or la sonde d'environnement lance trois process, `ffprobe` en lance un : on est
   très au-dessus de 400 ms sans le moindre signal.
5. **Suppression sans confirmation** : `Supprimer` sur un profil ou un channel exécute
   immédiatement. Aucun `ContentDialog` dans tout le projet.
6. **Aucun état vide.** Au premier lancement : zéro profil, zéro channel, listes vides,
   aucune indication de la marche à suivre.
7. **Éditeur de profil** : ~15 réglages en colonne plate (jusqu'au `keyint_min`), sans
   regroupement ni progressive disclosure. Aucun preset prêt à l'emploi.
8. **Accessibilité inexistante** : zéro `AutomationProperties`, zéro `KeyboardAccelerator`,
   zéro `ToolTip` dans le XAML.
9. **Journal** : correct techniquement (ring buffer 500 + `ListView` virtualisée) mais brut —
   pas d'auto-scroll, pas de copie, pas de filtre, pas de mise en évidence des erreurs.

Ta première tâche est de **rejouer cet audit toi-même** et de le compléter : chaque constat
doit nommer la loi violée (§6).

## 4. Mission — trois livrables, dans cet ordre

### Livrable 1 — `docs/design/ux-ui.md` (conception) — **à faire valider avant tout XAML**

- Audit UX de l'existant : constats → loi violée → conséquence pour l'utilisateur.
- Principes directeurs (5 max), et ce qu'ils excluent.
- **Tokens** : échelle d'espacement, rampe typographique, couleurs **sémantiques**
  (succès / alerte / erreur / neutre) exprimées en `ThemeResource`, jamais en dur.
- Inventaire écran par écran : wireframes (ASCII ou mermaid suffisent) + **tous les états** :
  vide · chargement · nominal · erreur · **en direct** · **reconnexion** · échec.
- Micro-copie française complète (titres, libellés, messages d'erreur, états vides, CTA).
- **Table de traçabilité : décision → loi.** Une ligne par décision de design.
- Plan d'accessibilité (§9).

### Livrable 2 — Implémentation WinUI 3

Ressources de style (`Styles/*.xaml` mergés dans `App.xaml`), refonte du shell et des trois
pages, nouveaux converters si nécessaire, `ContentDialog` de confirmation, états vides,
états occupés. Ordre conseillé : **shell → dashboard → channels → profils** (le dashboard
porte 80 % de la valeur).

### Livrable 3 — Vérification

`dotnet build` **et** `dotnet test` verts, zéro nouvel avertissement, plus un passage réel à
l'écran (lancer l'app, parcourir les états). Aucune régression sur les tests de ViewModels.

## 5. Décisions déjà prises — ne pas les rouvrir

- WinUI 3 XAML natif. **Pas de MudBlazor, pas de WebView, pas de Blazor** (ADR-0006 a tranché).
- Une seule session active à la fois. Le stream ne survit pas à la fermeture de la fenêtre.
- La clé de stream **n'est jamais réaffichée**, nulle part.
- Trois pages ; une 4ᵉ (**Planifications**) arrive en itération 2 — voir
  `docs/product/stream-scheduling.md`. **Laisse-lui la place** dans la navigation, et prévois
  un champ « durée » optionnel sur le lancement.

## 6. La contrainte centrale : les *Laws of UX*

Applique-les. Chaque loi ci-dessous a une traduction **concrète** attendue dans Nagare.

| Loi | Ce qu'elle exige **ici** |
|---|---|
| **Jakob's Law** | Nagare doit se comporter comme une app Windows 11 (Fluent : `NavigationView`, Mica, `InfoBar`, `ContentDialog`, thème système suivi) **et** emprunter les repères d'OBS (clé de stream, bitrate, « en direct », console de logs). N'invente aucune navigation maison. |
| **Fitts's Law** | Démarrer / Arrêter : cibles larges, au bout du parcours de configuration, là où l'œil finit. **Jamais** une action destructive adjacente à l'action primaire. Aucune cible cliquable sous ~32 px. |
| **Hick's Law** + **Choice Overload** | L'éditeur de profil impose 15 décisions. Réduis le choix visible : **presets prêts à l'emploi** (« Twitch 1080p60 », « YouTube 1440p60 »…), section « Avancé » repliée. Personne ne devrait avoir à choisir un `keyint_min` pour diffuser. |
| **Miller's Law** + **Chunking** | Regroupe les 15 champs en 3–4 blocs de ≤ 5 (Vidéo · Débit · Audio · Entrée). Idem pour les 5 statistiques du dashboard. |
| **Common Region** + **Proximity** + **Uniform Connectedness** | Le dashboard est un mur plat : découpe-le en **cartes ceinturées** — *Source* (fichier + média), *Diffusion* (profil + channel + commande), *Santé* (statut + stats), *Journal*. |
| **Von Restorff** | **Un seul** élément accentué par écran : le bouton primaire. Le rouge est réservé à l'anomalie **réelle** (`speed < 1.0x`, échec, reconnexion) — jamais décoratif. Si tout est saillant, rien ne l'est. |
| **Zeigarnik** + **Goal-Gradient** | Une **checklist de lancement** visible : Environnement ✓ · Fichier ✓ · Profil ✓ · Channel ✓. Elle comble le trou n° 3 de l'audit : un bouton désactivé doit **toujours** dire ce qui manque, sans clic. |
| **Doherty Threshold (400 ms)** | Binde `IsBusy`. `ProgressRing` / squelette pendant la sonde d'environnement et `ffprobe`. Au clic sur Démarrer : retour **immédiat** (« Démarrage… »), avant même la première stat de ffmpeg. |
| **Peak-End Rule** | Le **pic**, c'est le lancement : il doit inspirer confiance (préflight vert, commande visible, bascule « En direct » instantanée). La **fin**, c'est l'arrêt : donne un **bilan de session** (drops, reconnexions, raison de l'arrêt) au lieu de retomber dans le vide. Une session qui échoue ne doit pas être le dernier goût laissé. |
| **Tesler's Law** | La complexité de ffmpeg est **irréductible** : c'est l'app qui l'absorbe (valeurs par défaut, presets, invariants du domaine, commande générée), pas l'utilisateur. Mais l'expert garde son accès : l'aperçu de commande et le journal brut **restent**. |
| **Postel's Law** | Libéral en entrée : **drag & drop** d'un `.mp4` sur le dashboard, collage d'un chemin, tolérance sur les espaces. Strict en sortie : jamais d'état invalide envoyé au domaine. |
| **Paradox of the Active User** | Personne ne lira de documentation. **Les états vides sont la documentation** : « Aucun channel — créez-en un pour diffuser » + CTA direct. Le premier lancement (0 profil, 0 channel) est le parcours à soigner **en priorité**. |
| **Selective Attention** | Les messages critiques (« ffmpeg introuvable ») doivent tomber **dans le flux de la tâche**, pas dans une bannière haute qu'on apprend à ignorer. |
| **Aesthetic-Usability Effect** | Échelle d'espacement, rampe typo, iconographie cohérente : le soin visuel achète de la tolérance sur les frictions résiduelles. Ce n'est pas du vernis. |
| **Flow** | Pendant une diffusion : **aucune modale, aucun vol de focus, aucune InfoBar bloquante**. Une reconnexion se signale sans interrompre. |
| **Occam's Razor** + **Pareto** | 80 % de l'usage = démarrer / surveiller / arrêter. Tout ce qui ne sert pas ces trois gestes recule d'un plan. Supprime jusqu'à ce que ça casse. |
| **Mental Model** | Le vocabulaire du streamer (channel, clé de stream, bitrate, en direct), **jamais** celui du code (preflight, DTO, agrégat, snapshot). |

**Règle anti-décoration.** Une loi citée sans décision concrète en face est du remplissage.
Inversement, une décision de design sans loi derrière est une préférence personnelle : dis-le
franchement plutôt que de l'habiller. La table de traçabilité doit tenir dans les deux sens.

## 7. Non négociables

1. **La clé de stream.** Saisie en `PasswordBox`, jamais réaffichée, jamais loggée, jamais
   dans un tooltip ni dans le presse-papiers. Le `ChannelDto` **ne la porte pas** (il dit
   seulement `KeyConfigured`) : même un « •••• + 4 derniers caractères » est techniquement
   impossible — ne le propose pas. Un bouton « Copier la commande » ne peut copier que la
   version **masquée** (ADR-0005, SPEC §4).
2. **Le temps réel ne doit pas geler l'UI.** Ring buffer 500 lignes + `ListView` virtualisée +
   throttle des stats à 1/s : ce sont les garde-fous du plan §5, **non négociables**. Tout
   ajout (auto-scroll, surbrillance de ligne, animation) doit être **coalescé** sur le thread
   UI, sinon le débit de ffmpeg tue la fenêtre.
3. **`Nagare.ViewModels` reste sans dépendance WinUI.** Aucun type XAML (`Brush`,
   `Visibility`, `Color`) ne doit y entrer : c'est ce qui le rend testable. Les converters
   restent dans `Nagare.WinApp/Converters`.
4. **Les règles vivent dans le domaine.** `DomainException` (invariants E1–E8) et
   `GetStartPreflightQuery` décident ; l'UI **traduit** le verdict, elle ne le rejoue pas. Ne
   réécris aucune validation en XAML ni dans un ViewModel.
5. **Pas d'identité MSIX** (app non empaquetée) : **pas de toast Windows**, pas d'icône de
   notification système. Ne conçois rien qui en dépende.
6. **UI en français, code en anglais** (identifiants, fichiers, `x:Name`).
7. `x:Bind` compilé avec `Mode` explicite ; brushes via `ThemeResource` ; **aucune couleur en
   dur**.

## 8. Trou connu — à escalader, pas à contourner

Le dashboard ne peut **pas** afficher « en direct depuis 00:42:17 » : `SessionSnapshot` ne
porte **ni heure de début, ni durée**. Et un chronomètre local serait faux — le ViewModel se
réhydrate sur une session **déjà en cours** et n'a aucun moyen de savoir quand elle a démarré.

Si ton design en a besoin (et le Peak-End Rule plaide pour), **escalade** : c'est un champ à
ajouter dans `SessionSnapshot` (Application), pas une bidouille de présentation.

## 9. Accessibilité et thèmes

- Thème clair, sombre **et contraste élevé** corrects — testés, pas supposés.
- Contraste texte AA (4.5:1), cibles ≥ 32 px.
- `AutomationProperties.Name` sur tout bouton icône ; ordre de tabulation cohérent ; visuels
  de focus visibles.
- Parcours complet au clavier : lancer et arrêter une diffusion sans souris.
- Le statut ne doit **jamais** reposer sur la seule couleur (la pastille de santé actuelle est
  une `Ellipse` colorée : ajoute une forme, une icône ou un texte).

## 10. Critères d'acceptation (vérifiables, pas cosmétiques)

- [ ] Une app vierge (0 profil, 0 channel) conduit l'utilisateur jusqu'à « Démarrer » **sans
      documentation** : chaque écran vide propose l'action suivante.
- [ ] À **tout** instant où « Démarrer » est désactivé, l'écran dit **pourquoi**, sans clic.
- [ ] Deux secondes de coup d'œil sur le dashboard en diffusion répondent à : *ça diffuse ?
      c'est sain ? vers quel channel ? avec quel fichier ?*
- [ ] Toute suppression demande confirmation en **nommant** l'objet supprimé.
- [ ] Aucune action ne dépasse 400 ms sans retour visuel.
- [ ] Zéro couleur en dur ; clair / sombre / contraste élevé validés à l'écran.
- [ ] Diffusion complète au clavier seul.
- [ ] Un flux de logs soutenu (plusieurs centaines de lignes/s) laisse l'UI fluide.
- [ ] `Nagare.ViewModels` ne référence aucun type WinUI ; **tous** les tests passent.
- [ ] Chaque décision du doc pointe une loi ; chaque loi citée pointe une décision.

## 11. Interdits

- Réécrire une règle métier dans l'UI.
- Réafficher la clé, même partiellement.
- Ajouter MudBlazor, un WebView, un framework de thèmes tiers, ou toute dépendance non
  justifiée.
- **Inventer** un contrôle ou une API : vérifie son existence dans le Windows App SDK 2.2.0
  installé avant de l'écrire. En cas de doute, demande.
- Sur-ingénierie : pas de refonte MVVM, pas de nouvelle couche, pas d'abstraction prématurée.
  Du XAML explicite et lisible.

## 12. Méthode

1. **Découverte** — lis les fichiers listés en §1 et §2 (docs *et* code) avant d'ouvrir un
   `.xaml`.
2. **Audit** — rejoue et complète le §3, chaque constat rattaché à une loi.
3. **Conception** — écris `docs/design/ux-ui.md`. **Arrête-toi là et fais valider.**
4. **Implémentation** — shell → dashboard → channels → profils.
5. **Vérification** — build, tests, passage réel à l'écran.

## 13. Format de sortie

```
## Résultat
[2-5 lignes]

## Fichiers
- chemin — créé|modifié : raison

## Escalations
- agent : raison   (ou : aucune)

## À valider
- question/risque  (ou : rien)
```
