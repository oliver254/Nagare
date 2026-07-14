# ADR-0005 — Représentation et protection de la clé de stream

Statut : accepté — 2026-07-06

## Contexte

La clé de stream (Twitch/YouTube/RTMP) équivaut à un credential : quiconque la
détient peut diffuser sur la chaîne. Exigences spec : chiffrée au repos
(Data Protection / DPAPI), jamais en clair dans les logs, l'UI ou les messages
d'erreur. Contrainte : ffmpeg exige la clé en clair dans son URL de sortie, et
**répète cette URL dans ses messages d'erreur stderr**.

## Décision

- **Domain** : VO `ProtectedStreamKey` ne portant que le chiffré (`CipherText`) ;
  `ToString()` retourne `****` (un log accidentel est inoffensif). Aucun membre
  n'expose le clair.
- **Frontière** : port `IStreamKeyProtector { Protect, Unprotect }` défini en
  Application, implémenté en Infrastructure par **ASP.NET Data Protection**
  (purpose `"Nagare.StreamKey.v1"`, keyring dans `%APPDATA%\Nagare\keys`,
  protégé **DPAPI** — choix OS-spécifique isolé derrière le port, conformément
  à la spec).
- **Cycle de vie du clair** : (1) saisie UI → `SaveChannelCommand` →
  `Protect` dans le handler, clair oublié ; (2) `Unprotect` appelé uniquement
  par `FfmpegCommandBuilder` (Infrastructure) au lancement. Entre les deux, la
  clé n'existe que chiffrée.
- **Sorties** : `FfmpegCommand` porte `MaskedCommandLine` (seule version
  affichable/loggable, `ToString()` la retourne) et `Secrets` ; un
  `StreamKeyScrubber` remplace la clé par `****` dans **chaque ligne** stderr
  avant tout buffer/log/message d'erreur. Les DTOs UI n'exposent jamais la clé
  (seulement `CleConfiguree`), champ de saisie en `type=password`, non réaffiché.

## Conséquences

- Chiffré au repos lié au compte Windows (DPAPI) ; JSON de persistance sans
  secret exploitable. Portage non-Windows = nouvelle implémentation du port.
- Limitation assumée : la clé apparaît dans la ligne de commande du process
  ffmpeg, visible localement (Process Explorer). Acceptable pour une app locale
  mono-utilisateur ; ffmpeg n'offre pas d'alternative simple pour une URL de
  sortie RTMP.
- Clé perdue si le keyring/profil Windows est perdu → l'utilisateur ressaisit
  la clé (récupérable depuis la plateforme) : trivial, pas de mitigation requise.

## Alternatives écartées

- **Clé en clair dans le JSON** : credential exploitable par tout process du
  compte ; contraire à la spec.
- **DPAPI brut (`ProtectedData`)** : équivalent ici, mais Data Protection
  apporte purposes et rotation, et s'intègre nativement à l'hôte ASP.NET Core.
- **Windows Credential Manager** : interop supplémentaire sans gain réel pour
  un secret unique déjà couvert par Data Protection.
