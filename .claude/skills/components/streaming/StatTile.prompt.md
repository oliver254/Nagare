**StatTile** chunks one live ffmpeg stat into a scannable tile — the five stats (fps, kbits/s, speed, drops, reconnexions) become a tidy row of tiles instead of a flat number strip (Miller's Law / Chunking).

```jsx
<StatTile label="Images" value="60" unit="fps" icon="film" />
<StatTile label="Débit" value="5 998" unit="kbits/s" icon="activity" />
<StatTile label="Vitesse" value="1,00" unit="x" icon="gauge" />
<StatTile label="Drops" value="0" unit="drops" />
<StatTile label="Vitesse" value="0,82" unit="x" icon="gauge" warning />
```

- Values arrive pre-formatted (French locale, e.g. `1,02`).
- `warning` is the health signal — tie it to `speed < 1.0x` or growing drops. Don't tint tiles decoratively.
