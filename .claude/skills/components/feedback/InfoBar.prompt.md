**InfoBar** is the Fluent inline message. Show the domain's own message text (E1–E8, environment issues) verbatim.

```jsx
<InfoBar severity="error" title="Environnement ffmpeg"
  message="ffmpeg est introuvable. Renseignez son chemin dans la configuration de l'application, ou ajoutez ffmpeg au PATH." />
<InfoBar severity="warning" title="Réglage refusé" message="E4: bufsize must be greater than or equal to bitrate." isClosable />
<InfoBar severity="success" title="Session arrêtée" message="Aucun drop, aucune reconnexion." />
```

- `severity`: `informational` · `success` · `warning` · `error`.
- Place messages **in the flow of the task** they concern (Selective Attention), not only in a tall top banner.
- **During a broadcast, never render a blocking InfoBar or one that steals focus** (Flow). A reconnection shows as a non-blocking status change.
