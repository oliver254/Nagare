import React from "react";
import { Button } from "../buttons/Button.jsx";

/**
 * ContentDialog — Fluent modal dialog. Nagare's ONLY modal, used for destructive
 * confirmation (delete a profile / channel). Confirmation must NAME the object
 * being deleted, and the destructive button is tinted `danger`, never the accent
 * (the accent stays with the safe/affirmative or with Annuler).
 *
 * FLOW: never show a modal during a live broadcast. Confirmations belong to
 * management screens (Profils / Channels), not the dashboard mid-stream.
 */
export function ContentDialog({
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
  className = "",
}) {
  if (!open) return null;
  return (
    <div
      className={"n-scrim" + (contained ? " n-scrim--contained" : "")}
      role="presentation"
      onClick={(e) => {
        if (e.target === e.currentTarget && onClose) onClose();
      }}
    >
      <div
        className={`n-dialog ${className}`.trim()}
        role="dialog"
        aria-modal="true"
        aria-label={title}
      >
        <div className="n-dialog__body">
          {title && <h2 className="n-dialog__title">{title}</h2>}
          {children && <div className="n-dialog__content">{children}</div>}
        </div>
        {(primaryText || secondaryText || closeText) && (
          <div className="n-dialog__commands">
            {primaryText && (
              <Button variant={primaryVariant} onClick={onPrimary}>
                {primaryText}
              </Button>
            )}
            {secondaryText && <Button onClick={onSecondary}>{secondaryText}</Button>}
            {closeText && (
              <Button variant="subtle" onClick={onClose}>
                {closeText}
              </Button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}
