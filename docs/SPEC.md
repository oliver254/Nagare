# Projet : Nagare — Restreamer FFmpeg pour Twitch / YouTube

> Spec produit fournie par le propriétaire du projet (source de vérité fonctionnelle).
> Addendum environnement en fin de document.

## Vision
Application .NET qui pilote FFmpeg pour diffuser un fichier vidéo local (en boucle)
vers Twitch, YouTube ou un RTMP custom, avec configuration fine de l'encodage et
monitoring temps réel.

## Contexte technique cible
- .NET (voir addendum : cible effective `net10.0`), C#, Blazor Server
  (app locale mono-utilisateur, lancée sur localhost).
- Clean Architecture + DDD + CQRS. Code explicite, pas de "magie" ni d'abstractions
  inutiles (pas de MediatR : DI directe / mediator source-generated si besoin).
- Windows d'abord (NVENC), mais isoler tout ce qui est OS-spécifique derrière des interfaces.

## Fonctionnalités
1. **Bibliothèque vidéo** : sélection d'un fichier local, validation (existence, format
   lisible par ffprobe, durée, résolution).
2. **Profils d'encodage** réutilisables et persistés :
   - Vidéo : codec (h264_nvenc / hevc_nvenc / libx264), preset, rate control (CBR/VBR),
     bitrate, maxrate, bufsize, GOP (-g) et keyint_min, résolution, fps.
   - Audio : codec (aac), bitrate, sample rate.
   - Options d'entrée : -re, -stream_loop -1 (boucle infinie togglable).
3. **Cibles de diffusion** : Twitch / YouTube / RTMP custom → URL de base + clé de stream.
   La clé est **chiffrée au repos** (ASP.NET Data Protection / DPAPI), jamais loggée.
4. **Construction dynamique** de la ligne de commande ffmpeg à partir du profil + cible.
   Doit reproduire exactement une commande du type :
   `ffmpeg -re -stream_loop -1 -i in.mp4 -c:v h264_nvenc -preset p2 -rc cbr
    -b:v 3000k -maxrate 3000k -bufsize 3000k -g 60 -keyint_min 60
    -c:a aac -b:a 128k -ar 48000 -f flv <rtmp_url>/<stream_key>`
   Afficher la commande générée (clé masquée) avant lancement.
5. **Gestion du processus ffmpeg** : start / stop / restart propre, capture stdout+stderr,
   annulation via CancellationToken, kill du process si l'app ferme.
6. **Monitoring temps réel** : parse la sortie ffmpeg (frame=, fps=, bitrate=, speed=,
   dropped/dup frames) → statut de session (En cours / Reconnexion / Arrêté / Erreur) +
   indicateur de santé (ex : speed < 1.0x = alerte). Flux de logs consultable dans l'UI.
7. **Résilience** : détection de chute du flux → redémarrage automatique de ffmpeg avec
   backoff configurable.

## Architecture (esquisse)
- **Domain** : StreamProfile, EncodingSettings (Value Object), AudioSettings (VO),
  StreamTarget, StreamSession (agrégat, machine à états), événements de domaine sur
  les transitions de session.
- **Application** : CQRS. Commands (StartStream, StopStream, SaveProfile, DeleteProfile),
  Queries (GetProfiles, GetSessionStatus, GetLogs). Contrats d'I/O explicites.
- **Infrastructure** : IFfmpegProcessRunner (wrap System.Diagnostics.Process),
  IFfmpegCommandBuilder, IFfprobeService (validation média), IStreamKeyProtector
  (chiffrement), persistance des profils (SQLite ou JSON local).
- **Presentation** : Blazor Server — pages Profils, Cibles, Diffusion (dashboard live).

## Contraintes qualité
- Vérifier au démarrage la présence de ffmpeg/ffprobe (PATH ou chemin configuré) et,
  si NVENC demandé, la dispo de l'encodeur (`ffmpeg -encoders`).
- Ne jamais exposer la clé de stream dans les logs, l'UI (masquée) ou les messages d'erreur.
- Gestion propre des erreurs process (ffmpeg introuvable, RTMP refusé, GPU indispo).
- Tests unitaires sur le CommandBuilder (mapping profil → arguments) et la machine à états.

## Livrable attendu (première itération)
1. Arborescence de la solution (projets + dépendances entre couches).
2. Le Domain complet + le FfmpegCommandBuilder avec ses tests.
3. Un dashboard Blazor minimal : sélection vidéo, choix profil, choix cible, start/stop,
   logs live. On itérera sur le monitoring détaillé ensuite.

---

## Addendum environnement (constaté le 2026-07-06)
- Machine de dev : Windows 11 Pro. SDKs installés : **10.0.301** (et 10.0.100-rc.1).
  **Aucun SDK ni runtime .NET 9** → la spec disait ".NET 9" mais ce serait non
  exécutable ici. **Cible retenue : `net10.0` / C# 14** (LTS, successeur direct,
  aucune API de la spec impactée). À documenter en ADR.
- ffmpeg/ffprobe : **absents du PATH** → la vérification au démarrage et le chemin
  configurable (prévus par la spec) sont d'autant plus nécessaires. Les tests unitaires
  de la première itération n'exécutent pas le binaire.
- Le dépôt n'est pas encore un dépôt git.
- Décisions utilisateur du 2026-07-06 (priment sur le texte ci-dessus) :
  le code est **intégralement en anglais** (identifiants, fichiers, enums) ;
  la couche présentation s'appelle **Nagare.WinApp** ; l'UI utilise **MudBlazor** ;
  la « cible de diffusion » (`StreamTarget`) s'appelle désormais **`Channel`**
  (chaîne) et Twitch/YouTube/RTMP custom sont les valeurs de l'enum **`Platform`**.
- Feature en cadrage (eva) : planification de stream (date de début, durée en
  heures, channel) — voir docs/product/stream-scheduling.md quand disponible.
