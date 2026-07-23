import React from "react";
import { Icon } from "../icon/Icon.jsx";

/**
 * LaunchChecklist — the fix for the app's worst gap: a disabled "Démarrer" that
 * says nothing. This makes readiness visible without a click (Zeigarnik +
 * Goal-Gradient): Environnement ✓ · Fichier ✓ · Profil ✓ · Channel ✓. Each
 * pending item names exactly what is still missing.
 *
 * Drive `items[].done` from the same facts the preflight decides on; keep the
 * verdict itself in the Application layer.
 */
export function LaunchChecklist({
  title = "Prêt à diffuser ?",
  items = [],
  showProgress = true,
  className = "",
}) {
  const done = items.filter((i) => i.done).length;
  const pct = items.length ? Math.round((done / items.length) * 100) : 0;
  return (
    <div className={`n-check ${className}`.trim()}>
      <div className="n-check__head">
        <span className="n-check__title">{title}</span>
        <span className="n-check__count">{done}/{items.length}</span>
      </div>
      {items.map((it, i) => (
        <div
          key={i}
          className={"n-check__item" + (it.done ? " n-check__item--done" : "")}
        >
          <Icon
            className="n-check__mark"
            name={it.done ? "circle-check" : "circle"}
            size={16}
          />
          <span>{it.label}</span>
        </div>
      ))}
      {showProgress && (
        <div className="n-check__bar">
          <div className="n-check__bar-fill" style={{ width: `${pct}%` }} />
        </div>
      )}
    </div>
  );
}
