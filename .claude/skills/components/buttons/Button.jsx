import React from "react";
import { Icon } from "../icon/Icon.jsx";

/**
 * Button — Fluent/WinUI button.
 *
 * Von Restorff: exactly ONE accent button per screen (the primary action —
 * Démarrer / Enregistrer / Nouveau). Everything else is `standard` or `subtle`.
 * A destructive action is never styled as the accent and never sits next to it.
 */
export function Button({
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
  const cls = [
    "n-btn",
    variant !== "standard" && `n-btn--${variant}`,
    size === "small" && "n-btn--sm",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  const glyph = icon
    ? typeof icon === "string"
      ? <Icon name={icon} size={size === "small" ? 14 : 16} />
      : icon
    : null;

  return (
    <button type={type} className={cls} disabled={disabled} {...rest}>
      {iconPosition === "start" && glyph}
      {children != null && children !== "" && <span className="n-btn__label">{children}</span>}
      {iconPosition === "end" && glyph}
    </button>
  );
}
