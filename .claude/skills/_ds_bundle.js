/* @ds-bundle: {"format":4,"namespace":"NagareDesignSystem_9475eb","components":[{"name":"Button","sourcePath":"components/buttons/Button.jsx"},{"name":"IconButton","sourcePath":"components/buttons/IconButton.jsx"},{"name":"ContentDialog","sourcePath":"components/feedback/ContentDialog.jsx"},{"name":"InfoBar","sourcePath":"components/feedback/InfoBar.jsx"},{"name":"ProgressRing","sourcePath":"components/feedback/ProgressRing.jsx"},{"name":"Icon","sourcePath":"components/icon/Icon.jsx"},{"name":"ICON_PATHS","sourcePath":"components/icon/icon-paths.js"},{"name":"ComboBox","sourcePath":"components/inputs/ComboBox.jsx"},{"name":"NumberBox","sourcePath":"components/inputs/NumberBox.jsx"},{"name":"PasswordBox","sourcePath":"components/inputs/PasswordBox.jsx"},{"name":"TextBox","sourcePath":"components/inputs/TextBox.jsx"},{"name":"ToggleSwitch","sourcePath":"components/inputs/ToggleSwitch.jsx"},{"name":"NavRail","sourcePath":"components/navigation/NavRail.jsx"},{"name":"LaunchChecklist","sourcePath":"components/streaming/LaunchChecklist.jsx"},{"name":"StatTile","sourcePath":"components/streaming/StatTile.jsx"},{"name":"StatusBadge","sourcePath":"components/streaming/StatusBadge.jsx"},{"name":"Card","sourcePath":"components/surface/Card.jsx"},{"name":"EmptyState","sourcePath":"components/surface/EmptyState.jsx"}],"sourceHashes":{"components/buttons/Button.jsx":"afb4c144f900","components/buttons/IconButton.jsx":"6e58e2d17543","components/feedback/ContentDialog.jsx":"a28796286901","components/feedback/InfoBar.jsx":"b030c240c551","components/feedback/ProgressRing.jsx":"06337301013c","components/icon/Icon.jsx":"9c54bfaa9e7a","components/icon/icon-paths.js":"10dfaa69c2a5","components/inputs/ComboBox.jsx":"40921fa1c71e","components/inputs/NumberBox.jsx":"dcffb2982c59","components/inputs/PasswordBox.jsx":"09774f33d023","components/inputs/TextBox.jsx":"198ba10e672f","components/inputs/ToggleSwitch.jsx":"c33700a4d89c","components/navigation/NavRail.jsx":"25af66b092e9","components/streaming/LaunchChecklist.jsx":"4f92a6aa6313","components/streaming/StatTile.jsx":"3d7fb2cfb2a1","components/streaming/StatusBadge.jsx":"9f034a128848","components/surface/Card.jsx":"a64727a0c8bb","components/surface/EmptyState.jsx":"3a0dbc52078c","ui_kits/nagare-app/AppShell.jsx":"fc50d01cbcca","ui_kits/nagare-app/ChannelsPage.jsx":"95bee159926e","ui_kits/nagare-app/DashboardPage.jsx":"f44c9537ccb2","ui_kits/nagare-app/ProfilesPage.jsx":"140668aa3d03","ui_kits/nagare-app/store.js":"fc529f136958"},"inlinedExternals":[],"unexposedExports":[]} */

