import React from "react";
import { Icon } from "../icon/Icon.jsx";

/**
 * IconButton — a square, icon-only button (toolbar / list-row actions).
 *
 * `label` is REQUIRED: it becomes both `aria-label` and the tooltip. This is the
 * accessibility rule from the brief — every icon button carries an
 * AutomationProperties.Name. Default variant is `subtle`.
 */
export function IconButton({
  icon,
  label,
  variant = "subtle",
  disabled = false,
  className = "",
  ...rest
}) {
  const cls = [
    "n-btn",
    "n-iconbtn",
    variant !== "standard" && `n-btn--${variant}`,
    className,
  ]
    .filter(Boolean)
    .join(" ");
  return (
    <button
      type="button"
      className={cls}
      aria-label={label}
      title={label}
      disabled={disabled}
      {...rest}
    >
      {typeof icon === "string" ? <Icon name={icon} size={16} /> : icon}
    </button>
  );
}
