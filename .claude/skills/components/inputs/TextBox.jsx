import React from "react";

/**
 * TextBox — Fluent single- or multi-line text field with an optional header
 * label (WinUI TextBox `Header`). Set `mono` for command/URL fields.
 *
 * Validation is never re-authored in the UI: pass the domain's message straight
 * into `error` (e.g. a DomainException text). This is a thin presentational field.
 */
export function TextBox({
  header,
  hint,
  error,
  multiline = false,
  mono = false,
  id,
  className = "",
  ...rest
}) {
  const reactId = React.useId();
  const fieldId = id || reactId;
  const Tag = multiline ? "textarea" : "input";
  const inputCls = ["n-input", mono && "n-input--mono", className]
    .filter(Boolean)
    .join(" ");
  return (
    <div className="n-field">
      {header && (
        <label className="n-field__label" htmlFor={fieldId}>
          {header}
        </label>
      )}
      <Tag
        id={fieldId}
        className={inputCls}
        aria-invalid={error ? true : undefined}
        {...(multiline ? {} : { type: "text" })}
        {...rest}
      />
      {error ? (
        <span className="n-field__hint n-field__hint--error">{error}</span>
      ) : hint ? (
        <span className="n-field__hint">{hint}</span>
      ) : null}
    </div>
  );
}
