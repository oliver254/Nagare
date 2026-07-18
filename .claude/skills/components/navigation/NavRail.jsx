import React from "react";
import { Icon } from "../icon/Icon.jsx";

/**
 * NavRail — the Fluent NavigationView left pane (Jakob's Law: no bespoke nav).
 * Three destinations today — Tableau de bord / Profils / Channels — with a slot
 * kept for a fourth, Planifications (iteration 2), shown disabled with a
 * "Bientôt" tag so its place is reserved.
 */
export function NavRail({
  items = [],
  selected,
  onSelect,
  brand = "Nagare",
  brandMark = "流",
  footer,
  className = "",
}) {
  return (
    <nav className={`n-nav ${className}`.trim()} aria-label="Navigation principale">
      <div className="n-nav__brand">
        <span className="n-nav__brand-mark" aria-hidden="true">{brandMark}</span>
        <span className="n-nav__brand-name">{brand}</span>
      </div>
      {items.map((it) => {
        const isSel = it.tag === selected;
        return (
          <button
            key={it.tag}
            type="button"
            className={"n-nav__item" + (isSel ? " n-nav__item--selected" : "")}
            aria-current={isSel ? "page" : undefined}
            aria-disabled={it.disabled || undefined}
            onClick={() => !it.disabled && onSelect && onSelect(it.tag)}
          >
            {it.icon && <Icon className="n-nav__item-icon" name={it.icon} size={18} />}
            <span className="n-nav__item-label">{it.label}</span>
            {it.soon && (
              <span className="n-nav__soon">
                {typeof it.soon === "string" ? it.soon : "Bientôt"}
              </span>
            )}
          </button>
        );
      })}
      {footer && (
        <>
          <div className="n-nav__spacer" />
          {footer}
        </>
      )}
    </nav>
  );
}
