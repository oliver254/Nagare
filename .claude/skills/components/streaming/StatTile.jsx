import React from "react";
import { Icon } from "../icon/Icon.jsx";

/**
 * StatTile — one live ffmpeg stat, chunked into a scannable tile (Miller's Law).
 * Replaces the flat row of five bare numbers. `warning` turns the tile critical
 * — use it for the health signal (speed < 1.0x, growing drops), and nothing else
 * (Von Restorff: red means a real problem).
 */
export function StatTile({ label, value, unit, icon, warning = false, className = "" }) {
  const cls = ["n-stat", warning && "n-stat--warning", className]
    .filter(Boolean)
    .join(" ");
  return (
    <div className={cls}>
      <span className="n-stat__label">
        {icon && <Icon name={icon} size={13} />}
        {label}
      </span>
      <span className="n-stat__value">
        {value}
        {unit && <span className="n-stat__unit">{unit}</span>}
      </span>
    </div>
  );
}
