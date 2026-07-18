**ComboBox** is the Fluent dropdown select. Feed its `items` from the domain (codec preset lists, allowed sample rates, platform enum) so the UI offers exactly the valid values.

```jsx
<ComboBox header="Profil d'encodage" items={profiles} value={sel} onChange={setSel} placeholder="Choisir un profil" />
<ComboBox header="Plateforme" items={["Twitch", "YouTube", "RTMP custom"]} value={p} onChange={setP} />
<ComboBox header="Preset" items={["p1","p2","p3","p4","p5","p6","p7"]} value={preset} onChange={setPreset} />
```

- `items` — strings/numbers or `{ value, label }`.
- `value` / `onChange(value)` — controlled selection.
- Selected option gets the accent bar + check; popup is a blurred Acrylic flyout that closes on outside click.
