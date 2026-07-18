**LaunchChecklist** makes broadcast readiness visible, so a disabled **Démarrer** always explains itself — no dead button, no silent block. This closes the app's worst gap (Zeigarnik / Goal-Gradient).

```jsx
<LaunchChecklist items={[
  { label: "Environnement ffmpeg", done: true },
  { label: "Fichier vidéo",        done: true },
  { label: "Profil d'encodage",    done: false },
  { label: "Channel",              done: false },
]} />
```

- Drive `done` from the same inputs the Application preflight uses; don't re-implement the verdict in the UI.
- Pending rows name what's missing; the N/total count + progress bar give the goal-gradient nudge.
- Pair it with the Start button: when the preflight blocks for an environment/file reason, the InfoBar carries the *fix*; the checklist carries the *what's-left*.
