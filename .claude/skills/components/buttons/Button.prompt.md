**Button** is the Fluent action control. Use the `accent` variant for the single primary action on a screen (Démarrer, Enregistrer, Nouveau); everything else is `standard` or `subtle`.

```jsx
<Button variant="accent" icon="play">Démarrer</Button>
<Button>Arrêter</Button>
<Button variant="subtle" icon="pencil">Modifier</Button>
<Button variant="danger" icon="trash-2">Supprimer</Button>
<Button variant="hyperlink">En savoir plus</Button>
<Button variant="accent" disabled>Démarrer</Button>
```

- `variant`: `standard` (default) · `accent` (one per screen) · `subtle` (toolbars, low-emphasis) · `hyperlink` · `danger` (destructive label tint).
- `size`: `standard` (32px) · `small` (24px).
- `icon`: Lucide name or node; `iconPosition` `start`/`end`.
- Commands that open more UI take an ellipsis label: `Choisir un fichier…`.
- Never place a destructive button beside the accent primary (Fitts's Law).
