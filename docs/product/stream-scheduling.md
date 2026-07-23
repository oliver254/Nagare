# Cadrage produit — Planification de stream (« Stream Scheduling »)

> Statut : cadrage (eva) — 2026-07-06. À dispatcher vers `harold` (modélisation)
> puis `ada` / `blazor-ux`. Ne remet pas en cause l'itération 1 en cours.
> Vocabulaire : la destination réutilisable = **Channel** (chaîne) ; Twitch /
> YouTube / RTMP custom = **Platforms** (plateformes).

## 1. Contexte & problème

Demande utilisateur (verbatim) :

> « En tant qu'utilisateur, je souhaite lancer un stream, définir la date de
> début, nombre d'heures et sur quel channel (youtube ou twitch ou autre) afin
> de lancer le stream sur la plateforme adéquate. »

Décomposition :
- **Déjà couvert par l'itération 1** : choisir le channel (Channel persisté,
  Platform Twitch/YouTube/RTMP custom), choisir fichier + profil, lancer et
  arrêter **manuellement** un stream, suivre son statut et ses logs.
- **Le nouveau** : la dimension **temporelle** — programmer un démarrage à une
  date/heure future et borner la diffusion à une **durée en heures**, avec
  démarrage et arrêt **automatiques**.

Bénéfice : diffuser une vidéo en boucle sur un créneau choisi (ex. « ce soir de
21 h à 1 h sur Twitch ») sans être devant la machine au moment du démarrage ni
de l'arrêt.

## 2. Scope

### Dans le périmètre
- Programmer **une** diffusion : fichier + profil + channel + date/heure de
  début + durée en heures.
- Démarrage automatique à l'heure dite, arrêt automatique en fin de fenêtre.
- Variante immédiate : « démarrer maintenant pour N heures » (durée max sur un
  start manuel).
- Consulter la liste des planifications avec leur statut ; annuler ou modifier
  une planification **à venir**.
- Comportement défini en cas d'échec au démarrage planifié ou d'app fermée à
  l'heure H.

### Hors périmètre (explicite — YAGNI)
- **Récurrence** (tous les soirs, hebdomadaire…) : aucun signal de besoin ;
  reprogrammer à la main est acceptable pour un outil personnel.
- **Multi-streams simultanés** : l'invariant itération 1 « une seule session
  active » est conservé (voir arbitrage C).
- **Vue calendrier** : une liste chronologique suffit à cette volumétrie.
- **Exécution app fermée** (service Windows, Task Scheduler) : voir arbitrage A
  — signalé comme extension future, pas dans ce lot.
- **Notifications externes** (mail, Discord…) en cas d'échec : l'UI locale
  suffit pour l'instant.
- **Historique de sessions détaillé** : reste hors périmètre (comme en
  itération 1) ; seul le statut final de la planification est conservé
  (arbitrage E).

## 3. User stories & critères d'acceptation

Rôle unique : « l'utilisateur » (app locale mono-utilisateur).

### US-0 (Lot 1) — Démarrer maintenant pour une durée limitée
**En tant qu'**utilisateur, **je veux** lancer un stream immédiatement en fixant
une durée maximale, **afin qu'**il s'arrête tout seul sans que j'aie à revenir.

Critères d'acceptation :
- **Étant donné** le formulaire de lancement (fichier + profil + channel valides),
  **quand** je renseigne une durée de N heures et que je démarre,
  **alors** la session démarre comme un start manuel et une heure de fin
  (= maintenant + N) est affichée.
