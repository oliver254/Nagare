import React from "react";
import { Icon } from "../icon/Icon.jsx";

/**
 * EmptyState — the first-run documentation-as-UI pattern (Paradox of the Active
 * User). No one reads a manual, so every empty list states the next action and
 * offers a direct CTA. The zero-profile / zero-channel path is the one to nail.
 */
export function EmptyState({
  icon = "inbox",
  title,
  message,
  action,
  className = "",
}) {
  const glyph = icon
    ? typeof icon === "string"
      ? <Icon className="n-empty__icon" name={icon} size={44} />
      : icon
    : null;
  return (
    <div className={`n-empty ${className}`.trim()}>
      {glyph}
      {title && <div className="n-empty__title">{title}</div>}
      {message && <p className="n-empty__msg">{message}</p>}
      {action && <div className="n-empty__cta">{action}</div>}
    </div>
  );
}
