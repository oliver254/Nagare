# Nagare app — UI kit

A high-fidelity, interactive recreation of the **redesigned** Nagare desktop app
(the target the UX brief describes — the current shipping shell is an
acknowledged flat placeholder). It composes the design-system components; it is
not production XAML.

Open `index.html`. The three destinations are wired:

- **Tableau de bord** — the broadcast page. Four belted cards (Common Region):
  - *Source* — drag-and-drop / pick a `.mp4`; media summary as chips.
  - *Diffusion* — profil + channel, and the generated ffmpeg command **with the
    key masked** + a copy button.
  - *Santé* — when idle, a **LaunchChecklist** (Environnement · Fichier · Profil ·
    Channel) so a disabled **Démarrer** always says what's missing; when live, the
    status badge + chunked stat tiles; on stop, a **session summary** (Peak-End).
  - *Journal* — the ffmpeg log console (monospace, error/warning highlighting).
  - A **Démo** strip (kit only) walks the states without a real broadcast:
    pre-fill config, drop the stream, reconnect, recover, simulate a failure.
- **Profils** — CRUD with a **preset picker** (Hick's Law), fields **chunked** into
  Vidéo / Débit / Audio, an **"Avancé"** disclosure for bufsize / GOP / keyint_min,
  and domain invariant messages (E1–E8) surfaced verbatim.
- **Channels** — CRUD with the stream key in a `PasswordBox` that is **never
  re-shown**; delete asks for confirmation **naming the channel**.

## How it maps to the real code

- Data shapes mirror `StreamProfileDto` / `ChannelDto` and the encoding summary
  from `EncodingSummaryConverter`. The command string mirrors `FfmpegCommandBuilder`
  output (masked, per ADR-0005 / SPEC §4).
- The start gating mirrors `GetStartPreflightQuery`'s two tiers (what's *wrong*
  before what's *not done yet*).
- Status labels come from `DashboardViewModel.LabelOf`.

## Files

- `index.html` — mounts the shell + pages; loads the DS bundle.
- `store.js` — fake seed data + the masked-command builder (`window.NagareSeed`, `window.NagareBuildCommand`).
- `AppShell.jsx` — Windows 11 caption bar + `NavRail` + content sheet.
- `DashboardPage.jsx`, `ProfilesPage.jsx`, `ChannelsPage.jsx` — the three screens.
- `kit.css` — kit-only layout (window chrome, dashboard grid, CRUD lists).

## Known backend dependency (escalated, per brief §8)

The live "en direct · HH:MM:SS" timer needs a **start time** that `SessionSnapshot`
doesn't carry today — a local clock would be wrong on rehydration. The kit shows
the intended design to motivate adding `StartedAt` to `SessionSnapshot`
(Application layer), not a presentation hack.