- **Étant donné** une session lancée avec durée, **quand** l'heure de fin est
  atteinte et que la session est `EnCours`, **alors** elle s'arrête proprement
  (équivalent d'un stop utilisateur → `Arrete`) et l'UI indique « arrêt
  automatique (durée atteinte) ».
- **Étant donné** une session lancée avec durée, **quand** je clique Stop avant
  l'heure de fin, **alors** l'arrêt manuel fonctionne comme aujourd'hui (la
  durée n'empêche rien).
- **Étant donné** le formulaire, **quand** je ne renseigne pas de durée,
  **alors** le comportement actuel (stream sans limite) est inchangé.
- **Étant donné** une durée ≤ 0, **quand** je valide, **alors** la saisie est
  refusée avec un message explicite.

### US-1 (Lot 2) — Programmer un stream
**En tant qu'**utilisateur, **je veux** programmer un stream (fichier, profil,
channel, date/heure de début, durée en heures), **afin qu'**il se lance sur la
plateforme voulue sans intervention.

Critères d'acceptation :
- **Étant donné** des Channels et profils existants, **quand** je crée une
  planification avec fichier + profil + channel + date de début **future** +
  durée > 0, **alors** elle est enregistrée avec le statut « Programmée » et
  persiste après redémarrage de l'app.
- **Étant donné** une date de début dans le passé, **quand** je valide,
  **alors** la création est refusée avec un message explicite (voir arbitrage B).
- **Étant donné** une planification dont la fenêtre [début, début + durée)
  chevauche celle d'une planification « Programmée » existante, **quand** je
  valide, **alors** la création est refusée en nommant la planification en
  conflit (voir arbitrage C).
