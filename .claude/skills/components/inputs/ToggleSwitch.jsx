import React from "react";

/**
 * ToggleSwitch — Fluent on/off switch with an optional header. Used for the
 * profile editor's boolean options (force resolution, force fps, -re,
 * -stream_loop -1). `onChange(checked: boolean)`.
 */
export function ToggleSwitch({
  header,
  label,
  onText = "Activé",
  offText = "Désactivé",
  showStateText = false,
  checked,
  defaultChecked = false,
  onChange,
  disabled = false,
  id,
  className = "",
}) {
  const reactId = React.useId();
  const fieldId = id || reactId;
  const controlled = checked !== undefined;
  const [internal, setInternal] = React.useState(defaultChecked);
  const isOn = controlled ? checked : internal;

  const toggle = (e) => {
    const v = e.target.checked;
    if (!controlled) setInternal(v);
    if (onChange) onChange(v);
  };

  const text = label != null ? label : showStateText ? (isOn ? onText : offText) : null;

  return (
    <div className={`n-field ${className}`.trim()}>
      {header && <span className="n-field__label">{header}</span>}
      <label className="n-toggle" aria-disabled={disabled || undefined}>
        <input
          id={fieldId}
          type="checkbox"
          role="switch"
          checked={isOn}
          disabled={disabled}
          onChange={toggle}
        />
        <span className="n-toggle__track">
          <span className="n-toggle__knob" />
        </span>
        {text != null && <span className="n-toggle__label">{text}</span>}
      </label>
    </div>
  );
}