(() => {

const __ds_ns = (window.NagareDesignSystem_9475eb = window.NagareDesignSystem_9475eb || {});

const __ds_scope = {};

(__ds_ns.__errors = __ds_ns.__errors || []);

// components/feedback/ProgressRing.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * ProgressRing — Fluent indeterminate spinner. Bind it to ViewModelBase.IsBusy
 * so nothing crosses the 400 ms Doherty threshold in silence (environment probe,
 * ffprobe). Optionally pairs with a "Démarrage…" style label.
 */
function ProgressRing({
  size = 32,
  thickness,
  label,
  className = "",
  ...rest
}) {
  const t = thickness || Math.max(2, Math.round(size / 10));
  const ring = /*#__PURE__*/React.createElement("span", _extends({
    className: `n-ring ${className}`.trim(),
    style: {
      width: size,
      height: size,
      borderWidth: t
    },
    role: "progressbar",
    "aria-label": label || "Chargement"
  }, rest));
  if (label) {
    return /*#__PURE__*/React.createElement("span", {
      className: "n-busy"
    }, ring, /*#__PURE__*/React.createElement("span", null, label));
  }
  return ring;
}
Object.assign(__ds_scope, { ProgressRing });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/ProgressRing.jsx", error: String((e && e.message) || e) }); }

// components/icon/icon-paths.js
try { (() => {
// Auto-generated from Lucide (github.com/lucide-icons/lucide, ISC license).
// Inner SVG markup per glyph, inlined so icons tint with currentColor and work offline.
// Production Nagare maps these names to Segoe Fluent Icons instead.
const ICON_PATHS = {
  "activity": "<path d=\"M22 12h-2.48a2 2 0 0 0-1.93 1.46l-2.35 8.36a.25.25 0 0 1-.48 0L9.24 2.18a.25.25 0 0 0-.48 0l-2.35 8.36A2 2 0 0 1 4.49 12H2\"></path>",
  "calendar-clock": "<path d=\"M16 14v2.2l1.6 1\"></path> <path d=\"M16 2v4\"></path> <path d=\"M21 7.5V6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h3.5\"></path> <path d=\"M3 10h5\"></path> <path d=\"M8 2v4\"></path> <circle cx=\"16\" cy=\"16\" r=\"6\"></circle>",
  "cast": "<path d=\"M2 8V6a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-6\"></path> <path d=\"M2 12a9 9 0 0 1 8 8\"></path> <path d=\"M2 16a5 5 0 0 1 4 4\"></path> <line x1=\"2\" x2=\"2.01\" y1=\"20\" y2=\"20\"></line>",
  "check": "<path d=\"M20 6 9 17l-5-5\"></path>",
  "chevron-down": "<path d=\"m6 9 6 6 6-6\"></path>",
  "chevron-left": "<path d=\"m15 18-6-6 6-6\"></path>",
  "chevron-right": "<path d=\"m9 18 6-6-6-6\"></path>",
  "chevron-up": "<path d=\"m18 15-6-6-6 6\"></path>",
  "circle": "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle>",
  "circle-check": "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle> <path d=\"m9 12 2 2 4-4\"></path>",
  "circle-stop": "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle> <rect x=\"9\" y=\"9\" width=\"6\" height=\"6\" rx=\"1\"></rect>",
  "circle-x": "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle> <path d=\"m15 9-6 6\"></path> <path d=\"m9 9 6 6\"></path>",
  "clock": "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle> <path d=\"M12 6v6l4 2\"></path>",
  "copy": "<rect width=\"14\" height=\"14\" x=\"8\" y=\"8\" rx=\"2\" ry=\"2\"></rect> <path d=\"M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2\"></path>",
  "eye": "<path d=\"M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0\"></path> <circle cx=\"12\" cy=\"12\" r=\"3\"></circle>",
  "eye-off": "<path d=\"M10.733 5.076a10.744 10.744 0 0 1 11.205 6.575 1 1 0 0 1 0 .696 10.747 10.747 0 0 1-1.444 2.49\"></path> <path d=\"M14.084 14.158a3 3 0 0 1-4.242-4.242\"></path> <path d=\"M17.479 17.499a10.75 10.75 0 0 1-15.417-5.151 1 1 0 0 1 0-.696 10.75 10.75 0 0 1 4.446-5.143\"></path> <path d=\"m2 2 20 20\"></path>",
  "film": "<rect width=\"18\" height=\"18\" x=\"3\" y=\"3\" rx=\"2\"></rect> <path d=\"M7 3v18\"></path> <path d=\"M3 7.5h4\"></path> <path d=\"M3 12h18\"></path> <path d=\"M3 16.5h4\"></path> <path d=\"M17 3v18\"></path> <path d=\"M17 7.5h4\"></path> <path d=\"M17 16.5h4\"></path>",
  "folder-open": "<path d=\"m6 14 1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2\"></path>",
  "gauge": "<path d=\"m12 14 4-4\"></path> <path d=\"M3.34 19a10 10 0 1 1 17.32 0\"></path>",
  "hard-drive": "<path d=\"M10 16h.01\"></path> <path d=\"M2.212 11.577a2 2 0 0 0-.212.896V18a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-5.527a2 2 0 0 0-.212-.896L18.55 5.11A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z\"></path> <path d=\"M21.946 12.013H2.054\"></path> <path d=\"M6 16h.01\"></path>",
  "inbox": "<polyline points=\"22 12 16 12 14 15 10 15 8 12 2 12\"></polyline> <path d=\"M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z\"></path>",
  "info": "<circle cx=\"12\" cy=\"12\" r=\"10\"></circle> <path d=\"M12 16v-4\"></path> <path d=\"M12 8h.01\"></path>",
  "list": "<path d=\"M3 5h.01\"></path> <path d=\"M3 12h.01\"></path> <path d=\"M3 19h.01\"></path> <path d=\"M8 5h13\"></path> <path d=\"M8 12h13\"></path> <path d=\"M8 19h13\"></path>",
  "monitor": "<rect width=\"20\" height=\"14\" x=\"2\" y=\"3\" rx=\"2\"></rect> <line x1=\"8\" x2=\"16\" y1=\"21\" y2=\"21\"></line> <line x1=\"12\" x2=\"12\" y1=\"17\" y2=\"21\"></line>",
  "octagon-alert": "<path d=\"M12 16h.01\"></path> <path d=\"M12 8v4\"></path> <path d=\"M15.312 2a2 2 0 0 1 1.414.586l4.688 4.688A2 2 0 0 1 22 8.688v6.624a2 2 0 0 1-.586 1.414l-4.688 4.688a2 2 0 0 1-1.414.586H8.688a2 2 0 0 1-1.414-.586l-4.688-4.688A2 2 0 0 1 2 15.312V8.688a2 2 0 0 1 .586-1.414l4.688-4.688A2 2 0 0 1 8.688 2z\"></path>",
  "pause": "<rect x=\"14\" y=\"3\" width=\"5\" height=\"18\" rx=\"1\"></rect> <rect x=\"5\" y=\"3\" width=\"5\" height=\"18\" rx=\"1\"></rect>",
  "pencil": "<path d=\"M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z\"></path> <path d=\"m15 5 4 4\"></path>",
  "play": "<path d=\"M5 5a2 2 0 0 1 3.008-1.728l11.997 6.998a2 2 0 0 1 .003 3.458l-12 7A2 2 0 0 1 5 19z\"></path>",
  "plus": "<path d=\"M5 12h14\"></path> <path d=\"M12 5v14\"></path>",
  "radio": "<path d=\"M16.247 7.761a6 6 0 0 1 0 8.478\"></path> <path d=\"M19.075 4.933a10 10 0 0 1 0 14.134\"></path> <path d=\"M4.925 19.067a10 10 0 0 1 0-14.134\"></path> <path d=\"M7.753 16.239a6 6 0 0 1 0-8.478\"></path> <circle cx=\"12\" cy=\"12\" r=\"2\"></circle>",
  "radio-tower": "<path d=\"M4.9 16.1C1 12.2 1 5.8 4.9 1.9\"></path> <path d=\"M7.8 4.7a6.14 6.14 0 0 0-.8 7.5\"></path> <circle cx=\"12\" cy=\"9\" r=\"2\"></circle> <path d=\"M16.2 4.8c2 2 2.26 5.11.8 7.47\"></path> <path d=\"M19.1 1.9a9.96 9.96 0 0 1 0 14.1\"></path> <path d=\"M9.5 18h5\"></path> <path d=\"m8 22 4-11 4 11\"></path>",
  "refresh-cw": "<path d=\"M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8\"></path> <path d=\"M21 3v5h-5\"></path> <path d=\"M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16\"></path> <path d=\"M8 16H3v5\"></path>",
  "rotate-cw": "<path d=\"M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8\"></path> <path d=\"M21 3v5h-5\"></path>",
  "search": "<path d=\"m21 21-4.34-4.34\"></path> <circle cx=\"11\" cy=\"11\" r=\"8\"></circle>",
  "settings": "<path d=\"M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915\"></path> <circle cx=\"12\" cy=\"12\" r=\"3\"></circle>",
  "signal": "<path d=\"M2 20h.01\"></path> <path d=\"M7 20v-4\"></path> <path d=\"M12 20v-8\"></path> <path d=\"M17 20V8\"></path> <path d=\"M22 4v16\"></path>",
  "sliders-horizontal": "<path d=\"M10 5H3\"></path> <path d=\"M12 19H3\"></path> <path d=\"M14 3v4\"></path> <path d=\"M16 17v4\"></path> <path d=\"M21 12h-9\"></path> <path d=\"M21 19h-5\"></path> <path d=\"M21 5h-7\"></path> <path d=\"M8 10v4\"></path> <path d=\"M8 12H3\"></path>",
  "square": "<rect width=\"18\" height=\"18\" x=\"3\" y=\"3\" rx=\"2\"></rect>",
  "terminal": "<path d=\"M12 19h8\"></path> <path d=\"m4 17 6-6-6-6\"></path>",
  "trash-2": "<path d=\"M10 11v6\"></path> <path d=\"M14 11v6\"></path> <path d=\"M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6\"></path> <path d=\"M3 6h18\"></path> <path d=\"M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2\"></path>",
  "triangle-alert": "<path d=\"m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3\"></path> <path d=\"M12 9v4\"></path> <path d=\"M12 17h.01\"></path>",
  "upload": "<path d=\"M12 3v12\"></path> <path d=\"m17 8-5-5-5 5\"></path> <path d=\"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4\"></path>",
  "video": "<path d=\"m16 13 5.223 3.482a.5.5 0 0 0 .777-.416V7.87a.5.5 0 0 0-.752-.432L16 10.5\"></path> <rect x=\"2\" y=\"6\" width=\"14\" height=\"12\" rx=\"2\"></rect>",
  "x": "<path d=\"M18 6 6 18\"></path> <path d=\"m6 6 12 12\"></path>",
  "zap": "<path d=\"M4 14a1 1 0 0 1-.78-1.63l9.9-10.2a.5.5 0 0 1 .86.46l-1.92 6.02A1 1 0 0 0 13 10h7a1 1 0 0 1 .78 1.63l-9.9 10.2a.5.5 0 0 1-.86-.46l1.92-6.02A1 1 0 0 0 11 14z\"></path>"
};
Object.assign(__ds_scope, { ICON_PATHS });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/icon/icon-paths.js", error: String((e && e.message) || e) }); }

// components/icon/Icon.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * Icon — Nagare's glyph wrapper.
 *
 * Production Nagare uses Segoe Fluent Icons (the Windows system symbol font, via
 * WinUI FontIcon). That font isn't redistributable, so on the web this renders
 * the nearest match — a Lucide glyph — as inline SVG that tints with the current
 * text color (works in dark theme, on accent, and for status colors; no network).
 *
 * Swap the glyph source for Segoe Fluent Icons in the real app.
 */
function Icon({
  name,
  size = 16,
  color = "currentColor",
  strokeWidth = 2,
  label,
  className = "",
  style = {},
  ...rest
}) {
  const inner = __ds_scope.ICON_PATHS[name];
  if (inner === undefined && typeof console !== "undefined") {
    console.warn(`Icon: unknown glyph "${name}"`);
  }
  const dim = typeof size === "number" ? `${size}px` : size;
  const a11y = label ? {
    role: "img",
    "aria-label": label
  } : {
    "aria-hidden": "true",
    focusable: "false"
  };
  return /*#__PURE__*/React.createElement("svg", _extends({}, a11y, {
    className: `n-icon ${className}`.trim(),
    width: dim,
    height: dim,
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: color,
    strokeWidth: strokeWidth,
    strokeLinecap: "round",
    strokeLinejoin: "round",
    style: style,
    dangerouslySetInnerHTML: {
      __html: inner || ""
    }
  }, rest));
}
Object.assign(__ds_scope, { Icon });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/icon/Icon.jsx", error: String((e && e.message) || e) }); }

// components/buttons/Button.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * Button — Fluent/WinUI button.
 *
 * Von Restorff: exactly ONE accent button per screen (the primary action —
 * Démarrer / Enregistrer / Nouveau). Everything else is `standard` or `subtle`.
 * A destructive action is never styled as the accent and never sits next to it.
 */
function Button({
  variant = "standard",
  size = "standard",
  icon,
  iconPosition = "start",
  disabled = false,
  children,
  className = "",
  type = "button",
  ...rest
}) {
  const cls = ["n-btn", variant !== "standard" && `n-btn--${variant}`, size === "small" && "n-btn--sm", className].filter(Boolean).join(" ");
  const glyph = icon ? typeof icon === "string" ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    size: size === "small" ? 14 : 16
  }) : icon : null;
  return /*#__PURE__*/React.createElement("button", _extends({
    type: type,
    className: cls,
    disabled: disabled
  }, rest), iconPosition === "start" && glyph, children != null && children !== "" && /*#__PURE__*/React.createElement("span", {
    className: "n-btn__label"
  }, children), iconPosition === "end" && glyph);
}
Object.assign(__ds_scope, { Button });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/buttons/Button.jsx", error: String((e && e.message) || e) }); }

// components/buttons/IconButton.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * IconButton — a square, icon-only button (toolbar / list-row actions).
 *
 * `label` is REQUIRED: it becomes both `aria-label` and the tooltip. This is the
 * accessibility rule from the brief — every icon button carries an
 * AutomationProperties.Name. Default variant is `subtle`.
 */
function IconButton({
  icon,
  label,
  variant = "subtle",
  disabled = false,
  className = "",
  ...rest
}) {
  const cls = ["n-btn", "n-iconbtn", variant !== "standard" && `n-btn--${variant}`, className].filter(Boolean).join(" ");
  return /*#__PURE__*/React.createElement("button", _extends({
    type: "button",
    className: cls,
    "aria-label": label,
    title: label,
    disabled: disabled
  }, rest), typeof icon === "string" ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    size: 16
  }) : icon);
}
Object.assign(__ds_scope, { IconButton });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/buttons/IconButton.jsx", error: String((e && e.message) || e) }); }

// components/feedback/ContentDialog.jsx
try { (() => {
/**
 * ContentDialog — Fluent modal dialog. Nagare's ONLY modal, used for destructive
 * confirmation (delete a profile / channel). Confirmation must NAME the object
 * being deleted, and the destructive button is tinted `danger`, never the accent
 * (the accent stays with the safe/affirmative or with Annuler).
 *
 * FLOW: never show a modal during a live broadcast. Confirmations belong to
 * management screens (Profils / Channels), not the dashboard mid-stream.
 */
function ContentDialog({
  open = true,
  title,
  children,
  primaryText,
  secondaryText,
  closeText,
  primaryVariant = "accent",
  onPrimary,
  onSecondary,
  onClose,
  contained = false,
  className = ""
}) {
  if (!open) return null;
  return /*#__PURE__*/React.createElement("div", {
    className: "n-scrim" + (contained ? " n-scrim--contained" : ""),
    role: "presentation",
    onClick: e => {
      if (e.target === e.currentTarget && onClose) onClose();
    }
  }, /*#__PURE__*/React.createElement("div", {
    className: `n-dialog ${className}`.trim(),
    role: "dialog",
    "aria-modal": "true",
    "aria-label": title
  }, /*#__PURE__*/React.createElement("div", {
    className: "n-dialog__body"
  }, title && /*#__PURE__*/React.createElement("h2", {
    className: "n-dialog__title"
  }, title), children && /*#__PURE__*/React.createElement("div", {
    className: "n-dialog__content"
  }, children)), (primaryText || secondaryText || closeText) && /*#__PURE__*/React.createElement("div", {
    className: "n-dialog__commands"
  }, primaryText && /*#__PURE__*/React.createElement(__ds_scope.Button, {
    variant: primaryVariant,
    onClick: onPrimary
  }, primaryText), secondaryText && /*#__PURE__*/React.createElement(__ds_scope.Button, {
    onClick: onSecondary
  }, secondaryText), closeText && /*#__PURE__*/React.createElement(__ds_scope.Button, {
    variant: "subtle",
    onClick: onClose
  }, closeText))));
}
Object.assign(__ds_scope, { ContentDialog });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/ContentDialog.jsx", error: String((e && e.message) || e) }); }

// components/feedback/InfoBar.jsx
try { (() => {
const SEVERITY_ICON = {
  informational: "info",
  success: "circle-check",
  warning: "triangle-alert",
  error: "octagon-alert"
};

/**
 * InfoBar — Fluent inline message (informational / success / warning / error),
 * fed by the ViewModel's ErrorMessage / EnvironmentIssue.
 *
 * PLACEMENT MATTERS (Selective Attention): put a critical message IN the flow of
 * the task it concerns, not only in a tall banner users learn to ignore. And
 * DURING a live broadcast, an InfoBar must never be blocking or steal focus
 * (Flow) — a reconnection announces itself without interrupting.
 */
function InfoBar({
  severity = "informational",
  title,
  message,
  isClosable = false,
  onClose,
  actions,
  children,
  className = ""
}) {
  const cls = ["n-infobar", severity !== "informational" && `n-infobar--${severity}`, className].filter(Boolean).join(" ");
  return /*#__PURE__*/React.createElement("div", {
    className: cls,
    role: severity === "error" ? "alert" : "status"
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    className: "n-infobar__icon",
    name: SEVERITY_ICON[severity],
    size: 18
  }), /*#__PURE__*/React.createElement("div", {
    className: "n-infobar__body"
  }, title && /*#__PURE__*/React.createElement("span", {
    className: "n-infobar__title"
  }, title), message && /*#__PURE__*/React.createElement("span", {
    className: "n-infobar__msg"
  }, message), children, actions && /*#__PURE__*/React.createElement("div", {
    className: "n-infobar__actions"
  }, actions)), isClosable && /*#__PURE__*/React.createElement(__ds_scope.IconButton, {
    icon: "x",
    label: "Fermer",
    onClick: onClose
  }));
}
Object.assign(__ds_scope, { InfoBar });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/feedback/InfoBar.jsx", error: String((e && e.message) || e) }); }

// components/inputs/ComboBox.jsx
try { (() => {
/**
 * ComboBox — Fluent dropdown select with an optional header label. The popup is
 * an Acrylic flyout; the selected option shows an accent bar + check.
 *
 * The list of options is READ FROM the domain (presets per codec, sample rates,
 * platforms, enums) — never a second hard-coded copy of a domain rule.
 */
function ComboBox({
  header,
  hint,
  items = [],
  value,
  onChange,
  placeholder = "Sélectionner…",
  disabled = false,
  id,
  className = ""
}) {
  const reactId = React.useId();
  const fieldId = id || reactId;
  const [open, setOpen] = React.useState(false);
  const rootRef = React.useRef(null);
  const options = items.map(it => it && typeof it === "object" ? it : {
    value: it,
    label: String(it)
  });
  const selected = options.find(o => o.value === value);
  React.useEffect(() => {
    if (!open) return;
    const onDoc = e => {
      if (rootRef.current && !rootRef.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, [open]);
  const pick = o => {
    setOpen(false);
    if (onChange) onChange(o.value);
  };
  return /*#__PURE__*/React.createElement("div", {
    className: `n-field ${className}`.trim()
  }, header && /*#__PURE__*/React.createElement("label", {
    className: "n-field__label",
    htmlFor: fieldId
  }, header), /*#__PURE__*/React.createElement("div", {
    className: "n-combo",
    ref: rootRef
  }, /*#__PURE__*/React.createElement("button", {
    id: fieldId,
    type: "button",
    className: "n-combo__button",
    "aria-haspopup": "listbox",
    "aria-expanded": open,
    "aria-disabled": disabled || undefined,
    onClick: () => !disabled && setOpen(o => !o)
  }, /*#__PURE__*/React.createElement("span", {
    className: "n-combo__value" + (selected ? "" : " n-combo__value--placeholder")
  }, selected ? selected.label : placeholder), /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    className: "n-combo__chev",
    name: "chevron-down",
    size: 16
  })), open && /*#__PURE__*/React.createElement("ul", {
    className: "n-combo__pop",
    role: "listbox"
  }, options.map(o => /*#__PURE__*/React.createElement("li", {
    key: String(o.value),
    role: "option",
    "aria-selected": o.value === value,
    className: "n-combo__opt" + (o.value === value ? " n-combo__opt--selected" : ""),
    onClick: () => pick(o)
  }, /*#__PURE__*/React.createElement("span", null, o.label), o.value === value && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "check",
    size: 16,
    style: {
      marginLeft: "auto"
    }
  }))))), hint && /*#__PURE__*/React.createElement("span", {
    className: "n-field__hint"
  }, hint));
}
Object.assign(__ds_scope, { ComboBox });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/inputs/ComboBox.jsx", error: String((e && e.message) || e) }); }

// components/inputs/NumberBox.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * NumberBox — Fluent number field with compact spin buttons (WinUI NumberBox,
 * SpinButtonPlacementMode="Compact"). Used across the profile editor
 * (bitrate / maxrate / bufsize / GOP / keyint / resolution / fps).
 *
 * `onChange(value: number)` receives the parsed, clamped number — not a raw
 * event. Range enforcement here is only convenience; the DOMAIN owns the real
 * invariants (E1-E8) and its message goes into `error`.
 */
function NumberBox({
  header,
  hint,
  error,
  min,
  max,
  step = 1,
  value,
  defaultValue = 0,
  onChange,
  disabled = false,
  id,
  className = "",
  ...rest
}) {
  const reactId = React.useId();
  const fieldId = id || reactId;
  const controlled = value !== undefined;
  const [internal, setInternal] = React.useState(defaultValue);
  const current = controlled ? value : internal;
  const clamp = n => {
    if (min != null && n < min) n = min;
    if (max != null && n > max) n = max;
    return n;
  };
  const set = n => {
    const c = clamp(Number.isNaN(n) ? 0 : n);
    if (!controlled) setInternal(c);
    if (onChange) onChange(c);
  };
  return /*#__PURE__*/React.createElement("div", {
    className: "n-field"
  }, header && /*#__PURE__*/React.createElement("label", {
    className: "n-field__label",
    htmlFor: fieldId
  }, header), /*#__PURE__*/React.createElement("div", {
    className: "n-inputwrap n-inputwrap--num"
  }, /*#__PURE__*/React.createElement("input", _extends({
    id: fieldId,
    className: `n-input ${className}`.trim(),
    type: "text",
    inputMode: "numeric",
    value: current,
    disabled: disabled,
    "aria-invalid": error ? true : undefined,
    onChange: e => set(parseFloat(e.target.value))
  }, rest)), /*#__PURE__*/React.createElement("div", {
    className: "n-input-affix"
  }, /*#__PURE__*/React.createElement("div", {
    className: "n-spin"
  }, /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: "n-spin__btn",
    tabIndex: -1,
    "aria-label": "Augmenter",
    disabled: disabled,
    onClick: () => set(current + step)
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "chevron-up",
    size: 12
  })), /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: "n-spin__btn",
    tabIndex: -1,
    "aria-label": "Diminuer",
    disabled: disabled,
    onClick: () => set(current - step)
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: "chevron-down",
    size: 12
  }))))), error ? /*#__PURE__*/React.createElement("span", {
    className: "n-field__hint n-field__hint--error"
  }, error) : hint ? /*#__PURE__*/React.createElement("span", {
    className: "n-field__hint"
  }, hint) : null);
}
Object.assign(__ds_scope, { NumberBox });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/inputs/NumberBox.jsx", error: String((e && e.message) || e) }); }

// components/inputs/PasswordBox.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * PasswordBox — Fluent password field for the stream key.
 *
 * SECURITY (ADR-0005): a SAVED key is never loaded back into this box — the DTO
 * doesn't carry it and no query can return it. The reveal toggle only unmasks
 * what the user is typing right now; an empty field on save means "keep the
 * current key". Never pre-fill, never log, never copy the key.
 */
function PasswordBox({
  header,
  hint,
  revealable = true,
  id,
  className = "",
  ...rest
}) {
  const reactId = React.useId();
  const fieldId = id || reactId;
  const [reveal, setReveal] = React.useState(false);
  return /*#__PURE__*/React.createElement("div", {
    className: "n-field"
  }, header && /*#__PURE__*/React.createElement("label", {
    className: "n-field__label",
    htmlFor: fieldId
  }, header), /*#__PURE__*/React.createElement("div", {
    className: "n-inputwrap"
  }, /*#__PURE__*/React.createElement("input", _extends({
    id: fieldId,
    className: `n-input ${className}`.trim(),
    type: reveal ? "text" : "password"
  }, rest)), revealable && /*#__PURE__*/React.createElement("div", {
    className: "n-input-affix"
  }, /*#__PURE__*/React.createElement("button", {
    type: "button",
    className: "n-reveal",
    "aria-label": reveal ? "Masquer la clé" : "Afficher la clé",
    "aria-pressed": reveal,
    onMouseDown: e => e.preventDefault(),
    onClick: () => setReveal(v => !v)
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: reveal ? "eye-off" : "eye",
    size: 16
  })))), hint && /*#__PURE__*/React.createElement("span", {
    className: "n-field__hint"
  }, hint));
}
Object.assign(__ds_scope, { PasswordBox });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/inputs/PasswordBox.jsx", error: String((e && e.message) || e) }); }

