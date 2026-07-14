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

Le plan (`docs/plan-winui3-migration.md`) est découpé en **phases**, chacune avec
son **critère de sortie** explicite. Une phase = une branche = une PR.
On ne démarre pas une phase avant que la précédente soit *Done*, sauf phases
explicitement marquées parallélisables.

## Décisions d'architecture

Toute décision structurante donne lieu à un **ADR** dans `docs/adr/`. Une décision
qui en annule une autre marque l'ancienne **⛔ REMPLACÉE** (on ne supprime jamais
un ADR : l'historique des décisions a de la valeur).

## Configuration locale : User Secrets

Le dépôt est **public**. Aucun chemin machine, aucune URL privée, **aucune clé** ne doit
y être committé — pas même dans `appsettings.Development.json`.

La configuration propre à votre poste vit dans les **User Secrets**
(`UserSecretsId = nagare-winapp-local`, stockés dans
`%APPDATA%\Microsoft\UserSecrets\`, hors du dépôt) :

```bash
cd src/Nagare.WinApp
dotnet user-secrets set "Nagare:Ffmpeg:ExecutablePath" "C:\chemin\vers\ffmpeg.exe"
dotnet user-secrets set "Nagare:Ffmpeg:FfprobePath"    "C:\chemin\vers\ffprobe.exe"
dotnet user-secrets list
```

Laisser ces valeurs **vides** résout `ffmpeg`/`ffprobe` depuis le `PATH` (`FfmpegOptions`).

### ⚠️ Frontière à ne pas franchir

**Les User Secrets ne sont PAS chiffrés** — c'est du JSON en clair sur le disque.
Microsoft les documente comme un confort de développement, pas comme une protection.

| Usage | Mécanisme |
|---|---|
| **Configurer** : chemins ffmpeg, URL RTMP de test, clé de test pour valider une vraie diffusion | **User Secrets** |
| **Stocker** : les clés des channels créés par l'utilisateur dans l'app | **Data Protection / DPAPI**, chiffrées au repos dans `%APPDATA%\Nagare` (ADR-0005) |

Ne **jamais** déplacer le stockage des clés de channels vers les User Secrets : ce serait
remplacer du chiffré par du clair, en violation de la spec (« clé chiffrée au repos »).

## Règle anti-hallucination

Lire le code avant de l'écrire. Ne jamais inventer une API, une signature ou un
package. En cas de doute : poser la question, ne pas supposer.
