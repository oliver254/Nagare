**IconButton** is a square, icon-only button for toolbars and list-row actions. `label` is required — it supplies the accessible name and the tooltip.

```jsx
<IconButton icon="copy" label="Copier la commande" />
<IconButton icon="pencil" label="Modifier le channel" />
<IconButton icon="trash-2" label="Supprimer le channel" variant="subtle" />
```

- `label` — required accessible name + tooltip; never omit it.
- `variant` — `subtle` (default), `standard`, `accent`.
- 32×32 target meets the 32px minimum (Fitts's Law). For destructive row actions, still route through a confirmation ContentDialog.
