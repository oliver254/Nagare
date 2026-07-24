# Nagare — Workflow de développement

> Conventions alignées sur celles déjà en vigueur sur BrilliantMediator.
> Langue du code : **anglais**. Langue des échanges et des docs : **français**.

## Branches

Une branche par incrément livrable, jamais de commit direct sur `main`.

```bash
git checkout -b feature/{numero}-{nom-court}
# ex. feature/1-unit-tests, feature/2-winui3-migration
```

`main` reste toujours **verte** (build + tests). Une branche fusionne uniquement
quand sa Definition of Done est satisfaite.

## Commits

Format **Conventional Commits** :

```
{type}({scope}): {description}
```

- **Types** : `feat`, `fix`, `test`, `refactor`, `docs`, `chore`
- **Scopes** : `domain`, `application`, `infrastructure`, `winapp`, `tests`, `docs`
- Un commit = **un changement cohérent**. Pas de commit fourre-tout.
- Build et tests verts **avant** chaque commit : `dotnet build && dotnet test`.

Exemples :
```
test(infrastructure): golden test du FfmpegCommandBuilder
feat(winapp): fenêtre WinUI 3 et NavigationView
fix(domain): rejeter bufsize < bitrate (invariant E4)
```

## Definition of Done

Un incrément n'est « terminé » que si **tout** est vrai :

- [ ] `dotnet build Nagare.slnx` — **0 erreur, 0 avertissement**
- [ ] `dotnet test Nagare.slnx` — **tous verts**, et le nombre de tests est constaté (pas supposé)
- [ ] Tests écrits pour le comportement ajouté (pas seulement le chemin nominal : cas d'erreur inclus)
- [ ] Aucune clé de stream en clair nulle part (logs, UI, messages d'erreur)
- [ ] Audit `reviewer` passé (Clean Architecture, DDD, CQRS, conventions)
- [ ] Docs à jour si une décision d'architecture change (ADR) ou si le plan avance

## Découpage du travail

Le travail se découpe en **phases**, chacune avec son **critère de sortie**
explicite. Une phase = une branche = une PR. On ne démarre pas une phase avant que
la précédente soit *Done*, sauf phases explicitement marquées parallélisables.

Le plan de migration WinUI 3 (`docs/plan-winui3-migration.md`) a suivi ce découpage
et ses **7 phases sont closes** ; il reste la référence de la méthode et l'historique
des pièges rencontrés. Le chantier suivant est la **conception UX/UI**
(`docs/design/prompt-ux-ui.md`), qui impose une validation du document de conception
**avant** toute écriture de XAML.

## Décisions d'architecture

Toute décision structurante donne lieu à un **ADR** dans `docs/adr/`. Une décision
qui en annule une autre marque l'ancienne **⛔ REMPLACÉE** (on ne supprime jamais
un ADR : l'historique des décisions a de la valeur).

## Configuration locale : dans l'application

Le dépôt est **public**. Aucun chemin machine, aucune URL privée, **aucune clé** ne doit
y être committé — pas même dans `appsettings.Development.json`.

La configuration propre à votre poste — les chemins `ffmpeg`/`ffprobe` — se renseigne
**dans l'application**, écran **⚙ Paramètres** : *Détecter* ou *Parcourir…*, *Tester*,
*Enregistrer*. L'application écrit alors `%APPDATA%\Nagare\settings.json`, hors du
dépôt, et ce fichier **prime** sur l'`appsettings.json` livré avec l'exécutable
([ADR-0010](docs/adr/0010-configuration-ffmpeg-inscriptible.md)).

Il n'y a **plus de User Secrets** dans ce projet : ils exigeaient le SDK et le dossier
du projet, donc restaient hors de portée de quiconque utilise l'exe. Ne les
réintroduisez pas — une seule voie de configuration, la même pour le contributeur et
pour l'utilisateur.

Laisser les chemins **vides** résout `ffmpeg`/`ffprobe` depuis le `PATH`.

### 🔇 Rien de la machine ne sort dans le dépôt

Le dépôt, les commits, les **PR** et leurs **commentaires** sont **publics**. On n'y écrit
donc **jamais** :

- de **chemins absolus** (`C:\Users\…`) — ils exposent le nom d'utilisateur ;
- de **matériel** (modèle de GPU, de CPU), de **versions de pilotes** ;
- de version d'OS, de liste de SDK installés, de nom de machine ;
- toute autre empreinte de l'environnement local.

Ces informations n'apportent rien à un lecteur du projet et constituent une **fuite de
renseignement** sur le poste du mainteneur.

**Formulez en termes de projet, pas de machine** : « ffmpeg résolu depuis le `PATH` ou
un chemin configuré » plutôt que le chemin réel ; « encodage NVENC vérifié » plutôt que
le modèle exact de la carte.

### ⚠️ Frontière à ne pas franchir

**`settings.json` n'est PAS chiffré** — c'est du JSON en clair sous `%APPDATA%\Nagare`.
Il *configure*, il ne *stocke pas de secret*.

| Usage | Mécanisme |
|---|---|
| **Configurer** : chemins ffmpeg/ffprobe | **`settings.json`** (écran Paramètres, ADR-0010) |
| **Stocker** : les clés des channels créés par l'utilisateur dans l'app | **Data Protection / DPAPI**, chiffrées au repos dans `%APPDATA%\Nagare` (ADR-0005) |

Ne **jamais** écrire une clé de stream — même une clé de test — dans `settings.json`,
dans `appsettings.json` ni dans quoi que ce soit d'autre que le stockage chiffré : ce
serait remplacer du chiffré par du clair, en violation de la spec (« clé chiffrée au
repos »). Une clé de test se saisit comme les autres, dans l'écran **Channels**.

## Règle anti-hallucination

Lire le code avant de l'écrire. Ne jamais inventer une API, une signature ou un
package. En cas de doute : poser la question, ne pas supposer.
