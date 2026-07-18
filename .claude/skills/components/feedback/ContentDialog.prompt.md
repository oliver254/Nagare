**ContentDialog** is Nagare's only modal, used for destructive confirmation. The confirmation must **name the object** being removed, and the destructive button is `danger` (not the accent).

```jsx
<ContentDialog
  title="Supprimer le channel « Twitch principal » ?"
  primaryText="Supprimer" primaryVariant="danger" onPrimary={del}
  closeText="Annuler" onClose={close}
>
  Cette action est définitive. La clé de stream chiffrée sera perdue ; vous devrez la ressaisir pour recréer ce channel.
</ContentDialog>
```

- Always name the object in the title (Recognition over recall).
- `primaryVariant="danger"` for deletes — never `accent` (Fitts's Law: the destructive action isn't the highlighted one).
- **Never open during a live broadcast** (Flow). Deletes live on the Profils / Channels pages.
- Use `contained` to preview/embed the dialog inside a positioned box instead of full-viewport.
