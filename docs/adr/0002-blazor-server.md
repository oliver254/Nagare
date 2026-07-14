# ADR-0002 — Blazor Server pour la présentation (vs Avalonia)

Statut : accepté — 2026-07-06

## Contexte

Nagare est une app locale mono-utilisateur avec un dashboard temps réel
(statut de session, logs live ffmpeg). La spec demande explicitement un
dashboard **Blazor Server** en itération 1. Une UI desktop native (Avalonia,
WPF) serait l'alternative naturelle pour un outil local Windows.

## Décision

**Blazor Server**, hébergé sur `localhost` uniquement, lancé comme une console
app qui ouvre le navigateur. Pas d'authentification en itération 1 (surface =
boucle locale).

## Conséquences

- Le push temps réel (transitions de session, lignes de log) est natif : le
  circuit SignalR pousse les rendus, les composants s'abonnent à
  `ISessionMonitor` — pas de polling.
- Toute la logique reste côté serveur : les ports (`IFfmpegProcessRunner`,
  singletons applicatifs) s'injectent directement dans les composants.
- Contrainte assumée : l'app doit tourner pour que l'UI existe ; fermeture de
  l'app ⇒ kill propre du process ffmpeg (déjà exigé par la spec).
- Sécurité : bind `localhost` strict ; la clé de stream n'atteint jamais le
  navigateur (masquage côté serveur, DTOs sans clé — ADR-0005).

## Alternatives écartées

- **Avalonia / WPF** : pas de bénéfice pour un dashboard, écosystème de
  composants plus pauvre, hors spec ; compétence Blazor déjà en place.
- **Blazor WebAssembly + API** : deux processus et une frontière réseau pour
  une app mono-poste — sur-ingénierie.
