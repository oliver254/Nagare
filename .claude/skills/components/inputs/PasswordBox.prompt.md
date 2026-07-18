**PasswordBox** is the Fluent masked field used for the **stream key**. A saved key is never re-shown (ADR-0005): the field starts empty when editing an existing channel, and an empty field on save means "keep the current key".

```jsx
<PasswordBox
  header="Clé de stream"
  placeholder="•••••••••••"
  hint="Laisser vide conserve la clé actuelle : une clé enregistrée ne peut jamais être réaffichée."
/>
```

- Never pre-fill it with a stored key, never log it, never copy it — not even a masked "•••• + last 4" (the DTO physically cannot carry it).
- `revealable` (default true) unmasks only the value being typed right now; set `false` to remove the eye toggle entirely.
- The reveal button uses `onMouseDown → preventDefault` so focus stays in the field.
