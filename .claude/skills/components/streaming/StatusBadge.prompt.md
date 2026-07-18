**StatusBadge** shows session status and health as **shape + icon + text** — never color alone (§9). It replaces the bare colored `Ellipse` in the current dashboard.

```jsx
<StatusBadge tone="live">En direct</StatusBadge>
<StatusBadge tone="success">En cours · sain</StatusBadge>
<StatusBadge tone="attention">Reconnexion…</StatusBadge>
<StatusBadge tone="critical">Échec</StatusBadge>
<StatusBadge tone="neutral">Aucune session</StatusBadge>
```

Map from `SessionStatus`:
- `Starting` / `Reconnecting` → `attention`
- `Running` (healthy) → `success` or `live`
- `Running` with `speed < 1.0x` / growing drops → `critical`
- `Stopped` → `neutral`; `Failed` → `critical`

Red is only for a **real** anomaly — a healthy live stream is green/`live`, not red.