- **Étant donné** la création, **quand** la planification est enregistrée,
  **alors** l'aperçu de la commande ffmpeg **masquée** est consultable, avec la
  mention que la commande réelle sera construite au moment du démarrage (le
  profil ou la clé du channel peuvent changer d'ici là).

### US-2 (Lot 2) — Démarrage automatique à l'heure dite
**En tant qu'**utilisateur, **je veux** que le stream programmé démarre seul à
l'heure de début, **afin de** ne pas avoir à être devant l'écran.

Critères d'acceptation :
- **Étant donné** une planification « Programmée » et l'app ouverte, **quand**
  l'heure de début est atteinte et qu'aucune session n'est active, **alors** une
  session démarre (même chemin que le start manuel : validation fichier,
  construction commande, machine à états) et la planification passe à
  « En cours ».
- **Étant donné** une session déjà active (manuelle ou autre) à l'heure H,
  **quand** le déclenchement a lieu, **alors** la session en cours n'est
  **jamais** interrompue ; la planification passe à « Échouée » avec la raison
  « une session était déjà active » (voir arbitrage C).
- **Étant donné** l'app **fermée** à l'heure H, **quand** je rouvre l'app après
  la fin de la fenêtre, **alors** la planification est affichée « Manquée »
  (aucun démarrage tardif silencieux — voir arbitrage A).
- Tolérance de déclenchement : le démarrage a lieu au plus tard 1 minute après
  l'heure programmée (app ouverte) — critère vérifiable pour les tests.

### US-3 (Lot 2, mécanique partagée avec US-0) — Arrêt automatique en fin de fenêtre
**En tant qu'**utilisateur, **je veux** que le stream s'arrête seul à la fin de
la durée programmée, **afin de** ne pas diffuser au-delà du créneau prévu.

Critères d'acceptation :
- **Étant donné** une session lancée par une planification, **quand** l'heure
  de fin (début + durée) est atteinte et que la session est `EnCours`,
  **alors** elle s'arrête proprement et la planification passe à « Terminée ».
- **Étant donné** une session en `Reconnexion` (backoff), **quand** l'heure de
  fin est atteinte, **alors** les tentatives de reconnexion sont abandonnées et
  la session s'arrête ; la planification passe à « Terminée » (voir arbitrage D).
- **Étant donné** une session planifiée, **quand** je l'arrête manuellement
  avant l'heure de fin, **alors** l'arrêt fonctionne et la planification passe
  à « Terminée » (mention « arrêtée manuellement »).
- **Étant donné** une session planifiée tombée en `Erreur` (tentatives
  épuisées) avant la fin de fenêtre, **alors** la planification passe à
  « Échouée » avec la raison (scrubbée) ; **aucune relance** n'est tentée dans
  la même fenêtre (cohérent avec la machine à états : `Erreur` est terminal).

### US-4 (Lot 2) — Consulter les planifications
**En tant qu'**utilisateur, **je veux** voir mes planifications et leur statut,
**afin de** savoir ce qui va se lancer, ce qui tourne et ce qui s'est passé.

Critères d'acceptation :
- **Étant donné** des planifications existantes, **quand** j'ouvre la liste,
  **alors** je vois pour chacune : fichier, profil, channel (+ plateforme),
  début, durée, fin calculée, statut (« Programmée », « En cours »,
  « Terminée », « Annulée », « Manquée », « Échouée »).
- **Étant donné** une planification « Programmée », **alors** le temps restant
  avant démarrage est visible (compte à rebours).
- **Étant donné** une planification « En cours », **alors** le temps restant
  avant l'arrêt automatique est visible, et le dashboard de diffusion distingue
  une session **planifiée** d'une session **manuelle**.
- **Étant donné** une planification « Échouée » ou « Manquée », **alors** la
  raison est consultable (texte scrubbé — jamais la clé de stream).

### US-5 (Lot 2) — Annuler / modifier une planification
**En tant qu'**utilisateur, **je veux** annuler ou corriger une planification à
venir, **afin de** rester maître de ce qui sera diffusé.

Critères d'acceptation :
- **Étant donné** une planification « Programmée », **quand** je l'annule
  (avec confirmation), **alors** elle passe à « Annulée » et ne se déclenchera
  pas.
- **Étant donné** une planification « Programmée », **quand** je la modifie
  (fichier, profil, channel, début, durée), **alors** les mêmes validations
  qu'à la création s'appliquent (date future, pas de chevauchement).
- **Étant donné** une planification « En cours », **quand** je veux
  l'interrompre, **alors** je passe par le Stop de la session (pas de
  modification d'une planification en cours) ; les statuts terminaux ne sont
  ni modifiables ni annulables (seulement supprimables de la liste).
- **Étant donné** un profil ou un channel référencé par une planification
  « Programmée », **quand** je tente de le supprimer, **alors** la suppression
  est refusée en nommant la planification (extension de la garde existante
  « refusée si utilisé par la session active »).

### US-6 (Lot 2) — Échec au démarrage planifié
**En tant qu'**utilisateur, **je veux** qu'un démarrage planifié qui échoue
soit clairement signalé, **afin de** comprendre pourquoi je n'ai pas diffusé.

Critères d'acceptation :
- **Étant donné** un déclenchement à l'heure H, **quand** le lancement échoue
  (fichier disparu/illisible, ffmpeg introuvable, RTMP refusé, NVENC indispo),
  **alors** la planification passe à « Échouée » avec la raison scrubbée, et
  l'échec est visible dans la liste et sur le dashboard.
- **Aucun retry automatique** d'un échec initial : cohérent avec le choix acté
  en itération 1 (échec en `Demarrage` = config probablement fautive, le
  backoff est réservé aux chutes d'un flux établi).
- **Étant donné** un échec, **alors** aucune autre planification n'est
  impactée (la suivante se déclenchera normalement).

## 4. Arbitrages métier (recommandations)

| # | Question | Recommandation | Alternative écartée / future |
|---|---|---|---|
| A | L'app doit-elle être ouverte à l'heure H ? | **Oui.** Le déclencheur vit dans le process de l'app (fenêtre WinUI 3 — ADR-0006 ; l'arbitrage est inchangé, seul l'hôte l'est). App fermée à H ⇒ planification « Manquée » au redémarrage, sans démarrage tardif silencieux (diffuser en retard sans témoin est pire que ne pas diffuser). L'UI l'annonce clairement à la création (« l'application doit rester ouverte »). | Extension future : lancement par le Planificateur de tâches Windows ou un service Windows. Hors scope — à ne considérer que si des « Manquée » réelles s'accumulent. Un « rattrapage » optionnel (proposer de démarrer pour le temps restant si l'app rouvre pendant la fenêtre) est envisageable plus tard, **proposé**, jamais automatique. |
| B | Date dans le passé / « maintenant » | Date de début strictement future (tolérance de saisie ~1 min pour éviter les refus à la seconde près). « Démarrer maintenant pour N heures » n'est **pas** une planification : c'est le start manuel + durée (US-0, Lot 1). | Accepter une date passée « pour lancer tout de suite » : ambigu, doublonne US-0. |
| C | Chevauchement de planifications | Conserver l'invariant « **une seule session active** ». Refus **à la création** de toute fenêtre chevauchant une planification « Programmée ». **Au déclenchement**, si une session (manuelle) est active : la planification échoue, la session en cours n'est jamais tuée — un flux qui tourne a toujours raison sur un automatisme. | File d'attente (« démarrer dès que libre ») : complexité et surprise pour un gain hypothétique. Multi-sessions : hors scope. |
| D | Fin de fenêtre pendant une reconnexion (backoff) | **L'arrêt planifié gagne** : à l'heure de fin, les tentatives sont abandonnées et la session s'arrête (`Reconnexion → Arrete`, transition déjà autorisée). L'utilisateur a demandé N heures ; réessayer au-delà contredit son intention. Statut final : « Terminée » (la fenêtre est consommée). | Laisser le backoff finir sa tentative en cours : fenêtre dépassée, comportement imprévisible. |
| E | Devenir après exécution | La planification **reste dans la liste** avec son statut terminal (Terminée / Manquée / Échouée / Annulée) : trace minimale à coût quasi nul, précieuse pour comprendre « pourquoi ça n'a pas streamé cette nuit ». Suppression manuelle possible. Pas d'historique de sessions détaillé (inchangé). | Suppression automatique après exécution : perte de la seule trace d'un échec nocturne. Purge automatique (ex. > 50 entrées) : à ajouter seulement si la liste devient réellement encombrante. |
| F | Fuseau horaire | **Heure locale de la machine**, point. Saisie et affichage en heure locale ; stockage avec offset (robuste à un changement d'heure entre création et exécution). Pas de sélecteur de fuseau (app locale mono-utilisateur). Risque assumé et documenté : autour d'un changement d'heure DST, le déclenchement suit l'horloge locale (cas rarissime, non bloquant). | Stockage/saisie UTC : source d'erreurs de saisie pour zéro bénéfice ici. |

Règle de gestion transverse : la commande ffmpeg est construite **au moment du
déclenchement** (profil / channel / clé à jour), pas à la création de la
planification. L'aperçu affiché à la création est indicatif.

## 5. Priorisation

**Positionnement : itération 2**, après livraison de l'itération 1 (la
planification s'appuie sur le start/stop, la machine à états et la persistance —
les déstabiliser pendant leur implémentation aurait été contre-productif).

> **Point de situation (2026-07-23).** L'itération 1 est **livrée** : les quatre
> couches sont en place, l'application WinUI 3 tourne et les 7 phases du plan de
> migration sont closes. La dépendance bloquante est donc levée. Reste ouvert
> avant d'attaquer ce chantier : la **conception UX/UI**
> (`docs/design/prompt-ux-ui.md`), qui doit lui laisser sa place en navigation
> (4ᵉ entrée « Planifications ») et prévoir le champ « durée » optionnel au
> lancement.

Découpage en lots incrémentaux :

| Lot | Contenu | Valeur / justification |
|---|---|---|
| **Lot 1 — Durée max sur start manuel** (US-0) | Champ « durée » optionnel au lancement + arrêt automatique | Petit incrément sur l'existant, livre immédiatement la moitié de la valeur (« je lance ce soir avant de me coucher, ça s'arrête seul ») et fait émerger la mécanique d'arrêt programmé réutilisée au Lot 2 |
| **Lot 2 — Démarrage différé** (US-1 à US-6) | Planification persistée, déclencheur, statuts, liste, annulation/modification, gestion des échecs/manqués | Le cœur de la demande ; dépend du Lot 1 pour l'arrêt en fin de fenêtre |

MoSCoW au sein de la feature : **Must** = US-0, US-1, US-2, US-3, US-4
(consultation + annulation basique de US-5) ; **Should** = modification d'une
planification (US-5), compte à rebours ; **Could** = rattrapage proposé après
réouverture de l'app (arbitrage A) ; **Won't (cette fois)** = récurrence,
multi-streams, calendrier, Task Scheduler, notifications externes.

## 6. Besoins UX fonctionnels (le QUOI — conception UI par `blazor-ux`)

L'utilisateur doit pouvoir :
- Saisir une date/heure de début (future) et une durée en heures, avec l'heure
  de fin calculée affichée avant validation.
- Renseigner une durée optionnelle sur le lancement immédiat (Lot 1).
- Voir la liste chronologique des planifications avec statut, channel
  (+ plateforme), fichier, profil, fenêtre horaire.
- Distinguer d'un coup d'œil « Programmée » / « En cours » / états terminaux
  (Terminée, Annulée, Manquée, Échouée), et distinguer sur le dashboard une
  session planifiée d'une session manuelle.
- Voir un compte à rebours avant démarrage et le temps restant avant arrêt
  automatique pendant la diffusion.
- Annuler (avec confirmation) ou modifier une planification à venir.
- Consulter la raison d'un échec ou d'un manqué (texte scrubbé, clé jamais
  visible).
- Être averti à la création que l'app doit rester ouverte à l'heure du
  démarrage (arbitrage A).
- Consulter l'aperçu de commande masquée, avec la mention « construite au
  démarrage réel ».

## 7. Impacts pressentis sur le modèle (à confirmer par `harold`)

Niveau fonctionnel uniquement — la modélisation appartient à l'architecte :
- Un **nouveau concept persisté** type « ScheduledStream » (fichier, ProfileId,
  ChannelId, début, durée, statut propre : Programmée / En cours / Terminée /
  Annulée / Manquée / Échouée). Distinct de `StreamSession` : la planification
  **déclenche** une session ; la machine à états de session (§2.4 ARCHITECTURE)
  reste inchangée — `Reconnexion → Arrete` couvre déjà l'arbitrage D.
- Un **déclencheur temporel** côté Application (dans l'esprit du
  `StreamSessionCoordinator` / hosted service existant) : démarre à H, arrête à
  H + durée, marque « Manquée » au démarrage de l'app pour les fenêtres
  expirées.
- **Persistance JSON locale** dans le pattern ADR-0004 (ex. `schedules.json`),
  volumétrie minuscule, un seul writer.
- **Lot 1** : extension du start manuel avec une durée optionnelle (arrêt
  automatique).
- Extension des **gardes de suppression** : profil/channel référencé par une
  planification « Programmée » non supprimable (US-5).
- Possible besoin de distinguer la **raison d'arrêt** (manuel vs durée
  atteinte) dans les événements de session — à trancher par harold.

## 8. Risques

- **Attente irréaliste** : l'utilisateur peut croire que « programmé = ça
  streamera quoi qu'il arrive ». Mitigation : avertissement explicite à la
  création (app ouverte requise) + statut « Manquée » honnête.
- **Dérive de config entre création et H** (clé changée, fichier déplacé) :
  couvert par la construction de la commande au déclenchement + US-6 (échec
  clair, pas de retry).
- **Mise en veille Windows** à l'heure H : l'app est ouverte mais la machine
  dort → équivalent « Manquée » ou démarrage tardif. À documenter pour
  l'utilisateur (désactiver la veille pour les créneaux planifiés) ; toute
  gestion active (empêcher la veille) est hors scope pour l'instant.

## 9. Questions ouvertes (avec recommandation par défaut)

Voir « À valider » du cadrage : granularité de la durée, plafond de durée,
rattrapage d'une fenêtre encore ouverte à la réouverture de l'app.
