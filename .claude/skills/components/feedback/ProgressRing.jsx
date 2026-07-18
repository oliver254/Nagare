import React from "react";

/**
 * ProgressRing — Fluent indeterminate spinner. Bind it to ViewModelBase.IsBusy
 * so nothing crosses the 400 ms Doherty threshold in silence (environment probe,
 * ffprobe). Optionally pairs with a "Démarrage…" style label.
 */
export function ProgressRing({
  size = 32,
  thickness,
  label,
  className = "",
  ...rest
}) {
  const t = thickness || Math.max(2, Math.round(size / 10));
  const ring = (
    <span
      className={`n-ring ${className}`.trim()}
      style={{ width: size, height: size, borderWidth: t }}
      role="progressbar"
      aria-label={label || "Chargement"}
      {...rest}
    />
  );
  if (label) {
    return (
      <span className="n-busy">
        {ring}
        <span>{label}</span>
      </span>
    );
  }
  return ring;
}
