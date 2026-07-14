# Nagare — Modèle du domaine (UML mermaid)

> Vue de conception (itération 1). Source : `docs/ARCHITECTURE.md` §2.
> Identifiants en anglais (le code est intégralement en anglais).

## Diagramme de classes

```mermaid
classDiagram
    direction LR

    class StreamProfile {
        <<aggregate root>>
        +ProfileId Id
        +string Name
        +EncodingSettings Video
        +AudioSettings Audio
        +InputOptions Input
        +Create(name, video, audio, input)$ StreamProfile
        +Update(name, video, audio, input)
    }
    class EncodingSettings {
        <<value object>>
        +VideoCodec Codec
        +string Preset
        +RateControl RateControl
        +int BitrateKbps
        +int MaxrateKbps
        +int BufsizeKbps
        +int GopSize
        +int KeyintMin
        +Resolution? Resolution
        +int? Fps
    }
    class AudioSettings {
        <<value object>>
        +AudioCodec Codec
        +int BitrateKbps
        +int SampleRateHz
    }
    class InputOptions {
        <<value object>>
        +bool ReadAtNativeRate
        +bool LoopInfinitely
    }

    class Channel {
        <<aggregate root>>
        +ChannelId Id
        +string Name
        +Platform Platform
        +string BaseUrl
        +ProtectedStreamKey Key
        +Create(name, platform, baseUrl, key)$ Channel
        +Update(name, platform, baseUrl)
        +ReplaceKey(newKey)
    }
    class ProtectedStreamKey {
        <<value object>>
        +string CipherText
        +ToString() string
    }

    class StreamSession {
        <<aggregate root>>
        +SessionId Id
        +ProfileId ProfileId
        +ChannelId ChannelId
        +string InputFilePath
        +SessionStatus Status
        +int ReconnectAttempts
        +ReconnectPolicy Policy
        +string? LastError
        +Launch(profileId, channelId, inputFilePath, policy)$ StreamSession
        +MarkRunning()
        +BeginReconnect(reason)
        +Stop()
        +MarkFailed(reason)
    }
    class ReconnectPolicy {
        <<value object>>
        +int MaxAttempts
        +TimeSpan InitialDelay
        +double Factor
        +TimeSpan MaxDelay
    }

    class Platform {
        <<enumeration>>
        Twitch
        YouTube
        CustomRtmp
    }
    class SessionStatus {
        <<enumeration>>
        Starting
        Running
        Reconnecting
        Stopped
        Failed
    }
    class VideoCodec {
        <<enumeration>>
        H264Nvenc
        HevcNvenc
        Libx264
    }
    class RateControl {
        <<enumeration>>
        Cbr
        Vbr
    }
    class AudioCodec {
        <<enumeration>>
        Aac
    }

    StreamProfile *-- EncodingSettings
    StreamProfile *-- AudioSettings
    StreamProfile *-- InputOptions
    EncodingSettings ..> VideoCodec
    EncodingSettings ..> RateControl
    AudioSettings ..> AudioCodec

    Channel *-- ProtectedStreamKey
    Channel ..> Platform

    StreamSession *-- ReconnectPolicy
    StreamSession ..> SessionStatus
    StreamSession ..> StreamProfile : réf. ProfileId
    StreamSession ..> Channel : réf. ChannelId
```

Notes de conception :

