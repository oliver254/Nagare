---
name: nagare-design
description: Use this skill to generate well-branded interfaces and assets for Nagare (a Windows-native FFmpeg restreamer for Twitch / YouTube / RTMP), either for production or throwaway prototypes/mocks/etc. Contains essential design guidelines, colors, type, fonts, assets, and UI kit components for prototyping.
user-invocable: true
---

Read the README.md file within this skill, and explore the other available files.
If creating visual artifacts (slides, mocks, throwaway prototypes, etc), copy assets out and create static HTML files for the user to view. If working on production code, you can copy assets and read the rules here to become an expert in designing with this brand.
If the user invokes this skill without any other guidance, ask them what they want to build or design, ask some questions, and act as an expert designer who outputs HTML artifacts _or_ production code, depending on the need.

Nagare specifics worth loading first:
- It is a **Fluent Design (Windows 11)** app — behave like a native Windows app (NavigationView, Mica, InfoBar, ContentDialog, follow the system theme & accent) and borrow OBS landmarks (stream key, bitrate, "en direct", log console). Never invent bespoke navigation or controls.
- **UI in French, code in English.** One accent element per screen (the primary button); red only for a real anomaly. Never re-show the stream key.
- Tokens are in `tokens/*.css` (linked via `styles.css`); components are React primitives under `components/` (namespace `window.NagareDesignSystem_9475eb`); the full app recreation is in `ui_kits/nagare-app/`.
- Fonts (Segoe UI Variable, Cascadia Mono) and icons (Segoe Fluent Icons) are Windows system resources — the web substitutes here are flagged in README.md; use the real ones in production.
