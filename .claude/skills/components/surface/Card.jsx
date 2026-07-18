import React from "react";
import { Icon } from "../icon/Icon.jsx";

/**
 * Card — a bounded, belted region (Common Region + Uniform Connectedness). This
 * is the antidote to the current dashboard's "flat wall of stacked controls":
 * the dashboard is cut into Source / Diffusion / Santé / Journal cards, each
 * with a quiet title and everything it owns inside one stroke.
 */
export function Card({ title, icon, badge, children, className = "", ...rest }) {
  const glyph = icon
    ? typeof icon === "string"
      ? <Icon className="n-card__icon" name={icon} size={18} />
      : icon
    : null;
  const hasHeader = title || glyph || badge;
  return (
    <section className={`n-card ${className}`.trim()} {...rest}>
      {hasHeader && (
        <header className="n-card__header">
          {glyph}
          {title && <h3 className="n-card__title">{title}</h3>}
          {badge && <div className="n-card__badge">{badge}</div>}
        </header>
      )}
      {children}
    </section>
  );
}
