# ADR-0001 — Cible `net10.0` / C# 14 (au lieu de .NET 9)

Statut : accepté — 2026-07-06

## Contexte

La spec initiale mentionnait .NET 9. Aucun SDK ni runtime .NET 9 n'était présent
sur l'environnement de développement, seulement du .NET 10 : cibler `net9.0`
aurait produit une solution non exécutable sans installation supplémentaire, pour
une application locale mono-poste.

## Décision

Tous les projets ciblent **`net10.0`** avec **C# 14**.

> Pas de `global.json` : le build utilise le SDK 10.x le plus récent installé.
> À réintroduire si un besoin de reproductibilité stricte de la version de SDK
> apparaît (CI, poste partagé).

## Conséquences

- Exécutable immédiatement sur la machine cible ; .NET 10 est LTS (support plus
  long que .NET 9 STS).
- Aucune API requise par la spec (Process, Data Protection, Blazor Server,
  System.Text.Json) n'est impactée par le changement de version.
- Les packages `Microsoft.Extensions.*` / `Microsoft.AspNetCore.*` s'alignent
  sur la bande 10.x.

## Alternatives écartées

- **Installer .NET 9** : STS en fin de vie rapprochée, aucun bénéfice, une
  dépendance machine de plus.
- **Multi-targeting net9.0+net10.0** : complexité sans consommateur pour la
  cible 9.
