# Nagare Design System

流 **Nagare** — the design language for a Windows-native FFmpeg restreamer.

Nagare loops a local video file to Twitch, YouTube or a custom RTMP endpoint by
driving **ffmpeg** — a local, single-user, offline (except the outbound RTMP)
Windows desktop app. This design system captures how Nagare should look, read
and behave: it is **Fluent Design (Windows 11) tuned for one streamer's
workflow**, with every decision anchored to a Law of UX.

> **Status.** Nagare's Domain / Application / Infrastructure are complete and
> tested; the **presentation layer is mid-rebuild** (Blazor Server → WinUI 3
> native). This system is the visual + UX foundation for that rebuild. It is a
> *reference recreation for design work*, not the shipping XAML.

---

## Sources

Everything here is grounded in the real codebase — no invented APIs, no invented
component inventory.

- **GitHub — [oliver254/Nagare](https://github.com/oliver254/Nagare)** (`main`).
  Explore it to build more faithfully:
  - `README.md`, `docs/SPEC.md` — product spec & scope.
  - `docs/domain-model.md` — the three aggregates + the `StreamSession` state
    machine (drives every status the UI shows).
  - `docs/adr/0005-protection-cle-stream.md` — the stream-key security contract.
  - `docs/adr/0006-winui3-natif.md`, `docs/plan-winui3-migration.md` — why WinUI 3,
    and the real-time / security guardrails (§5, §6).
  - `src/Nagare.WinApp/` — the current XAML shell, three pages, `ValueConverters.cs`.
  - `src/Nagare.ViewModels/` — `DashboardViewModel`, `ProfilesViewModel`,
    `ChannelsViewModel` (the exact state and copy the UI binds to).
- The **UX/UI brief** (`docs/prompt-ux-ui`) supplied with this project — the
  Laws-of-UX mandate and acceptance criteria the redesign must meet.

The design brief is public but assumes no reader access; the summaries here
stand on their own.

---

## The product in one page

One streamer, one machine, no account. They already know the vocabulary
(bitrate, stream key, NVENC) and don't want to learn a new grammar. The central
gesture pays for everything else:

> pick a file → pick an encoding **profile** → pick a **channel** → **Démarrer** →
> watch (live, healthy?) → **Arrêter**.

Three concepts, and nothing else:

- **Profil** (`StreamProfile`) — a reusable encoding preset: codec, preset, rate
  control, bitrate/maxrate/bufsize, GOP/keyint, resolution, fps, audio, input
  options (`-re`, infinite loop). *Page: Profils.*
- **Channel** (`Channel`) — a destination: name, platform (Twitch / YouTube /
  custom RTMP), base URL, and an **encrypted stream key**. *Page: Channels.*
- **Session** (`StreamSession`) — one live broadcast. **Only one at a time.**
  States: `Starting → Running → (Reconnecting) → Stopped / Failed`. *Page:
  Tableau de bord.*

### Two allegiances (Jakob's Law)

Nagare must feel like **Windows 11** (Fluent: `NavigationView`, Mica, `InfoBar`,
`ContentDialog`, follows the system theme & accent) **and** borrow the landmarks
of **OBS** (stream key, bitrate, "en direct", a raw log console). It invents no
navigation and no bespoke control.

---

## CONTENT FUNDAMENTALS

**Language split: UI in French, code in English.** Every user-facing string is
French; every identifier, filename and enum is English. Never leak a code term
(`preflight`, `DTO`, `snapshot`, `aggregate`) into the UI — use the streamer's
word (channel, clé de stream, bitrate, en direct).

**Voice: terse, technical, honest, second-person-implicit.** Nagare addresses an
expert. It states facts and next actions; it never chats, congratulates, or
apologizes at length. No marketing tone. Instructions use the infinitive or
imperative ("Renseignez son chemin…", "Choisissez un profil libx264",
"Laisser vide conserve la clé actuelle").

**Casing: French sentence case everywhere.** Titles and labels are sentence
case ("Tableau de bord", "Profils d'encodage", "Clé de stream", "URL de base").
No Title Case (French doesn't use it). Commands that open further UI take an
ellipsis: **"Choisir un fichier…"**. Field headers are short nouns ("Codec",
"Preset", "Rate control", "Bitrate (kbps)").

**Units are lowercase and terse**, matching ffmpeg/OBS: `fps`, `kbits/s`, `x`
(speed), `drops`, `reconnexions`. Numbers use the French locale (`1,02x`).

**Errors are the domain speaking — surfaced verbatim, never re-authored.** The
domain raises rule violations (E1–E8) and the UI shows the message as-is:
> "E4: bufsize must be greater than or equal to bitrate."

Contextual guidance is written to *unblock*, not to scold:
- ffmpeg missing → "ffmpeg est introuvable. Renseignez son chemin dans la
  configuration de l'application, ou ajoutez ffmpeg au PATH."
- NVENC profile on a non-NVENC machine → "Le profil sélectionné exige NVENC,
  indisponible sur cette machine. Choisissez un profil libx264."
- Bad file → "Fichier introuvable." / "Fichier illisible par ffprobe."
- Security reassurance → "Laisser vide conserve la clé actuelle : une clé
  enregistrée ne peut jamais être réaffichée."

**Status labels are one word** (from `DashboardViewModel.LabelOf`): "Aucune
session", "Démarrage", "En cours", "Reconnexion", "Arrêtée", "Échec".

**Empty states ARE the documentation** (Paradox of the Active User): no one reads
a manual, so every empty list names the next action — "Aucun channel — créez-en
un pour diffuser" + a direct CTA.

**Emoji: none in the app UI.** The repo README uses 流 / ⚠️ / ✅ as Markdown
decoration; the product interface does not. Iconography carries meaning instead
(see below). The kanji **流** ("flow / current") is the brand's wordmark glyph,
used only in the name treatment "流 Nagare".

---

## VISUAL FOUNDATIONS

The aesthetic is **Fluent, flat, and quiet**. Neutrals do the work; one accent
per screen; color earns its place. "If everything is salient, nothing is."

**Color.** The Windows Fluent system palette, following the **system accent**
(shipped default: Windows blue `#0078D4`). Neutrals dominate every surface.
Accent (`--accent-fill`) marks exactly **one** element per screen — the primary
button (`Démarrer`, `Nouveau`, `Enregistrer`). **Red (`--critical`) is reserved
for a real anomaly** — `speed < 1.0x`, a failed session, a reconnection — never
decoration. Semantic colors are four: success (green), caution (amber), critical
(red), attention (blue), each with a matching soft background for InfoBars and
badges. Every color is a token / `{ThemeResource}`; **never hard-coded**.

**Themes.** Light, dark, and high-contrast must all be correct — tested, not
assumed. Light is the default; `.theme-dark` overrides; consumers linking
`styles.css` also get automatic `prefers-color-scheme` switching. In dark theme
the accent button flips to a light-blue fill with **black** text (real Fluent
behavior). Status must never rely on color alone — pair it with a shape, an icon,
and a word.

**Type.** Segoe UI Variable, on the WinUI text ramp: Caption 12 / Body 14 /
BodyStrong 14·600 / Subtitle 20·600 / Title 28·600 / TitleLarge 40·600 /
Display 68·600. Regular (400) for reading, SemiBold (600) for titles and
emphasis — there is no light or black weight in play. **Monospace (Consolas /
Cascadia Mono) is used deliberately** for the two "expert" surfaces: the ffmpeg
command preview and the log console — an OBS-like signal that this is raw truth.

**Spacing & layout.** A 4px grid. Page frame margin **24**; padding inside a
bounded card **16**; gap between cards **16**; vertical field rhythm **12**;
inline control gap **8**. The shell is a left `NavigationView` rail — with room
kept for a fourth item ("Planifications", iteration 2). The dashboard is cut into
**bounded cards** (Common Region): *Source*, *Diffusion*, *Santé*, *Journal*.
The primary action sits at the **end** of the configuration flow, where the eye
lands (Fitts's Law); a destructive action is **never** placed adjacent to it.

**Backgrounds & materials.** No gradients, no illustrations, no photography, no
patterns, no textures. The window backdrop is **Mica** (an opaque, desktop-tinted
material) — represented as the solid base `#F3F3F3` (light) / `#202020` (dark).
Flyouts and menus may use **Acrylic** (translucent + blur). Cards are a
translucent *layer fill* over Mica.

**Borders, radii, elevation.** Corner radius is **4px** for controls, **8px** for
overlays (cards, dialogs, flyouts). Surfaces are separated by **1px strokes**,
not shadows — cards are flat and read as regions via their stroke. Shadow is
reserved for things that float: flyouts/tooltips (`--shadow-flyout`) and the
`ContentDialog` (`--shadow-dialog`). Text inputs carry a stronger bottom edge
that becomes an **accent underline on focus** (Fluent's signature).

**Interaction states.** Hover lightens with a subtle fill (`--subtle-secondary`);
press flattens/darkens (`--subtle-tertiary` / accent `--accent-fill-tertiary`).
**No scaling, no bounce** — Fluent buttons don't shrink. Focus shows a visible
**2px ring** (`--focus-outer`) with a 1px inner gap, on every focusable element.

**Motion.** Fluent easings: standard `cubic-bezier(0.8,0,0.2,1)`, decelerate for
enters, accelerate for exits; durations 150 / 250 / 333 ms. Motion is functional
— InfoBar slide-in, ProgressRing spin, nav transition. **During a live broadcast
there is no modal, no focus theft, no blocking InfoBar** (Flow): a reconnection
announces itself without interrupting. All motion respects
`prefers-reduced-motion`.

**Imagery vibe.** There is none, by design. Nagare is instrumentation, not a
gallery. The closest thing to "imagery" is the monospaced log stream — cool,
neutral, high-density, OBS-like.

---

## ICONOGRAPHY

**Production set: Segoe Fluent Icons** — the Windows 11 system symbol font used
by WinUI (`FontIcon` / `SymbolIcon` with glyph codepoints). It is the correct,
Jakob's-Law-consistent choice: the same icons the OS and every native app use.
The current Nagare shell ships **without** icons; the redesign adds them to the
nav rail, to icon-only buttons (each with `AutomationProperties.Name`), and to
the status/health badge (icon + shape + text, never color alone).

Representative usage (pick exact glyphs from the Segoe Fluent Icons reference):
Tableau de bord (gauge), Profils (sliders), Channels (broadcast/tv),
Planifications (calendar-clock), Play, Stop, Choisir un fichier (folder-open),
Nouveau (plus), Modifier (pencil), Supprimer (trash), Copier (copy), and the
status glyphs success / caution / critical / info / reconnecting.

> **Substitution — flagged.** Segoe Fluent Icons is a Windows *system* font: not
> redistributable and not bundled here, so it cannot render in a cross-platform
> web preview. The specimen cards and UI kit in this system use **[Lucide](https://lucide.dev)**
> (MIT, CDN) as the nearest match — a clean, consistent 1.5–2px line set that
> reads like Fluent's "regular" weight. **Swap Lucide for Segoe Fluent Icons in
> the real WinUI app.** No emoji, no unicode-glyph icons, no hand-drawn SVG.

---

## Fonts

- **Sans:** `Segoe UI Variable` (optical axes: *Display* for large text, *Text*
  for body), falling back to `Segoe UI`, then the platform UI sans.
- **Mono:** `Cascadia Mono` / `Consolas`, falling back to the platform monospace.

Both are **Windows system fonts — not redistributable**, so there are no
`@font-face` rules and no font binaries in this project. On Windows the stacks
resolve to the real fonts; elsewhere the browser falls back to `system-ui` /
`ui-monospace`. This is faithful to a Windows-native app.

> ⚠️ **If you need pixel-accurate previews on non-Windows machines**, tell me and
> I'll wire up a licensed webfont or the closest open substitute (e.g. *Selawik*,
> Microsoft's own metric-compatible Segoe replacement). I did **not** substitute
> a Google font automatically, because doing so would misrepresent the Fluent look.

---

## Intentional additions

The component inventory is exactly the Fluent/WinUI controls the Nagare XAML uses
(Button, ComboBox, TextBox, PasswordBox, NumberBox, ToggleSwitch, InfoBar,
ProgressRing, ContentDialog, Card, NavigationView). Four components are **added**
to satisfy the UX brief's Laws-of-UX mandates — each built from Fluent primitives,
not invented controls:

- **StatusBadge** — health/status as *icon + shape + text*, replacing the bare
  colored `Ellipse` (accessibility §9; status never by color alone; Von Restorff).
- **StatTile** — the five live stats grouped into scannable tiles (Miller's Law /
  Chunking) instead of a flat row.
- **LaunchChecklist** — Environnement ✓ · Fichier ✓ · Profil ✓ · Channel ✓, so a
  disabled **Démarrer** always says *what is missing* without a click
  (Zeigarnik / Goal-Gradient; fixes the brief's worst gap).
- **EmptyState** — the first-run documentation-as-UI pattern (Paradox of the
  Active User).

---

## Index

**Namespace** for components in `@dsCard` / kit HTML:
`const { Button, … } = window.NagareDesignSystem_9475eb`.

**Root**
- `styles.css` — the single entry point consumers link (imports only).
- `readme.md` — this guide. · `SKILL.md` — portable Agent-Skill wrapper.

**tokens/** — `colors.css` · `typography.css` · `spacing.css` · `elevation.css`

**components/** — React primitives (each has `.jsx` + `.d.ts` + `.prompt.md`; one
`@dsCard` per directory). Styling ships in `components/nagare-components.css`.
- `icon/` — **Icon**
- `buttons/` — **Button**, **IconButton**
- `inputs/` — **TextBox**, **PasswordBox**, **NumberBox**, **ComboBox**, **ToggleSwitch**
- `feedback/` — **InfoBar**, **ProgressRing**, **ContentDialog**
- `surface/` — **Card**, **EmptyState**
- `navigation/` — **NavRail**
- `streaming/` — **StatusBadge**, **StatTile**, **LaunchChecklist**

**guidelines/** — foundation specimen cards: Colors (accent, neutrals, text,
surfaces, semantic, dark), Type (ramp, mono, families), Spacing (scale, radii,
metrics), Brand (wordmark, iconography, motion, elevation).

**ui_kits/nagare-app/** — interactive recreation of the redesigned app
(`index.html` + shell + Dashboard / Profils / Channels). See its `README.md`.

**assets/icons/** — the Lucide glyph set (ISC), copied in as the web substitute
for Segoe Fluent Icons and inlined by `Icon`.

## Caveats

- **Fonts** (Segoe UI Variable, Cascadia Mono) and **icons** (Segoe Fluent Icons)
  are Windows system resources, not redistributable — substituted for web preview
  and flagged above. Swap them back in the real WinUI app.
- Values mirror the real Fluent 2 / WinUI theme resources and the Nagare code; the
  **accent ramp is the Windows default blue** — a real install follows the user's
  system accent.
- The UI kit renders the **redesign target**, not the current shipping shell.
