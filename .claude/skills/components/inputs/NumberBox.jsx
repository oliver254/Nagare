import React from "react";
import { Icon } from "../icon/Icon.jsx";

/**
 * NumberBox — Fluent number field with compact spin buttons (WinUI NumberBox,
 * SpinButtonPlacementMode="Compact"). Used across the profile editor
 * (bitrate / maxrate / bufsize / GOP / keyint / resolution / fps).
 *
 * `onChange(value: number)` receives the parsed, clamped number — not a raw
 * event. Range enforcement here is only convenience; the DOMAIN owns the real
 * invariants (E1-E8) and its message goes into `error`.
 */
export function NumberBox({
  header,
  hint,
  error,
  min,
  max,
  step = 1,
  value,
  defaultValue = 0,
  onChange,
  disabled = false,
  id,
  className = "",
  ...rest
}) {
  const reactId = React.useId();
  const fieldId = id || reactId;
  const controlled = value !== undefined;
  const [internal, setInternal] = React.useState(defaultValue);
  const current = controlled ? value : internal;

  const clamp = (n) => {
    if (min != null && n < min) n = min;
    if (max != null && n > max) n = max;
    return n;
  };
  const set = (n) => {
    const c = clamp(Number.isNaN(n) ? 0 : n);
    if (!controlled) setInternal(c);
    if (onChange) onChange(c);
  };

  return (
    <div className="n-field">
      {header && (
        <label className="n-field__label" htmlFor={fieldId}>
          {header}
        </label>
      )}
      <div className="n-inputwrap n-inputwrap--num">
        <input
          id={fieldId}
          className={`n-input ${className}`.trim()}
          type="text"
          inputMode="numeric"
          value={current}
          disabled={disabled}
          aria-invalid={error ? true : undefined}
          onChange={(e) => set(parseFloat(e.target.value))}
          {...rest}
        />
        <div className="n-input-affix">
          <div className="n-spin">
            <button
              type="button"
              className="n-spin__btn"
              tabIndex={-1}
              aria-label="Augmenter"
              disabled={disabled}
              onClick={() => set(current + step)}
            >
              <Icon name="chevron-up" size={12} />
            </button>
            <button
              type="button"
              className="n-spin__btn"
              tabIndex={-1}
              aria-label="Diminuer"
              disabled={disabled}
              onClick={() => set(current - step)}
            >
              <Icon name="chevron-down" size={12} />
            </button>
          </div>
        </div>
      </div>
      {error ? (
        <span className="n-field__hint n-field__hint--error">{error}</span>
      ) : hint ? (
        <span className="n-field__hint">{hint}</span>
      ) : null}
    </div>
  );
}
