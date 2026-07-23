import React from "react";
import { Icon } from "../icon/Icon.jsx";

/**
 * PasswordBox — Fluent password field for the stream key.
 *
 * SECURITY (ADR-0005): a SAVED key is never loaded back into this box — the DTO
 * doesn't carry it and no query can return it. The reveal toggle only unmasks
 * what the user is typing right now; an empty field on save means "keep the
 * current key". Never pre-fill, never log, never copy the key.
 */
export function PasswordBox({
  header,
  hint,
  revealable = true,
  id,
  className = "",
  ...rest
}) {
  const reactId = React.useId();
  const fieldId = id || reactId;
  const [reveal, setReveal] = React.useState(false);
  return (
    <div className="n-field">
      {header && (
        <label className="n-field__label" htmlFor={fieldId}>
          {header}
        </label>
      )}
      <div className="n-inputwrap">
        <input
          id={fieldId}
          className={`n-input ${className}`.trim()}
          type={reveal ? "text" : "password"}
          {...rest}
        />
        {revealable && (
          <div className="n-input-affix">
            <button
              type="button"
              className="n-reveal"
              aria-label={reveal ? "Masquer la clé" : "Afficher la clé"}
              aria-pressed={reveal}
              onMouseDown={(e) => e.preventDefault()}
              onClick={() => setReveal((v) => !v)}
            >
              <Icon name={reveal ? "eye-off" : "eye"} size={16} />
            </button>
          </div>
        )}
      </div>
      {hint && <span className="n-field__hint">{hint}</span>}
    </div>
  );
}
