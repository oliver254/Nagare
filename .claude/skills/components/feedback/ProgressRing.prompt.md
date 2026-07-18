**ProgressRing** is the Fluent indeterminate spinner. Bind it to `IsBusy` so any action over ~400 ms (Doherty threshold) shows immediate feedback — the environment probe (three process launches) and `ffprobe` both need it.

```jsx
<ProgressRing />
<ProgressRing size={20} label="Analyse du fichier…" />
<ProgressRing size={48} label="Démarrage…" />
```

- `label` renders text beside the ring; omit for a bare spinner.
- On **Démarrer**, show "Démarrage…" immediately — before ffmpeg's first stat — then flip to the live "En direct" state (Peak-End Rule).
- Respects `prefers-reduced-motion` (slows the spin).