// components/inputs/TextBox.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * TextBox — Fluent single- or multi-line text field with an optional header
 * label (WinUI TextBox `Header`). Set `mono` for command/URL fields.
 *
 * Validation is never re-authored in the UI: pass the domain's message straight
 * into `error` (e.g. a DomainException text). This is a thin presentational field.
 */
function TextBox({
  header,
  hint,
  error,
  multiline = false,
  mono = false,
  id,
  className = "",
  ...rest
}) {
  const reactId = React.useId();
  const fieldId = id || reactId;
  const Tag = multiline ? "textarea" : "input";
  const inputCls = ["n-input", mono && "n-input--mono", className].filter(Boolean).join(" ");
  return /*#__PURE__*/React.createElement("div", {
    className: "n-field"
  }, header && /*#__PURE__*/React.createElement("label", {
    className: "n-field__label",
    htmlFor: fieldId
  }, header), /*#__PURE__*/React.createElement(Tag, _extends({
    id: fieldId,
    className: inputCls,
    "aria-invalid": error ? true : undefined
  }, multiline ? {} : {
    type: "text"
  }, rest)), error ? /*#__PURE__*/React.createElement("span", {
    className: "n-field__hint n-field__hint--error"
  }, error) : hint ? /*#__PURE__*/React.createElement("span", {
    className: "n-field__hint"
  }, hint) : null);
}
Object.assign(__ds_scope, { TextBox });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/inputs/TextBox.jsx", error: String((e && e.message) || e) }); }

