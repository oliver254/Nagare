# ADR-0004 — Persistance des profils/cibles en JSON local (vs SQLite)

Statut : accepté — 2026-07-06

## Contexte

À persister : `StreamProfile` et `Channel` — quelques dizaines d'objets au
grand maximum, app locale mono-utilisateur, un seul writer, aucune requête
relationnelle, aucun besoin transactionnel inter-agrégats. Les sessions ne sont
pas persistées en itération 1. La spec laisse le choix « SQLite ou JSON local ».

## Décision

**Fichiers JSON** via `System.Text.Json` : `%APPDATA%\Nagare\profiles.json` et
`%APPDATA%\Nagare\targets.json`. Écriture atomique (fichier temporaire +
`File.Replace`) et sérialisation des accès par `SemaphoreSlim`, encapsulées
dans un `JsonFileStore` unique. Accès exclusivement derrière
`IStreamProfileRepository` / `IChannelRepository` (ports Application).

## Conséquences

- Zéro dépendance (pas de provider SQLite, pas d'ORM, pas de migrations) ;
  fichiers lisibles et sauvegardables à la main — utile pour un outil personnel.
- La clé de stream y figure **chiffrée** (`ProtectedStreamKey.CipherText`,
  ADR-0005) : un JSON lisible ne fuit rien.
- Repositories = charger tout / réécrire tout : trivial et suffisant à cette
  volumétrie.
- Réversibilité : les ports isolent le choix ; si l'historique de sessions ou
  des requêtes apparaissent (itérations monitoring), bascule vers SQLite sans
  toucher Domain/Application.

## Alternatives écartées

- **SQLite (+ EF Core ou Dapper)** : moteur, schéma et migrations pour deux
  collections minuscules — sur-ingénierie ici (YAGNI).
- **LiteDB** : dépendance tierce de plus, mêmes inconvénients, moins standard.