- Les trois **agrégats** ne se référencent **que par identifiant** (`ProfileId`,
  `ChannelId`) — jamais par référence objet directe (frontières d'agrégat).
- `EncodingSettings` porte les invariants **E1–E8** (voir ARCHITECTURE.md §2.2),
  validés au constructeur (`DomainException` sinon).
- `ProtectedStreamKey` ne contient **que le chiffré** ; `ToString()` renvoie
  `****`. Le déchiffrement vit en Infrastructure, jamais en Domain/Application.
- `StreamSession` n'est **pas persistée** en itération 1 (vit en mémoire).

## Diagramme d'états — `StreamSession`

Transition / événement de domaine émis :

```mermaid
stateDiagram-v2
    [*] --> Starting : StartStreamCommand / SessionLaunched
    Starting --> Running : 1res stats ffmpeg / SessionStarted
    Starting --> Failed : échec lancement / SessionFailed
    Starting --> Stopped : stop utilisateur / SessionStopped
    Running --> Reconnecting : chute du flux / ReconnectStarted
    Running --> Stopped : stop utilisateur / SessionStopped
    Running --> Failed : faute interne / abandon explicite / SessionFailed
    Reconnecting --> Reconnecting : relance échouée, tentative n+1 / ReconnectStarted
    Reconnecting --> Running : redémarrage OK / SessionRecovered
    Reconnecting --> Failed : tentatives épuisées / SessionFailed
    Reconnecting --> Stopped : stop utilisateur / SessionStopped
    Stopped --> [*]
    Failed --> [*]
```

Règle assumée : un échec en `Starting` va directement en `Failed` (pas de
backoff — la config est probablement fautive). La reconnexion automatique avec
backoff est réservée aux chutes d'un flux **déjà établi** (`Running`).

`Running → Failed` (`MarkFailed`) est l'**abandon explicite** : une faute interne
prive la session de tout ce qui pourrait la faire avancer (plus de process, plus
de timer) — sans cette transition, elle resterait un **zombie** en `Running`. Elle
ne court-circuite **pas** la règle ci-dessus : une sortie de ffmpeg passe toujours
par `BeginReconnect` et consomme une tentative. Elle a été ouverte parce que
l'atteindre via `BeginReconnect` émettait un `ReconnectStarted` **mensonger** dans
la piste d'audit de la session.

**L'auto-transition `Reconnecting → Reconnecting`** est le cœur du backoff : une
relance ffmpeg qui meurt avant d'émettre des stats compte une tentative de plus
(délai `ReconnectPolicy.DelayFor(n)`, exponentiel plafonné). Quand les tentatives
sont épuisées, la session part en `Failed`. Une reconnexion **réussie**
(`MarkRunning`) remet `ReconnectAttempts` à 0 : le budget complet est restauré
pour un futur incident.

> Cette transition manquait au modèle initial, et le garde de `BeginReconnect`
> refusait `Reconnecting` — la branche d'épuisement était donc **inatteignable**
> (aucun backoff progressif, aucun échec après N tentatives). Bug révélé par les
> tests et corrigé (commit `41856d9`).

## Événements de domaine

```mermaid
classDiagram
    class IDomainEvent {
        <<interface>>
        +DateTimeOffset OccurredAt
    }
    class SessionLaunched {
        +SessionId Id
        +ProfileId ProfileId
        +ChannelId ChannelId
    }
    class SessionStarted {
        +SessionId Id
    }
    class ReconnectStarted {
        +SessionId Id
        +int Attempt
        +TimeSpan NextDelay
        +string Reason
    }
    class SessionRecovered {
        +SessionId Id
        +int AfterAttempts
    }
    class SessionStopped {
        +SessionId Id
    }
    class SessionFailed {
        +SessionId Id
        +string Reason
    }
    IDomainEvent <|.. SessionLaunched
    IDomainEvent <|.. SessionStarted
    IDomainEvent <|.. ReconnectStarted
    IDomainEvent <|.. SessionRecovered
    IDomainEvent <|.. SessionStopped
    IDomainEvent <|.. SessionFailed
```

Dispatch (volontairement minimal, ADR/ARCHITECTURE §2.5) : l'agrégat accumule ses
événements ; le `StreamSessionCoordinator` (Application) draine la collection après
chaque transition et les publie explicitement (notification UI + logs). Pas de bus,
pas de réflexion — `IDomainEventHandler<T>` seulement si un 2ᵉ consommateur apparaît.
```

## Couches (dépendances)

```mermaid
graph TD
    WinApp["Nagare.WinApp<br/>Blazor Server · MudBlazor · composition root"] --> App["Nagare.Application<br/>CQRS, ports, coordinateur"]
    WinApp --> Infra["Nagare.Infrastructure<br/>ffmpeg, Data Protection, JSON"]
    Infra --> App
    App --> Dom["Nagare.Domain<br/>agrégats, VOs, événements"]
    Infra --> Dom
```
