**ToggleSwitch** is the Fluent on/off switch. Nagare prefers it over a checkbox because it binds cleanly to a `bool` (`IsOn`), matching the profile editor's boolean options.

```jsx
<ToggleSwitch header="Forcer une résolution" checked={hasRes} onChange={setHasRes} />
<ToggleSwitch header="Boucle infinie (-stream_loop -1)" defaultChecked />
<ToggleSwitch header="Lire à la vitesse native (-re)" checked={re} onChange={setRe} showStateText />
```

- `onChange(checked)` receives the boolean.
- On = accent track + white knob; off = strong-outline track. Focusable and toggles with Space/Enter.
- Use `showStateText` for an explicit "Activé / Désactivé" label when the header alone isn't clear.
