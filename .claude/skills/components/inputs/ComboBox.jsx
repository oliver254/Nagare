import React from "react";
import { Icon } from "../icon/Icon.jsx";

/**
 * ComboBox — Fluent dropdown select with an optional header label. The popup is
 * an Acrylic flyout; the selected option shows an accent bar + check.
 *
 * The list of options is READ FROM the domain (presets per codec, sample rates,
 * platforms, enums) — never a second hard-coded copy of a domain rule.
 */
export function ComboBox({
  header,
  hint,
  items = [],
  value,
  onChange,
  placeholder = "Sélectionner…",
  disabled = false,
  id,
  className = "",
}) {
  const reactId = React.useId();
  const fieldId = id || reactId;
  const [open, setOpen] = React.useState(false);
  const rootRef = React.useRef(null);

  const options = items.map((it) =>
    it && typeof it === "object" ? it : { value: it, label: String(it) }
  );
  const selected = options.find((o) => o.value === value);

  React.useEffect(() => {
    if (!open) return;
    const onDoc = (e) => {
      if (rootRef.current && !rootRef.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener("mousedown", onDoc);
    return () => document.removeEventListener("mousedown", onDoc);
  }, [open]);

  const pick = (o) => {
    setOpen(false);
    if (onChange) onChange(o.value);
  };

  return (
    <div className={`n-field ${className}`.trim()}>
      {header && (
        <label className="n-field__label" htmlFor={fieldId}>
          {header}
        </label>
      )}
      <div className="n-combo" ref={rootRef}>
        <button
          id={fieldId}
          type="button"
          className="n-combo__button"
          aria-haspopup="listbox"
          aria-expanded={open}
          aria-disabled={disabled || undefined}
          onClick={() => !disabled && setOpen((o) => !o)}
        >
          <span
            className={
              "n-combo__value" +
              (selected ? "" : " n-combo__value--placeholder")
            }
          >
            {selected ? selected.label : placeholder}
          </span>
          <Icon className="n-combo__chev" name="chevron-down" size={16} />
        </button>
        {open && (
          <ul className="n-combo__pop" role="listbox">
            {options.map((o) => (
              <li
                key={String(o.value)}
                role="option"
                aria-selected={o.value === value}
                className={
                  "n-combo__opt" +
                  (o.value === value ? " n-combo__opt--selected" : "")
                }
                onClick={() => pick(o)}
              >
                <span>{o.label}</span>
                {o.value === value && (
                  <Icon name="check" size={16} style={{ marginLeft: "auto" }} />
                )}
              </li>
            ))}
          </ul>
        )}
      </div>
      {hint && <span className="n-field__hint">{hint}</span>}
    </div>
  );
}
