**Card** is a bounded region (a 1px stroke, 8px radius, no shadow). Use it to group related controls — the dashboard becomes four cards instead of a flat wall.

```jsx
<Card title="Source" icon="file-video">
  {/* file picker + media summary */}
</Card>

<Card title="Santé" icon="activity" badge={<StatusBadge tone="live">En direct</StatusBadge>}>
  {/* stat tiles */}
</Card>
```

- `title` + `icon` render a quiet header; `badge` fills a right-aligned header slot (status).
- Dashboard grouping: **Source** (file + media), **Diffusion** (profil + channel + commande), **Santé** (statut + stats), **Journal** (logs).
- Cards are flat — separation comes from the stroke, not a drop shadow.
