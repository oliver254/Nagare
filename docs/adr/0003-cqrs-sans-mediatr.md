# ADR-0003 — CQRS sans MediatR : handlers maison + DI directe

Statut : ⛔ **REMPLACÉ par l'ADR-0007** (BrilliantMediator) — 2026-07-06.
Motif : BrilliantMediator est un mediator source-generated sans réflexion —
l'échappatoire que la spec autorisait — et le pivot desktop annule l'objection
de scoping. Conservé pour l'historique.

## Contexte

La spec impose CQRS mais exclut MediatR (« pas de magie ») ; MediatR est par
ailleurs devenu payant en usage commercial. Le besoin réel : séparer
lectures/écritures avec des contrats d'I/O explicites, pour ~6 commands et
~7 queries en itération 1.

## Décision

Deux interfaces maison dans `Nagare.Application.Abstractions` :
`ICommandHandler<TCommand[, TResult]>` et `IQueryHandler<TQuery, TResult>`
(méthode unique `HandleAsync(…, CancellationToken)`). Enregistrement DI
**explicite un par un** dans `AddNagareApplication()` ; les composants Blazor
injectent le handler typé dont ils ont besoin. Pas de dispatcher central, pas
de scan d'assembly, pas de pipeline behaviors.

## Conséquences

- Navigation triviale (F12 mène au handler), zéro dépendance externe, zéro
  réflexion au démarrage.
- Les préoccupations transverses (logging, validation) se traitent par
  décorateurs enregistrés explicitement — seulement si un besoin réel apparaît.
- Coût assumé : une ligne d'enregistrement DI par handler ; un oubli se détecte
  à la première résolution (test smoke DI possible).

## Alternatives écartées

- **MediatR** : exclu par la spec ; indirection et licence sans contrepartie ici.
- **Mediator source-generated (ex. martinothamar/Mediator)** : envisageable
  plus tard si le nombre de handlers explose ; prématuré pour ~13 use cases.