// components/inputs/ToggleSwitch.jsx
try { (() => {
/**
 * ToggleSwitch — Fluent on/off switch with an optional header. Used for the
 * profile editor's boolean options (force resolution, force fps, -re,
 * -stream_loop -1). `onChange(checked: boolean)`.
 */
function ToggleSwitch({
  header,
  label,
  onText = "Activé",
  offText = "Désactivé",
  showStateText = false,
  checked,
  defaultChecked = false,
  onChange,
  disabled = false,
  id,
  className = ""
}) {
  const reactId = React.useId();
  const fieldId = id || reactId;
  const controlled = checked !== undefined;
  const [internal, setInternal] = React.useState(defaultChecked);
  const isOn = controlled ? checked : internal;
  const toggle = e => {
    const v = e.target.checked;
    if (!controlled) setInternal(v);
    if (onChange) onChange(v);
  };
  const text = label != null ? label : showStateText ? isOn ? onText : offText : null;
  return /*#__PURE__*/React.createElement("div", {
    className: `n-field ${className}`.trim()
  }, header && /*#__PURE__*/React.createElement("span", {
    className: "n-field__label"
  }, header), /*#__PURE__*/React.createElement("label", {
    className: "n-toggle",
    "aria-disabled": disabled || undefined
  }, /*#__PURE__*/React.createElement("input", {
    id: fieldId,
    type: "checkbox",
    role: "switch",
    checked: isOn,
    disabled: disabled,
    onChange: toggle
  }), /*#__PURE__*/React.createElement("span", {
    className: "n-toggle__track"
  }, /*#__PURE__*/React.createElement("span", {
    className: "n-toggle__knob"
  })), text != null && /*#__PURE__*/React.createElement("span", {
    className: "n-toggle__label"
  }, text)));
}
Object.assign(__ds_scope, { ToggleSwitch });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/inputs/ToggleSwitch.jsx", error: String((e && e.message) || e) }); }

// components/navigation/NavRail.jsx
try { (() => {
/**
 * NavRail — the Fluent NavigationView left pane (Jakob's Law: no bespoke nav).
 * Three destinations today — Tableau de bord / Profils / Channels — with a slot
 * kept for a fourth, Planifications (iteration 2), shown disabled with a
 * "Bientôt" tag so its place is reserved.
 */
function NavRail({
  items = [],
  selected,
  onSelect,
  brand = "Nagare",
  brandMark = "流",
  footer,
  className = ""
}) {
  return /*#__PURE__*/React.createElement("nav", {
    className: `n-nav ${className}`.trim(),
    "aria-label": "Navigation principale"
  }, /*#__PURE__*/React.createElement("div", {
    className: "n-nav__brand"
  }, /*#__PURE__*/React.createElement("span", {
    className: "n-nav__brand-mark",
    "aria-hidden": "true"
  }, brandMark), /*#__PURE__*/React.createElement("span", {
    className: "n-nav__brand-name"
  }, brand)), items.map(it => {
    const isSel = it.tag === selected;
    return /*#__PURE__*/React.createElement("button", {
      key: it.tag,
      type: "button",
      className: "n-nav__item" + (isSel ? " n-nav__item--selected" : ""),
      "aria-current": isSel ? "page" : undefined,
      "aria-disabled": it.disabled || undefined,
      onClick: () => !it.disabled && onSelect && onSelect(it.tag)
    }, it.icon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
      className: "n-nav__item-icon",
      name: it.icon,
      size: 18
    }), /*#__PURE__*/React.createElement("span", {
      className: "n-nav__item-label"
    }, it.label), it.soon && /*#__PURE__*/React.createElement("span", {
      className: "n-nav__soon"
    }, typeof it.soon === "string" ? it.soon : "Bientôt"));
  }), footer && /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("div", {
    className: "n-nav__spacer"
  }), footer));
}
Object.assign(__ds_scope, { NavRail });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/navigation/NavRail.jsx", error: String((e && e.message) || e) }); }

// components/streaming/LaunchChecklist.jsx
try { (() => {
/**
 * LaunchChecklist — the fix for the app's worst gap: a disabled "Démarrer" that
 * says nothing. This makes readiness visible without a click (Zeigarnik +
 * Goal-Gradient): Environnement ✓ · Fichier ✓ · Profil ✓ · Channel ✓. Each
 * pending item names exactly what is still missing.
 *
 * Drive `items[].done` from the same facts the preflight decides on; keep the
 * verdict itself in the Application layer.
 */
function LaunchChecklist({
  title = "Prêt à diffuser ?",
  items = [],
  showProgress = true,
  className = ""
}) {
  const done = items.filter(i => i.done).length;
  const pct = items.length ? Math.round(done / items.length * 100) : 0;
  return /*#__PURE__*/React.createElement("div", {
    className: `n-check ${className}`.trim()
  }, /*#__PURE__*/React.createElement("div", {
    className: "n-check__head"
  }, /*#__PURE__*/React.createElement("span", {
    className: "n-check__title"
  }, title), /*#__PURE__*/React.createElement("span", {
    className: "n-check__count"
  }, done, "/", items.length)), items.map((it, i) => /*#__PURE__*/React.createElement("div", {
    key: i,
    className: "n-check__item" + (it.done ? " n-check__item--done" : "")
  }, /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    className: "n-check__mark",
    name: it.done ? "circle-check" : "circle",
    size: 16
  }), /*#__PURE__*/React.createElement("span", null, it.label))), showProgress && /*#__PURE__*/React.createElement("div", {
    className: "n-check__bar"
  }, /*#__PURE__*/React.createElement("div", {
    className: "n-check__bar-fill",
    style: {
      width: `${pct}%`
    }
  })));
}
Object.assign(__ds_scope, { LaunchChecklist });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/streaming/LaunchChecklist.jsx", error: String((e && e.message) || e) }); }

// components/streaming/StatTile.jsx
try { (() => {
/**
 * StatTile — one live ffmpeg stat, chunked into a scannable tile (Miller's Law).
 * Replaces the flat row of five bare numbers. `warning` turns the tile critical
 * — use it for the health signal (speed < 1.0x, growing drops), and nothing else
 * (Von Restorff: red means a real problem).
 */
function StatTile({
  label,
  value,
  unit,
  icon,
  warning = false,
  className = ""
}) {
  const cls = ["n-stat", warning && "n-stat--warning", className].filter(Boolean).join(" ");
  return /*#__PURE__*/React.createElement("div", {
    className: cls
  }, /*#__PURE__*/React.createElement("span", {
    className: "n-stat__label"
  }, icon && /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: icon,
    size: 13
  }), label), /*#__PURE__*/React.createElement("span", {
    className: "n-stat__value"
  }, value, unit && /*#__PURE__*/React.createElement("span", {
    className: "n-stat__unit"
  }, unit)));
}
Object.assign(__ds_scope, { StatTile });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/streaming/StatTile.jsx", error: String((e && e.message) || e) }); }

// components/streaming/StatusBadge.jsx
try { (() => {
const TONE_ICON = {
  neutral: "circle",
  success: "circle-check",
  caution: "triangle-alert",
  critical: "octagon-alert",
  attention: "info",
  live: "radio"
};

/**
 * StatusBadge — session status / health as SHAPE + ICON + TEXT, never color
 * alone (accessibility §9). Replaces the current dashboard's bare colored
 * Ellipse. `live` shows a pulsing red dot + "En direct". Red is reserved for a
 * real anomaly (Von Restorff): running-and-healthy is `success`, not red.
 */
function StatusBadge({
  tone = "neutral",
  children,
  icon,
  dot = false,
  className = ""
}) {
  const cls = ["n-badge", tone !== "neutral" && `n-badge--${tone}`, className].filter(Boolean).join(" ");
  const showDot = dot || tone === "live";
  const glyph = icon !== undefined ? icon : TONE_ICON[tone];
  return /*#__PURE__*/React.createElement("span", {
    className: cls,
    role: "status"
  }, showDot ? /*#__PURE__*/React.createElement("span", {
    className: "n-badge__dot"
  }) : glyph ? /*#__PURE__*/React.createElement("span", {
    className: "n-badge__shape"
  }, typeof glyph === "string" ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    name: glyph,
    size: 14
  }) : glyph) : null, /*#__PURE__*/React.createElement("span", {
    className: "n-badge__text"
  }, children));
}
Object.assign(__ds_scope, { StatusBadge });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/streaming/StatusBadge.jsx", error: String((e && e.message) || e) }); }

// components/surface/Card.jsx
try { (() => {
function _extends() { return _extends = Object.assign ? Object.assign.bind() : function (n) { for (var e = 1; e < arguments.length; e++) { var t = arguments[e]; for (var r in t) ({}).hasOwnProperty.call(t, r) && (n[r] = t[r]); } return n; }, _extends.apply(null, arguments); }
/**
 * Card — a bounded, belted region (Common Region + Uniform Connectedness). This
 * is the antidote to the current dashboard's "flat wall of stacked controls":
 * the dashboard is cut into Source / Diffusion / Santé / Journal cards, each
 * with a quiet title and everything it owns inside one stroke.
 */
function Card({
  title,
  icon,
  badge,
  children,
  className = "",
  ...rest
}) {
  const glyph = icon ? typeof icon === "string" ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    className: "n-card__icon",
    name: icon,
    size: 18
  }) : icon : null;
  const hasHeader = title || glyph || badge;
  return /*#__PURE__*/React.createElement("section", _extends({
    className: `n-card ${className}`.trim()
  }, rest), hasHeader && /*#__PURE__*/React.createElement("header", {
    className: "n-card__header"
  }, glyph, title && /*#__PURE__*/React.createElement("h3", {
    className: "n-card__title"
  }, title), badge && /*#__PURE__*/React.createElement("div", {
    className: "n-card__badge"
  }, badge)), children);
}
Object.assign(__ds_scope, { Card });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/surface/Card.jsx", error: String((e && e.message) || e) }); }

// components/surface/EmptyState.jsx
try { (() => {
/**
 * EmptyState — the first-run documentation-as-UI pattern (Paradox of the Active
 * User). No one reads a manual, so every empty list states the next action and
 * offers a direct CTA. The zero-profile / zero-channel path is the one to nail.
 */
function EmptyState({
  icon = "inbox",
  title,
  message,
  action,
  className = ""
}) {
  const glyph = icon ? typeof icon === "string" ? /*#__PURE__*/React.createElement(__ds_scope.Icon, {
    className: "n-empty__icon",
    name: icon,
    size: 44
  }) : icon : null;
  return /*#__PURE__*/React.createElement("div", {
    className: `n-empty ${className}`.trim()
  }, glyph, title && /*#__PURE__*/React.createElement("div", {
    className: "n-empty__title"
  }, title), message && /*#__PURE__*/React.createElement("p", {
    className: "n-empty__msg"
  }, message), action && /*#__PURE__*/React.createElement("div", {
    className: "n-empty__cta"
  }, action));
}
Object.assign(__ds_scope, { EmptyState });
})(); } catch (e) { __ds_ns.__errors.push({ path: "components/surface/EmptyState.jsx", error: String((e && e.message) || e) }); }

// ui_kits/nagare-app/AppShell.jsx
try { (() => {
const {
  NavRail,
  Icon
} = window.NagareDesignSystem_9475eb;
const {
  useState
} = React;
function TitleBar() {
  return /*#__PURE__*/React.createElement("div", {
    className: "win-titlebar"
  }, /*#__PURE__*/React.createElement("div", {
    className: "win-title"
  }, /*#__PURE__*/React.createElement("span", {
    className: "win-mark"
  }, "\u6D41"), /*#__PURE__*/React.createElement("span", null, "Nagare")), /*#__PURE__*/React.createElement("div", {
    className: "win-caption"
  }, /*#__PURE__*/React.createElement("div", {
    className: "win-cap-btn",
    "aria-hidden": "true"
  }, /*#__PURE__*/React.createElement("span", {
    className: "m-min"
  })), /*#__PURE__*/React.createElement("div", {
    className: "win-cap-btn",
    "aria-hidden": "true"
  }, /*#__PURE__*/React.createElement("span", {
    className: "m-max"
  })), /*#__PURE__*/React.createElement("div", {
    className: "win-cap-btn win-close",
    "aria-hidden": "true"
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "x",
    size: 14
  }))));
}

// The Fluent shell: Windows caption bar + NavigationView rail + content sheet.
// A reserved, disabled "Planifications" slot holds space for iteration 2.
function AppShell({
  page,
  onNavigate,
  children
}) {
  const items = [{
    tag: "dashboard",
    label: "Tableau de bord",
    icon: "gauge"
  }, {
    tag: "profiles",
    label: "Profils",
    icon: "sliders-horizontal"
  }, {
    tag: "channels",
    label: "Channels",
    icon: "radio-tower"
  }, {
    tag: "scheduling",
    label: "Planifications",
    icon: "calendar-clock",
    disabled: true,
    soon: true
  }];
  return /*#__PURE__*/React.createElement("div", {
    className: "win"
  }, /*#__PURE__*/React.createElement(TitleBar, null), /*#__PURE__*/React.createElement("div", {
    className: "win-body"
  }, /*#__PURE__*/React.createElement("div", {
    className: "win-nav"
  }, /*#__PURE__*/React.createElement(NavRail, {
    selected: page,
    onSelect: onNavigate,
    items: items
  })), /*#__PURE__*/React.createElement("main", {
    className: "win-content"
  }, children)));
}
Object.assign(window, {
  AppShell
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/nagare-app/AppShell.jsx", error: String((e && e.message) || e) }); }

// ui_kits/nagare-app/ChannelsPage.jsx
try { (() => {
const DS = window.NagareDesignSystem_9475eb;
const {
  useState
} = React;

// Channels CRUD. The stream key is entered in a PasswordBox and NEVER read back
// (ADR-0005): editing starts with an empty key field; empty = keep current key.
// Validation messages are the domain's own (surfaced in an InfoBar), not rewritten.
function ChannelsPage() {
  const {
    Card,
    Button,
    IconButton,
    TextBox,
    ComboBox,
    PasswordBox,
    InfoBar,
    EmptyState,
    ContentDialog,
    Icon
  } = DS;
  const seed = window.NagareSeed;
  const [channels, setChannels] = useState(seed.channels.map(c => ({
    ...c
  })));
  const [selectedId, setSelectedId] = useState(channels[0]?.id ?? null);
  const [editing, setEditing] = useState(null); // null | "new" | id
  const [form, setForm] = useState(null);
  const [error, setError] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const selected = channels.find(c => c.id === selectedId) || null;
  const isCreating = editing === "new";
  const platformLabel = p => p === "CustomRtmp" ? "RTMP custom" : p;
  const defaultUrl = p => p === "Twitch" ? "rtmp://live.twitch.tv/app" : p === "YouTube" ? "rtmp://a.rtmp.youtube.com/live2" : "";
  const startNew = () => {
    setError(null);
    setEditing("new");
    setForm({
      name: "",
      platform: "Twitch",
      baseUrl: defaultUrl("Twitch"),
      key: ""
    });
  };
  const startEdit = () => {
    if (!selected) return;
    setError(null);
    setEditing(selected.id);
    setForm({
      name: selected.name,
      platform: selected.platform,
      baseUrl: selected.baseUrl,
      key: ""
    });
  };
  const cancel = () => {
    setEditing(null);
    setError(null);
  };
  const save = () => {
    // Mimic the domain invariants surfaced verbatim to the UI.
    if (!form.name.trim()) return setError("Channel name cannot be empty.");
    const url = form.baseUrl.trim().toLowerCase();
    if (!url.startsWith("rtmp://") && !url.startsWith("rtmps://")) return setError("Base URL must use the rtmp:// or rtmps:// scheme.");
    if (isCreating && !form.key) return setError("A protected stream key is required.");
    if (isCreating) {
      const id = "c" + Date.now();
      setChannels(cs => [...cs, {
        id,
        name: form.name.trim(),
        platform: form.platform,
        baseUrl: form.baseUrl.trim(),
        keyConfigured: true
      }]);
      setSelectedId(id);
    } else {
      setChannels(cs => cs.map(c => c.id === editing ? {
        ...c,
        name: form.name.trim(),
        platform: form.platform,
        baseUrl: form.baseUrl.trim(),
        keyConfigured: c.keyConfigured || !!form.key
      } : c));
    }
    setEditing(null);
    setError(null);
  };
  const confirmDelete = () => {
    setChannels(cs => cs.filter(c => c.id !== deleteTarget.id));
    if (selectedId === deleteTarget.id) setSelectedId(null);
    if (editing === deleteTarget.id) setEditing(null);
    setDeleteTarget(null);
  };
  return /*#__PURE__*/React.createElement("div", {
    className: "page"
  }, /*#__PURE__*/React.createElement("div", {
    className: "page-head"
  }, /*#__PURE__*/React.createElement("h1", {
    className: "page-title"
  }, "Channels"), /*#__PURE__*/React.createElement("p", {
    className: "page-sub"
  }, "Vos destinations de diffusion. La cl\xE9 de stream est chiffr\xE9e et n'est jamais r\xE9affich\xE9e.")), /*#__PURE__*/React.createElement("div", {
    className: "toolbar"
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "accent",
    icon: "plus",
    onClick: startNew
  }, "Nouveau channel"), /*#__PURE__*/React.createElement(Button, {
    icon: "pencil",
    disabled: !selected,
    onClick: startEdit
  }, "Modifier"), /*#__PURE__*/React.createElement(Button, {
    variant: "danger",
    icon: "trash-2",
    disabled: !selected,
    onClick: () => setDeleteTarget(selected)
  }, "Supprimer")), channels.length === 0 ? /*#__PURE__*/React.createElement(Card, null, /*#__PURE__*/React.createElement(EmptyState, {
    icon: "radio-tower",
    title: "Aucun channel",
    message: "Cr\xE9ez un channel pour choisir o\xF9 diffuser \u2014 Twitch, YouTube ou un RTMP custom.",
    action: /*#__PURE__*/React.createElement(Button, {
      variant: "accent",
      icon: "plus",
      onClick: startNew
    }, "Nouveau channel")
  })) : /*#__PURE__*/React.createElement("div", {
    className: "crud"
  }, /*#__PURE__*/React.createElement("div", {
    className: "list"
  }, channels.map(c => /*#__PURE__*/React.createElement("button", {
    key: c.id,
    className: "list-row" + (c.id === selectedId ? " list-row--sel" : ""),
    onClick: () => setSelectedId(c.id)
  }, /*#__PURE__*/React.createElement(Icon, {
    className: "list-icon",
    name: c.platform === "YouTube" ? "radio" : "radio-tower",
    size: 18
  }), /*#__PURE__*/React.createElement("div", {
    className: "list-row__body"
  }, /*#__PURE__*/React.createElement("span", {
    className: "list-name"
  }, c.name), /*#__PURE__*/React.createElement("span", {
    className: "list-sub"
  }, platformLabel(c.platform), " \xB7 ", c.baseUrl)), /*#__PURE__*/React.createElement("span", {
    className: "chip"
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "check",
    size: 12,
    style: {
      marginRight: 4
    }
  }), "Cl\xE9 configur\xE9e")))), editing !== null && form && /*#__PURE__*/React.createElement(Card, {
    title: isCreating ? "Nouveau channel" : "Modifier le channel",
    icon: "radio-tower"
  }, /*#__PURE__*/React.createElement("div", {
    className: "editor"
  }, error && /*#__PURE__*/React.createElement(InfoBar, {
    severity: "error",
    title: "R\xE9glage refus\xE9",
    message: error,
    isClosable: true,
    onClose: () => setError(null)
  }), /*#__PURE__*/React.createElement(TextBox, {
    header: "Nom",
    value: form.name,
    onChange: e => setForm({
      ...form,
      name: e.target.value
    }),
    placeholder: "Ma cha\xEEne principale"
  }), /*#__PURE__*/React.createElement(ComboBox, {
    header: "Plateforme",
    value: form.platform,
    items: window.NagareSeed.platforms,
    onChange: v => setForm({
      ...form,
      platform: v,
      baseUrl: defaultUrl(v) || form.baseUrl
    })
  }), /*#__PURE__*/React.createElement(TextBox, {
    header: "URL de base",
    value: form.baseUrl,
    onChange: e => setForm({
      ...form,
      baseUrl: e.target.value
    }),
    mono: true
  }), /*#__PURE__*/React.createElement(PasswordBox, {
    header: "Cl\xE9 de stream",
    value: form.key,
    onChange: e => setForm({
      ...form,
      key: e.target.value
    }),
    placeholder: "\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022\u2022",
    hint: isCreating ? "Requise. Chiffrée au repos ; jamais réaffichée." : "Laisser vide conserve la clé actuelle : une clé enregistrée ne peut jamais être réaffichée."
  }), /*#__PURE__*/React.createElement("div", {
    className: "editor__actions"
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "accent",
    onClick: save
  }, "Enregistrer"), /*#__PURE__*/React.createElement(Button, {
    onClick: cancel
  }, "Annuler"))))), deleteTarget && /*#__PURE__*/React.createElement(ContentDialog, {
    title: `Supprimer le channel « ${deleteTarget.name} » ?`,
    primaryText: "Supprimer",
    primaryVariant: "danger",
    onPrimary: confirmDelete,
    closeText: "Annuler",
    onClose: () => setDeleteTarget(null)
  }, "Cette action est d\xE9finitive. La cl\xE9 de stream chiffr\xE9e sera perdue ; vous devrez la ressaisir pour recr\xE9er ce channel."));
}
Object.assign(window, {
  ChannelsPage
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/nagare-app/ChannelsPage.jsx", error: String((e && e.message) || e) }); }

// ui_kits/nagare-app/DashboardPage.jsx
try { (() => {
const DSD = window.NagareDesignSystem_9475eb;

// Dashboard — the broadcast page. Idle → Démarrer → live monitoring → Arrêter →
// session summary (Peak-End). The wall of stacked controls becomes four belted
// cards: Source · Diffusion · Santé · Journal (Common Region). A LaunchChecklist
// makes a disabled Démarrer explain itself (Zeigarnik). Red is used only for a
// real anomaly. No modal / no focus theft while live (Flow).
function DashboardPage() {
  const {
    useState,
    useEffect,
    useRef
  } = React;
  const {
    Card,
    Button,
    IconButton,
    ComboBox,
    InfoBar,
    ProgressRing,
    StatusBadge,
    StatTile,
    LaunchChecklist,
    Icon
  } = DSD;
  const seed = window.NagareSeed;
  const [file, setFile] = useState(seed.media);
  const [profileId, setProfileId] = useState("p1");
  const [channelId, setChannelId] = useState("c1");
  const [status, setStatus] = useState("idle"); // idle|starting|live|reconnecting|stopped|failed
  const [stats, setStats] = useState({
    fps: 0,
    bitrate: 0,
    speed: 0,
    drops: 0,
    reconnects: 0
  });
  const [logs, setLogs] = useState(window.NagareLogSeed.slice());
  const [elapsed, setElapsed] = useState(0);
  const [summary, setSummary] = useState(null);
  const [copied, setCopied] = useState(false);
  const frameRef = useRef(1);
  const consoleRef = useRef(null);
  const profile = seed.profiles.find(p => p.id === profileId) || null;
  const channel = seed.channels.find(c => c.id === channelId) || null;
  const isLive = status === "live" || status === "reconnecting";
  const locked = isLive || status === "starting";
  const ready = file && profile && channel && (status === "idle" || status === "stopped");
  const warn = isLive && stats.speed > 0 && stats.speed < 1.0;
  const command = window.NagareBuildCommand(profile, channel, file && file.name);
  const checklist = [{
    label: "Environnement ffmpeg",
    done: true
  }, {
    label: "Fichier vidéo",
    done: !!file
  }, {
    label: "Profil d'encodage",
    done: !!profile
  }, {
    label: "Channel",
    done: !!channel
  }];
  const missing = checklist.find(i => !i.done);

  // Live engine: one coalesced tick per second (matches the 1/s stats throttle).
  useEffect(() => {
    if (!isLive) return;
    const id = setInterval(() => {
      setElapsed(e => e + 1);
      setStats(s => {
        const base = profile ? profile.bitrate : 6000;
        const speed = status === "reconnecting" ? 0 : warnRef.current ? 0.80 + Math.random() * 0.12 : 0.99 + Math.random() * 0.02;
        const drops = s.drops + (warnRef.current ? Math.floor(Math.random() * 6) : 0);
        return {
          fps: profile ? profile.fps : 60,
          bitrate: Math.round(base * (0.985 + Math.random() * 0.02)),
          speed: Number(speed.toFixed(2)),
          drops,
          reconnects: s.reconnects
        };
      });
      frameRef.current += profile ? profile.fps : 60;
      const f = frameRef.current;
      const line = status === "reconnecting" ? "[flv] Connection to tcp://live.twitch.tv failed — retrying" : `frame=${String(f).padStart(6)} fps=${profile ? profile.fps : 60} q=22.0 bitrate=${profile ? profile.bitrate : 6000}.0kbits/s speed=1.00x`;
      setLogs(ls => [...ls.slice(-180), line]);
    }, 1000);
    return () => clearInterval(id);
  }, [isLive, status, profile]);
  const warnRef = useRef(false);
  useEffect(() => {
    warnRef.current = false;
  }, [status]);
  useEffect(() => {
    if (consoleRef.current) consoleRef.current.scrollTop = consoleRef.current.scrollHeight;
  }, [logs]);
  const start = () => {
    if (!ready) return;
    setSummary(null);
    setElapsed(0);
    frameRef.current = 1;
    setStats({
      fps: 0,
      bitrate: 0,
      speed: 0,
      drops: 0,
      reconnects: 0
    });
    setLogs(window.NagareLogSeed.slice());
    setStatus("starting");
    setTimeout(() => {
      setStatus("live");
      setStats(s => ({
        ...s,
        fps: profile ? profile.fps : 60,
        bitrate: profile ? profile.bitrate : 6000,
        speed: 1.0
      }));
    }, 1300);
  };
  const stop = failed => {
    setSummary({
      duration: elapsed,
      drops: stats.drops,
      reconnects: stats.reconnects,
      failed: !!failed,
      reason: failed ? "Échec : connexion RTMP refusée par le serveur." : "Arrêt par l'utilisateur."
    });
    setStatus("stopped");
  };
  const reset = () => {
    setSummary(null);
    setStatus("idle");
    setElapsed(0);
    setStats({
      fps: 0,
      bitrate: 0,
      speed: 0,
      drops: 0,
      reconnects: 0
    });
  };

  // Demo aids (kit only)
  const demoDrop = () => {
    warnRef.current = true;
  };
  const demoRecover = () => {
    warnRef.current = false;
    setStatus("live");
  };
  const demoReconnect = () => {
    setStatus("reconnecting");
    setStats(s => ({
      ...s,
      reconnects: s.reconnects + 1,
      speed: 0
    }));
    setTimeout(() => setStatus("live"), 2600);
  };
  const fmt = s => [Math.floor(s / 3600), Math.floor(s / 60) % 60, s % 60].map(n => String(n).padStart(2, "0")).join(":");
  const fr = n => Math.round(n).toLocaleString("fr-FR");
  const spd = n => n.toFixed(2).replace(".", ",");
  const badge = status === "starting" ? /*#__PURE__*/React.createElement(StatusBadge, {
    tone: "attention"
  }, "D\xE9marrage\u2026") : status === "reconnecting" ? /*#__PURE__*/React.createElement(StatusBadge, {
    tone: "attention"
  }, "Reconnexion\u2026") : status === "live" ? warn ? /*#__PURE__*/React.createElement(StatusBadge, {
    tone: "critical"
  }, "En direct \xB7 vitesse basse") : /*#__PURE__*/React.createElement(StatusBadge, {
    tone: "live"
  }, "En direct") : status === "stopped" ? summary && summary.failed ? /*#__PURE__*/React.createElement(StatusBadge, {
    tone: "critical"
  }, "\xC9chec") : /*#__PURE__*/React.createElement(StatusBadge, {
    tone: "neutral"
  }, "Arr\xEAt\xE9e") : /*#__PURE__*/React.createElement(StatusBadge, {
    tone: "neutral"
  }, "Aucune session");
  return /*#__PURE__*/React.createElement("div", {
    className: "page"
  }, /*#__PURE__*/React.createElement("div", {
    className: "page-head"
  }, /*#__PURE__*/React.createElement("h1", {
    className: "page-title"
  }, "Tableau de bord"), /*#__PURE__*/React.createElement("p", {
    className: "page-sub"
  }, "Choisir un fichier, un profil, un channel \u2014 puis diffuser et surveiller.")), /*#__PURE__*/React.createElement("div", {
    className: "dash"
  }, /*#__PURE__*/React.createElement("div", {
    className: "a-source"
  }, /*#__PURE__*/React.createElement(Card, {
    title: "Source",
    icon: "folder-open"
  }, file ? /*#__PURE__*/React.createElement("div", {
    className: "dropzone dropzone--filled"
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "film",
    size: 22
  }), /*#__PURE__*/React.createElement("div", {
    className: "dropzone__body"
  }, /*#__PURE__*/React.createElement("span", {
    className: "dropzone__name"
  }, file.name), /*#__PURE__*/React.createElement("div", {
    className: "media-chips"
  }, /*#__PURE__*/React.createElement("span", {
    className: "chip"
  }, file.duration), /*#__PURE__*/React.createElement("span", {
    className: "chip"
  }, file.w, "\xD7", file.h), /*#__PURE__*/React.createElement("span", {
    className: "chip"
  }, file.fps, " fps"), /*#__PURE__*/React.createElement("span", {
    className: "chip"
  }, file.vcodec), /*#__PURE__*/React.createElement("span", {
    className: "chip"
  }, file.acodec))), !locked && /*#__PURE__*/React.createElement(IconButton, {
    icon: "x",
    label: "Retirer le fichier",
    onClick: () => setFile(null),
    style: {
      marginLeft: "auto"
    }
  })) : /*#__PURE__*/React.createElement("button", {
    className: "dropzone",
    onClick: () => setFile(seed.media)
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "upload",
    size: 22
  }), /*#__PURE__*/React.createElement("div", {
    className: "dropzone__body"
  }, /*#__PURE__*/React.createElement("span", {
    className: "dropzone__name"
  }, "Choisir un fichier\u2026"), /*#__PURE__*/React.createElement("span", null, "ou d\xE9posez un .mp4 ici \xB7 collez un chemin"))))), /*#__PURE__*/React.createElement("div", {
    className: "a-diffusion"
  }, /*#__PURE__*/React.createElement(Card, {
    title: "Diffusion",
    icon: "cast"
  }, /*#__PURE__*/React.createElement("div", {
    className: "editor__row",
    style: {
      marginBottom: 12
    }
  }, /*#__PURE__*/React.createElement(ComboBox, {
    header: "Profil d'encodage",
    value: profileId || undefined,
    placeholder: "Choisir un profil",
    disabled: locked,
    items: seed.profiles.map(p => ({
      value: p.id,
      label: p.name
    })),
    onChange: setProfileId
  }), /*#__PURE__*/React.createElement(ComboBox, {
    header: "Channel",
    value: channelId || undefined,
    placeholder: "Choisir un channel",
    disabled: locked,
    items: seed.channels.map(c => ({
      value: c.id,
      label: c.name
    })),
    onChange: setChannelId
  })), /*#__PURE__*/React.createElement("div", {
    style: {
      fontSize: 12,
      color: "var(--text-secondary)",
      marginBottom: 4
    }
  }, "Commande ffmpeg \u2014 cl\xE9 masqu\xE9e"), /*#__PURE__*/React.createElement("div", {
    className: "cmd"
  }, /*#__PURE__*/React.createElement("div", {
    className: "cmd__box" + (command ? "" : " cmd__box--empty")
  }, command || "Sélectionnez un profil et un channel pour voir la commande."), command && /*#__PURE__*/React.createElement(IconButton, {
    className: "cmd__copy",
    icon: copied ? "check" : "copy",
    label: "Copier la commande (cl\xE9 masqu\xE9e)",
    onClick: () => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1200);
    }
  })))), /*#__PURE__*/React.createElement("div", {
    className: "a-health"
  }, /*#__PURE__*/React.createElement(Card, {
    title: "Sant\xE9",
    icon: "activity",
    badge: badge
  }, status === "starting" && /*#__PURE__*/React.createElement("div", {
    style: {
      padding: "24px 0",
      display: "flex",
      justifyContent: "center"
    }
  }, /*#__PURE__*/React.createElement(ProgressRing, {
    size: 40,
    label: "D\xE9marrage\u2026"
  })), (status === "idle" || status === "stopped" && !summary) && /*#__PURE__*/React.createElement("div", {
    className: "health-idle"
  }, /*#__PURE__*/React.createElement(LaunchChecklist, {
    items: checklist
  }), missing ? /*#__PURE__*/React.createElement("div", {
    className: "why"
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "triangle-alert",
    size: 16
  }), /*#__PURE__*/React.createElement("span", null, "Il manque : ", /*#__PURE__*/React.createElement("b", null, missing.label.toLowerCase()), ". Renseignez-le pour activer la diffusion.")) : /*#__PURE__*/React.createElement("div", {
    className: "why"
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "check",
    size: 16,
    style: {
      color: "var(--success)"
    }
  }), /*#__PURE__*/React.createElement("span", null, "Tout est pr\xEAt. Vous pouvez diffuser.")), /*#__PURE__*/React.createElement("div", {
    className: "primary-action"
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "accent",
    icon: "play",
    disabled: !ready,
    onClick: start
  }, "D\xE9marrer"))), isLive && /*#__PURE__*/React.createElement("div", {
    className: "stack"
  }, /*#__PURE__*/React.createElement("div", {
    style: {
      display: "flex",
      alignItems: "baseline",
      gap: 8
    }
  }, /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 28,
      fontWeight: 600,
      fontVariantNumeric: "tabular-nums"
    }
  }, fmt(elapsed)), /*#__PURE__*/React.createElement("span", {
    style: {
      fontSize: 12,
      color: "var(--text-secondary)"
    }
  }, "en direct \xB7 ", channel && channel.name)), /*#__PURE__*/React.createElement("div", {
    className: "stats"
  }, /*#__PURE__*/React.createElement(StatTile, {
    label: "Images",
    value: String(stats.fps),
    unit: "fps",
    icon: "film"
  }), /*#__PURE__*/React.createElement(StatTile, {
    label: "D\xE9bit",
    value: fr(stats.bitrate),
    unit: "kbits/s",
    icon: "activity"
  }), /*#__PURE__*/React.createElement(StatTile, {
    label: "Vitesse",
    value: status === "reconnecting" ? "—" : spd(stats.speed),
    unit: "x",
    icon: "gauge",
    warning: warn
  }), /*#__PURE__*/React.createElement(StatTile, {
    label: "Drops",
    value: fr(stats.drops),
    unit: "drops",
    warning: stats.drops > 0
  })), /*#__PURE__*/React.createElement(StatTile, {
    label: "Reconnexions",
    value: fr(stats.reconnects),
    unit: "",
    icon: "refresh-cw",
    warning: stats.reconnects > 0
  }), /*#__PURE__*/React.createElement("div", {
    className: "primary-action"
  }, /*#__PURE__*/React.createElement(Button, {
    icon: "circle-stop",
    onClick: () => stop(false)
  }, "Arr\xEAter"))), status === "stopped" && summary && /*#__PURE__*/React.createElement("div", {
    className: "stack"
  }, /*#__PURE__*/React.createElement(InfoBar, {
    severity: summary.failed ? "error" : "success",
    title: summary.failed ? "Session en échec" : "Session terminée",
    message: summary.reason
  }), /*#__PURE__*/React.createElement("div", {
    className: "summary-grid"
  }, /*#__PURE__*/React.createElement(StatTile, {
    label: "Dur\xE9e",
    value: fmt(summary.duration)
  }), /*#__PURE__*/React.createElement(StatTile, {
    label: "Drops",
    value: fr(summary.drops),
    warning: summary.drops > 0
  }), /*#__PURE__*/React.createElement(StatTile, {
    label: "Reconnexions",
    value: fr(summary.reconnects),
    warning: summary.reconnects > 0
  })), /*#__PURE__*/React.createElement("div", {
    className: "primary-action"
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "accent",
    icon: "play",
    disabled: !ready,
    onClick: start
  }, "Rediffuser"), /*#__PURE__*/React.createElement(Button, {
    icon: "rotate-cw",
    onClick: reset
  }, "Nouvelle session"))))), /*#__PURE__*/React.createElement("div", {
    className: "a-journal"
  }, /*#__PURE__*/React.createElement(Card, {
    title: "Journal ffmpeg",
    icon: "terminal",
    badge: /*#__PURE__*/React.createElement("span", {
      style: {
        fontSize: 12,
        color: "var(--text-tertiary)",
        fontFamily: "var(--font-mono)"
      }
    }, "500 derni\xE8res lignes")
  }, /*#__PURE__*/React.createElement("div", {
    className: "console",
    ref: consoleRef
  }, logs.map((l, i) => {
    const err = /failed|error|refused|introuvable/i.test(l);
    const wl = /retry|reconnect|drop|warn/i.test(l);
    return /*#__PURE__*/React.createElement("div", {
      key: i,
      className: "console__line" + (err ? " console__line--err" : wl ? " console__line--warn" : "")
    }, l);
  }))))), /*#__PURE__*/React.createElement("div", {
    className: "demo-strip"
  }, /*#__PURE__*/React.createElement("b", null, "D\xE9mo :"), status === "idle" && /*#__PURE__*/React.createElement(Button, {
    size: "small",
    onClick: () => {
      setFile(seed.media);
      setProfileId("p1");
      setChannelId("c1");
    }
  }, "Pr\xE9-remplir"), status === "idle" && /*#__PURE__*/React.createElement(Button, {
    size: "small",
    onClick: () => {
      setFile(null);
      setProfileId(null);
      setChannelId(null);
    }
  }, "Config vide"), isLive && /*#__PURE__*/React.createElement(Button, {
    size: "small",
    onClick: demoDrop
  }, "Chute du flux"), isLive && /*#__PURE__*/React.createElement(Button, {
    size: "small",
    onClick: demoRecover
  }, "R\xE9tablir"), isLive && /*#__PURE__*/React.createElement(Button, {
    size: "small",
    onClick: demoReconnect
  }, "Reconnexion"), isLive && /*#__PURE__*/React.createElement(Button, {
    size: "small",
    variant: "danger",
    onClick: () => stop(true)
  }, "Simuler un \xE9chec"), /*#__PURE__*/React.createElement("span", null, "parcourez les \xE9tats sans vraie diffusion.")));
}
Object.assign(window, {
  DashboardPage
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/nagare-app/DashboardPage.jsx", error: String((e && e.message) || e) }); }

// ui_kits/nagare-app/ProfilesPage.jsx
try { (() => {
const DSP = window.NagareDesignSystem_9475eb;

// Profiles CRUD with the Laws-of-UX editor: pick a PRESET instead of 15 fields
// (Hick's Law), fields CHUNKED into Vidéo / Débit / Audio / Entrée (Miller's Law),
// and an "Avancé" disclosure for keyint_min / bufsize / GOP (progressive disclosure).
// Invariants E1-E8 are the domain's; the editor surfaces the message, doesn't invent it.
function ProfilesPage() {
  const {
    useState
  } = React;
  const {
    Card,
    Button,
    TextBox,
    NumberBox,
    ComboBox,
    ToggleSwitch,
    InfoBar,
    EmptyState,
    ContentDialog,
    Icon
  } = DSP;
  const seed = window.NagareSeed;
  const summarize = p => `${p.codec} · ${p.preset} · ${p.rc} · ${p.bitrate} kbps · ${p.w}×${p.h} · ${p.fps} fps`;
  const presetValues = {
    twitch1080: {
      codec: "h264_nvenc",
      preset: "p5",
      rc: "CBR",
      bitrate: 6000,
      maxrate: 6000,
      bufsize: 6000,
      gop: 120,
      keyint: 120,
      w: 1920,
      h: 1080,
      fps: 60,
      audioBitrate: 160,
      sampleRate: 48000
    },
    yt1440: {
      codec: "hevc_nvenc",
      preset: "p6",
      rc: "CBR",
      bitrate: 12000,
      maxrate: 12000,
      bufsize: 12000,
      gop: 120,
      keyint: 120,
      w: 2560,
      h: 1440,
      fps: 60,
      audioBitrate: 192,
      sampleRate: 48000
    },
    twitch720: {
      codec: "h264_nvenc",
      preset: "p4",
      rc: "CBR",
      bitrate: 4500,
      maxrate: 4500,
      bufsize: 4500,
      gop: 120,
      keyint: 120,
      w: 1280,
      h: 720,
      fps: 60,
      audioBitrate: 160,
      sampleRate: 48000
    },
    cpu720: {
      codec: "libx264",
      preset: "veryfast",
      rc: "CBR",
      bitrate: 3500,
      maxrate: 3500,
      bufsize: 3500,
      gop: 60,
      keyint: 60,
      w: 1280,
      h: 720,
      fps: 30,
      audioBitrate: 128,
      sampleRate: 44100
    }
  };
  const [profiles, setProfiles] = useState(seed.profiles.map(p => ({
    ...p
  })));
  const [selectedId, setSelectedId] = useState(profiles[0]?.id ?? null);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(null);
  const [advanced, setAdvanced] = useState(false);
  const [activePreset, setActivePreset] = useState(null);
  const [error, setError] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const selected = profiles.find(p => p.id === selectedId) || null;
  const isCreating = editing === "new";
  const set = patch => setForm(f => ({
    ...f,
    ...patch
  }));
  const startNew = () => {
    setError(null);
    setAdvanced(false);
    setActivePreset("twitch1080");
    setEditing("new");
    setForm({
      name: "Nouveau profil",
      ...presetValues.twitch1080
    });
  };
  const startEdit = () => {
    if (!selected) return;
    setError(null);
    setAdvanced(false);
    setActivePreset(null);
    setEditing(selected.id);
    setForm({
      ...selected
    });
  };
  const applyPreset = key => {
    setActivePreset(key);
    setForm(f => ({
      ...f,
      ...presetValues[key]
    }));
  };
  const save = () => {
    const f = form;
    if (f.bitrate <= 0 || f.maxrate <= 0 || f.bufsize <= 0) return setError("E1: bitrate, maxrate and bufsize must be strictly positive.");
    if (f.rc === "CBR" && f.maxrate !== f.bitrate) return setError("E2: in CBR, maxrate must equal bitrate.");
    if (f.bufsize < f.bitrate) return setError("E4: bufsize must be greater than or equal to bitrate.");
    if (f.gop <= 0 || f.keyint <= 0 || f.keyint > f.gop) return setError("E5: GOP must be positive and 0 < keyint_min <= g.");
    if (isCreating) {
      const id = "p" + Date.now();
      setProfiles(ps => [...ps, {
        ...f,
        id
      }]);
      setSelectedId(id);
    } else setProfiles(ps => ps.map(p => p.id === editing ? {
      ...f,
      id: editing
    } : p));
    setEditing(null);
    setError(null);
  };
  const confirmDelete = () => {
    setProfiles(ps => ps.filter(p => p.id !== deleteTarget.id));
    if (selectedId === deleteTarget.id) setSelectedId(null);
    if (editing === deleteTarget.id) setEditing(null);
    setDeleteTarget(null);
  };
  const presetList = form ? window.NagareSeed.presetsFor(form.codec) : [];
  return /*#__PURE__*/React.createElement("div", {
    className: "page"
  }, /*#__PURE__*/React.createElement("div", {
    className: "page-head"
  }, /*#__PURE__*/React.createElement("h1", {
    className: "page-title"
  }, "Profils d'encodage"), /*#__PURE__*/React.createElement("p", {
    className: "page-sub"
  }, "Des r\xE9glages ffmpeg r\xE9utilisables. Partez d'un preset ; l'app absorbe la complexit\xE9.")), /*#__PURE__*/React.createElement("div", {
    className: "toolbar"
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "accent",
    icon: "plus",
    onClick: startNew
  }, "Nouveau profil"), /*#__PURE__*/React.createElement(Button, {
    icon: "pencil",
    disabled: !selected,
    onClick: startEdit
  }, "Modifier"), /*#__PURE__*/React.createElement(Button, {
    variant: "danger",
    icon: "trash-2",
    disabled: !selected,
    onClick: () => setDeleteTarget(selected)
  }, "Supprimer")), profiles.length === 0 ? /*#__PURE__*/React.createElement(Card, null, /*#__PURE__*/React.createElement(EmptyState, {
    icon: "sliders-horizontal",
    title: "Aucun profil d'encodage",
    message: "Un profil d\xE9crit comment encoder : codec, d\xE9bit, r\xE9solution. Commencez par un preset pr\xEAt \xE0 l'emploi.",
    action: /*#__PURE__*/React.createElement(Button, {
      variant: "accent",
      icon: "plus",
      onClick: startNew
    }, "Nouveau profil")
  })) : /*#__PURE__*/React.createElement("div", {
    className: "crud crud--wide"
  }, /*#__PURE__*/React.createElement("div", {
    className: "list"
  }, profiles.map(p => /*#__PURE__*/React.createElement("button", {
    key: p.id,
    className: "list-row" + (p.id === selectedId ? " list-row--sel" : ""),
    onClick: () => setSelectedId(p.id)
  }, /*#__PURE__*/React.createElement(Icon, {
    className: "list-icon",
    name: "sliders-horizontal",
    size: 18
  }), /*#__PURE__*/React.createElement("div", {
    className: "list-row__body"
  }, /*#__PURE__*/React.createElement("span", {
    className: "list-name"
  }, p.name), /*#__PURE__*/React.createElement("span", {
    className: "list-sub"
  }, summarize(p)))))), editing !== null && form && /*#__PURE__*/React.createElement(Card, {
    title: isCreating ? "Nouveau profil" : "Modifier le profil",
    icon: "sliders-horizontal"
  }, /*#__PURE__*/React.createElement("div", {
    className: "editor"
  }, error && /*#__PURE__*/React.createElement(InfoBar, {
    severity: "error",
    title: "R\xE9glage refus\xE9",
    message: error,
    isClosable: true,
    onClose: () => setError(null)
  }), /*#__PURE__*/React.createElement(TextBox, {
    header: "Nom",
    value: form.name,
    onChange: e => set({
      name: e.target.value
    })
  }), isCreating && /*#__PURE__*/React.createElement(React.Fragment, null, /*#__PURE__*/React.createElement("div", {
    className: "editor__section"
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "zap",
    size: 16
  }), "Preset de d\xE9part"), /*#__PURE__*/React.createElement("div", {
    className: "presets"
  }, window.NagareSeed.presets.map(ps => /*#__PURE__*/React.createElement("button", {
    key: ps.key,
    className: "preset" + (activePreset === ps.key ? " preset--sel" : ""),
    onClick: () => applyPreset(ps.key)
  }, /*#__PURE__*/React.createElement(Icon, {
    className: "preset__icon",
    name: ps.icon,
    size: 18
  }), /*#__PURE__*/React.createElement("span", null, /*#__PURE__*/React.createElement("span", {
    className: "preset__name"
  }, ps.label), /*#__PURE__*/React.createElement("br", null), /*#__PURE__*/React.createElement("span", {
    className: "preset__sub"
  }, ps.sub)))))), /*#__PURE__*/React.createElement("div", {
    className: "editor__section"
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "film",
    size: 16
  }), "Vid\xE9o"), /*#__PURE__*/React.createElement("div", {
    className: "editor__row"
  }, /*#__PURE__*/React.createElement(ComboBox, {
    header: "Codec",
    value: form.codec,
    items: window.NagareSeed.codecs,
    onChange: v => set({
      codec: v,
      preset: window.NagareSeed.presetsFor(v)[0]
    })
  }), /*#__PURE__*/React.createElement(ComboBox, {
    header: "Preset",
    value: form.preset,
    items: presetList,
    onChange: v => set({
      preset: v
    })
  })), /*#__PURE__*/React.createElement("div", {
    className: "editor__row"
  }, /*#__PURE__*/React.createElement(NumberBox, {
    header: "Largeur",
    value: form.w,
    step: 2,
    onChange: v => set({
      w: v
    })
  }), /*#__PURE__*/React.createElement(NumberBox, {
    header: "Hauteur",
    value: form.h,
    step: 2,
    onChange: v => set({
      h: v
    })
  })), /*#__PURE__*/React.createElement(NumberBox, {
    header: "fps",
    value: form.fps,
    onChange: v => set({
      fps: v
    })
  }), /*#__PURE__*/React.createElement("div", {
    className: "editor__section"
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "activity",
    size: 16
  }), "D\xE9bit"), /*#__PURE__*/React.createElement(ComboBox, {
    header: "Rate control",
    value: form.rc,
    items: ["CBR", "VBR"],
    onChange: v => set({
      rc: v,
      maxrate: v === "CBR" ? form.bitrate : form.maxrate
    })
  }), /*#__PURE__*/React.createElement("div", {
    className: "editor__row"
  }, /*#__PURE__*/React.createElement(NumberBox, {
    header: "Bitrate (kbps)",
    value: form.bitrate,
    step: 100,
    onChange: v => set({
      bitrate: v,
      maxrate: form.rc === "CBR" ? v : form.maxrate,
      bufsize: form.bufsize < v ? v : form.bufsize
    })
  }), /*#__PURE__*/React.createElement(NumberBox, {
    header: "Maxrate (kbps)",
    value: form.maxrate,
    step: 100,
    disabled: form.rc === "CBR",
    onChange: v => set({
      maxrate: v
    })
  })), /*#__PURE__*/React.createElement("div", {
    className: "editor__section"
  }, /*#__PURE__*/React.createElement(Icon, {
    name: "radio",
    size: 16
  }), "Audio"), /*#__PURE__*/React.createElement("div", {
    className: "editor__row"
  }, /*#__PURE__*/React.createElement(NumberBox, {
    header: "Bitrate audio (kbps)",
    value: form.audioBitrate,
    step: 16,
    onChange: v => set({
      audioBitrate: v
    })
  }), /*#__PURE__*/React.createElement(ComboBox, {
    header: "Sample rate (Hz)",
    value: form.sampleRate,
    items: [44100, 48000],
    onChange: v => set({
      sampleRate: v
    })
  })), /*#__PURE__*/React.createElement("div", {
    className: "disclose"
  }, /*#__PURE__*/React.createElement("button", {
    className: "disclose__toggle",
    "aria-expanded": advanced,
    onClick: () => setAdvanced(a => !a)
  }, /*#__PURE__*/React.createElement(Icon, {
    className: "chev",
    name: "chevron-right",
    size: 16
  }), "Avanc\xE9", /*#__PURE__*/React.createElement("span", {
    style: {
      marginLeft: "auto",
      fontWeight: 400,
      fontSize: 12,
      color: "var(--text-tertiary)"
    }
  }, "bufsize \xB7 GOP \xB7 keyint_min")), advanced && /*#__PURE__*/React.createElement("div", {
    className: "disclose__body"
  }, /*#__PURE__*/React.createElement(NumberBox, {
    header: "Bufsize (kbps)",
    value: form.bufsize,
    step: 100,
    onChange: v => set({
      bufsize: v
    })
  }), /*#__PURE__*/React.createElement("div", {
    className: "editor__row"
  }, /*#__PURE__*/React.createElement(NumberBox, {
    header: "GOP (-g)",
    value: form.gop,
    onChange: v => set({
      gop: v
    })
  }), /*#__PURE__*/React.createElement(NumberBox, {
    header: "keyint_min",
    value: form.keyint,
    onChange: v => set({
      keyint: v
    })
  })))), /*#__PURE__*/React.createElement("div", {
    className: "editor__actions"
  }, /*#__PURE__*/React.createElement(Button, {
    variant: "accent",
    onClick: save
  }, "Enregistrer"), /*#__PURE__*/React.createElement(Button, {
    onClick: () => {
      setEditing(null);
      setError(null);
    }
  }, "Annuler"))))), deleteTarget && /*#__PURE__*/React.createElement(ContentDialog, {
    title: `Supprimer le profil « ${deleteTarget.name} » ?`,
    primaryText: "Supprimer",
    primaryVariant: "danger",
    onPrimary: confirmDelete,
    closeText: "Annuler",
    onClose: () => setDeleteTarget(null)
  }, "Cette action est d\xE9finitive. Les diffusions \xE0 venir ne pourront plus utiliser ce profil."));
}
Object.assign(window, {
  ProfilesPage
});
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/nagare-app/ProfilesPage.jsx", error: String((e && e.message) || e) }); }

// ui_kits/nagare-app/store.js
try { (() => {
// Seed data for the Nagare UI kit (fake, in-memory). Mirrors the shapes the real
// ViewModels expose: StreamProfileDto, ChannelDto, and the encoding summary the
// EncodingSummaryConverter produces. English identifiers, French display strings.
window.NagareSeed = {
  profiles: [{
    id: "p1",
    name: "Twitch 1080p60",
    codec: "h264_nvenc",
    preset: "p5",
    rc: "CBR",
    bitrate: 6000,
    maxrate: 6000,
    bufsize: 6000,
    gop: 120,
    keyint: 120,
    w: 1920,
    h: 1080,
    fps: 60,
    audioBitrate: 160,
    sampleRate: 48000
  }, {
    id: "p2",
    name: "YouTube 1440p60",
    codec: "hevc_nvenc",
    preset: "p6",
    rc: "CBR",
    bitrate: 12000,
    maxrate: 12000,
    bufsize: 12000,
    gop: 120,
    keyint: 120,
    w: 2560,
    h: 1440,
    fps: 60,
    audioBitrate: 192,
    sampleRate: 48000
  }, {
    id: "p3",
    name: "libx264 720p · CPU",
    codec: "libx264",
    preset: "veryfast",
    rc: "CBR",
    bitrate: 3500,
    maxrate: 3500,
    bufsize: 3500,
    gop: 60,
    keyint: 60,
    w: 1280,
    h: 720,
    fps: 30,
    audioBitrate: 128,
    sampleRate: 44100
  }],
  channels: [{
    id: "c1",
    name: "Twitch principal",
    platform: "Twitch",
    baseUrl: "rtmp://live.twitch.tv/app",
    keyConfigured: true
  }, {
    id: "c2",
    name: "YouTube — live",
    platform: "YouTube",
    baseUrl: "rtmp://a.rtmp.youtube.com/live2",
    keyConfigured: true
  }],
  // Ready-made presets for the profile editor (Hick's Law: choose a preset, not 15 fields).
  presets: [{
    key: "twitch1080",
    label: "Twitch 1080p60",
    sub: "h264_nvenc · 6000 kbps",
    icon: "radio-tower"
  }, {
    key: "yt1440",
    label: "YouTube 1440p60",
    sub: "hevc_nvenc · 12000 kbps",
    icon: "radio-tower"
  }, {
    key: "twitch720",
    label: "Twitch 720p60 · faible débit",
    sub: "h264_nvenc · 4500 kbps",
    icon: "signal"
  }, {
    key: "cpu720",
    label: "libx264 720p · sans GPU",
    sub: "libx264 · 3500 kbps",
    icon: "hard-drive"
  }],
  media: {
    name: "boucle-attente.mp4",
    duration: "00:04:12",
    w: 1920,
    h: 1080,
    fps: 60,
    vcodec: "h264",
    acodec: "aac",
    size: "247 Mo"
  },
  codecs: [{
    value: "h264_nvenc",
    label: "h264_nvenc · GPU"
  }, {
    value: "hevc_nvenc",
    label: "hevc_nvenc · GPU"
  }, {
    value: "libx264",
    label: "libx264 · CPU"
  }],
  platforms: [{
    value: "Twitch",
    label: "Twitch"
  }, {
    value: "YouTube",
    label: "YouTube"
  }, {
    value: "CustomRtmp",
    label: "RTMP custom"
  }],
  presetsFor: function (codec) {
    return codec === "libx264" ? ["ultrafast", "superfast", "veryfast", "faster", "fast", "medium", "slow", "slower", "veryslow"] : ["p1", "p2", "p3", "p4", "p5", "p6", "p7"];
  }
};

// A short ffmpeg-style startup log (the console rehydrates ~these on load).
window.NagareLogSeed = ["ffmpeg version 6.1.1 Copyright (c) 2000-2024 the FFmpeg developers", "  built with gcc 13.2.0", "Input #0, mov,mp4,m4a,3gp,3g2,mj2, from 'boucle-attente.mp4':", "  Duration: 00:04:12.00, start: 0.000000, bitrate: 8123 kb/s", "  Stream #0:0(und): Video: h264 (High), yuv420p, 1920x1080, 60 fps, 60 tbr", "  Stream #0:1(und): Audio: aac (LC), 48000 Hz, stereo, fltp, 160 kb/s", "Stream mapping:", "  Stream #0:0 -> #0:0 (h264 (native) -> h264_nvenc)", "  Stream #0:1 -> #0:1 (aac (native) -> aac)", "[h264_nvenc @ 0x5581] Using NVENC preset p5 (rc=cbr)", "Output #0, flv, to 'rtmp://live.twitch.tv/app/••••':", "Press [q] to stop, [?] for help"];

// Build the masked ffmpeg command from a profile + channel (SPEC §4: key masked).
window.NagareBuildCommand = function (p, c, file) {
  if (!p || !c) return "";
  const rc = p.rc.toLowerCase();
  const parts = ["ffmpeg", "-re", "-stream_loop", "-1", "-i", `"${file || "boucle-attente.mp4"}"`, "-c:v", p.codec, "-preset", p.preset, "-rc", rc, "-b:v", `${p.bitrate}k`, "-maxrate", `${p.maxrate}k`, "-bufsize", `${p.bufsize}k`, "-g", String(p.gop), "-keyint_min", String(p.keyint), "-vf", `scale=${p.w}:${p.h}`, "-r", String(p.fps), "-c:a", "aac", "-b:a", `${p.audioBitrate}k`, "-ar", String(p.sampleRate), "-f", "flv", `${c.baseUrl}/••••`];
  return parts.join(" ");
};
})(); } catch (e) { __ds_ns.__errors.push({ path: "ui_kits/nagare-app/store.js", error: String((e && e.message) || e) }); }

__ds_ns.Button = __ds_scope.Button;

__ds_ns.IconButton = __ds_scope.IconButton;

__ds_ns.ContentDialog = __ds_scope.ContentDialog;

__ds_ns.InfoBar = __ds_scope.InfoBar;

__ds_ns.ProgressRing = __ds_scope.ProgressRing;

__ds_ns.Icon = __ds_scope.Icon;

__ds_ns.ICON_PATHS = __ds_scope.ICON_PATHS;

__ds_ns.ComboBox = __ds_scope.ComboBox;

__ds_ns.NumberBox = __ds_scope.NumberBox;

__ds_ns.PasswordBox = __ds_scope.PasswordBox;

__ds_ns.TextBox = __ds_scope.TextBox;

__ds_ns.ToggleSwitch = __ds_scope.ToggleSwitch;

__ds_ns.NavRail = __ds_scope.NavRail;

__ds_ns.LaunchChecklist = __ds_scope.LaunchChecklist;

__ds_ns.StatTile = __ds_scope.StatTile;

__ds_ns.StatusBadge = __ds_scope.StatusBadge;

__ds_ns.Card = __ds_scope.Card;

__ds_ns.EmptyState = __ds_scope.EmptyState;

})();
