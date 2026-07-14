# ADR-0001 — Cible `net10.0` / C# 14 (au lieu de .NET 9)

Statut : accepté — 2026-07-06

## Contexte

La spec initiale mentionnait .NET 9. Constat sur la machine de dev (addendum
SPEC du 2026-07-06) : SDKs installés = 10.0.301 et 10.0.100-rc.1, **aucun SDK ni
runtime .NET 9**. Cibler `net9.0` produirait une solution non exécutable
localement sans installation supplémentaire, pour une app locale mono-poste.

## Décision

Tous les projets ciblent **`net10.0`** avec **C# 14**.

> Mise à jour 2026-07-06 : `global.json` **supprimé** à la demande de
> l'utilisateur. Le build utilise donc le SDK 10.x le plus récent installé
> (actuellement `10.0.301`, la RC `10.0.100-rc.1` étant antérieure et non
> sélectionnée par défaut). À réintroduire si un besoin de reproductibilité
> stricte de la version de SDK apparaît (CI, poste partagé).

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
