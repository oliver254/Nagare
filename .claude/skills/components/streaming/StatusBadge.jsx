import React from "react";
import { Icon } from "../icon/Icon.jsx";

const TONE_ICON = {
  neutral: "circle",
  success: "circle-check",
  caution: "triangle-alert",
  critical: "octagon-alert",
  attention: "info",
  live: "radio",
};

/**
 * StatusBadge — session status / health as SHAPE + ICON + TEXT, never color
 * alone (accessibility §9). Replaces the current dashboard's bare colored
 * Ellipse. `live` shows a pulsing red dot + "En direct". Red is reserved for a
 * real anomaly (Von Restorff): running-and-healthy is `success`, not red.
 */
export function StatusBadge({ tone = "neutral", children, icon, dot = false, className = "" }) {
  const cls = ["n-badge", tone !== "neutral" && `n-badge--${tone}`, className]
    .filter(Boolean)
    .join(" ");
  const showDot = dot || tone === "live";
  const glyph = icon !== undefined ? icon : TONE_ICON[tone];
  return (
    <span className={cls} role="status">
      {showDot ? (
        <span className="n-badge__dot" />
      ) : glyph ? (
        <span className="n-badge__shape">
          {typeof glyph === "string" ? <Icon name={glyph} size={14} /> : glyph}
        </span>
      ) : null}
      <span className="n-badge__text">{children}</span>
    </span>
  );
}
