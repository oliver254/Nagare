**EmptyState** turns an empty list into instructions. The first-run path (0 profils, 0 channels) is the one to get right — each empty screen names the next step.

```jsx
<EmptyState icon="radio-tower" title="Aucun channel"
  message="Créez un channel pour choisir où diffuser."
  action={<Button variant="accent" icon="plus">Nouveau channel</Button>} />

<EmptyState icon="sliders-horizontal" title="Aucun profil d'encodage"
  message="Un profil décrit comment encoder : codec, débit, résolution. Commencez par un preset."
  action={<Button variant="accent" icon="plus">Nouveau profil</Button>} />
```

- Keep the message to one short sentence and always offer the CTA.
- Never leave a blank list with no guidance (Paradox of the Active User).
