# ADR-0007 — CQRS via BrilliantMediator (remplace les handlers maison)

Statut : accepté — 2026-07-06. **Remplace l'ADR-0003.**

## Contexte

L'ADR-0003 retenait des interfaces `ICommandHandler`/`IQueryHandler` maison avec
injection directe, la spec excluant MediatR (« pas de magie », réflexion).

Deux faits nouveaux :

1. **BrilliantMediator** (github.com/Monbsoft/BrilliantMediator, MIT, publié sur
   nuget.org en 3.0.0) est un mediator **source-generated, sans réflexion** —
   c'est-à-dire exactement l'échappatoire que la spec autorisait explicitement
   (« mediator source-generated si besoin »). Il est développé par le
   propriétaire du projet : le risque d'abandon d'une dépendance tierce ne
   s'applique pas, et l'utiliser dans Nagare constitue un **dogfooding** utile.
2. Le **pivot vers WinUI 3** (ADR-0006) annule la principale objection technique.
   `Mediator` crée un `IServiceScope` par dispatch ; en Blazor Server cela
   entrait en conflit avec le scope-par-circuit. En desktop il n'y a pas de
   circuit : un scope par dispatch devient au contraire propre — un « unit of
   work » par action utilisateur.

## Décision

**BrilliantMediator 3.0.0 + BrilliantMediator.SourceGenerator 3.0.0.**

- Les commands/queries implémentent `ICommand`, `ICommand<TResponse>`, `IQuery<TResponse>`.
- Les handlers implémentent `ICommandHandler<…>` / `IQueryHandler<…>` (méthode `Handle`).
- Enregistrement : `AddBrilliantMediator().AddGeneratedHandlers().Build()` puis
  `UseBrilliantMediator()` — **auto-enregistrement à la compilation**, sans scan
  d'assembly ni réflexion.
- Les ViewModels injectent **un seul `IMediator`** et appellent `DispatchAsync` /
  `SendAsync`.
- Les **événements de domaine** (`SessionLaunched`, …) **restent collectés sur
  l'agrégat** et drainés par le `StreamSessionCoordinator`. Ils ne passent
  **pas** par `IEvent`/`PublishAsync` : on ne mélange pas événements de domaine
  et pub/sub applicatif.

## Conséquences

- Supprime le seul vrai défaut de l'ADR-0003 : l'enregistrement DI manuel,
  ligne par ligne, avec le risque d'oubli.
- Un ViewModel = une dépendance (`IMediator`) au lieu de N interfaces de handlers.
- **Coût assumé** : `Nagare.Application` dépend désormais de
  `BrilliantMediator.Abstractions`. C'est une dépendance sur des **interfaces de
  messagerie**, pas sur de l'infrastructure (même posture que MediatR). La
  version puriste — conserver nos interfaces et adapter en Infrastructure —
  coûterait une couche d'indirection pour un bénéfice théorique.
- On perd la navigation F12 directe vers le handler (indirection du dispatch) et
  les call sites explicites.
- Pas de pipeline behaviors dans la lib : logging/validation transverses se
  feront par décorateurs si un besoin réel apparaît (aucun aujourd'hui).

## Réserves techniques relevées (côté lib, pas côté Nagare)

Le chemin critique de `Mediator` construit sa clé de dispatch par interpolation
de `typeof(TCommand).FullName` **à chaque appel** : cela alloue une chaîne par
dispatch et, à la lettre, touche `typeof` dans le hot path — ce que les propres
règles de relecture de la lib qualifient de bloquant. À l'échelle de Nagare
(quelques dispatches par action utilisateur) l'impact est **nul**. Signalé pour
la lib, sans conséquence sur cette décision.

Deux défauts du paquet **`BrilliantMediator.SourceGenerator` 3.0.0**, découverts à
l'implémentation (2026-07-14) et contournés dans `Nagare.Application.csproj` :

1. **Le générateur n'est pas empaqueté comme analyseur** : sa DLL est publiée dans
   `lib/netstandard2.0/` au lieu de `analyzers/dotnet/cs/`. Roslyn ne la charge donc pas et
   `AddGeneratedHandlers()` n'est jamais émis. Le csproj de la lib doit ajouter
   `IncludeBuildOutput=false` + `<None Pack="true" PackagePath="analyzers/dotnet/cs" />`.
2. **Le code émis n'ouvre pas de contexte nullable** : il utilise l'annotation `?` (ici
   `IQuery<SessionSnapshot?>`) sans émettre `#nullable enable`, ce que le compilateur exige d'un
   fichier auto-généré (CS8669) — fatal sous `TreatWarningsAsErrors`.

Aucun des deux ne remet la décision en cause : l'auto-enregistrement fonctionne, les 13 handlers
sont bien générés. Les contournements sont documentés et localisés.

## Alternatives écartées

- **Handlers maison + injection directe** (ADR-0003) : reste valable, mais ne
  bénéficie pas de l'auto-enregistrement et prive la lib d'un dogfooding réel.
- **MediatR** : exclu par la spec (réflexion, licence).
- **Mediator (martinothamar)** : source-generated et bien plus adopté, mais
  BrilliantMediator est la lib du propriétaire du projet — à qualité fonctionnelle
  équivalente ici, le dogfooding tranche.
