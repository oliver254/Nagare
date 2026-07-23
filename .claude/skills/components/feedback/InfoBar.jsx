import React from "react";
import { Icon } from "../icon/Icon.jsx";
import { IconButton } from "../buttons/IconButton.jsx";

const SEVERITY_ICON = {
  informational: "info",
  success: "circle-check",
  warning: "triangle-alert",
  error: "octagon-alert",
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
export function InfoBar({
  severity = "informational",
  title,
  message,
  isClosable = false,
  onClose,
  actions,
  children,
  className = "",
}) {
  const cls = [
    "n-infobar",
    severity !== "informational" && `n-infobar--${severity}`,
    className,
  ]
    .filter(Boolean)
    .join(" ");
  return (
    <div className={cls} role={severity === "error" ? "alert" : "status"}>
      <Icon className="n-infobar__icon" name={SEVERITY_ICON[severity]} size={18} />
      <div className="n-infobar__body">
        {title && <span className="n-infobar__title">{title}</span>}
        {message && <span className="n-infobar__msg">{message}</span>}
        {children}
        {actions && <div className="n-infobar__actions">{actions}</div>}
      </div>
      {isClosable && <IconButton icon="x" label="Fermer" onClick={onClose} />}
    </div>
  );
}
