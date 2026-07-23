**NumberBox** is the Fluent number field with compact up/down spin buttons, used throughout the profile editor.

```jsx
<NumberBox header="Bitrate (kbps)" value={3000} step={100} onChange={setBitrate} />
<NumberBox header="GOP (-g)" value={60} min={1} onChange={setGop} />
<NumberBox header="keyint_min" value={60} min={1} error="E5: GOP must be positive and 0 < keyint_min <= g." />
```

- `onChange(value)` receives the parsed **number**, not an event.
- `min`/`max`/`step` are convenience clamping only — the domain enforces the real encoding invariants (E1–E8); show its message via `error`.
- Headers carry the unit in parentheses (`Bitrate (kbps)`), matching the app.
