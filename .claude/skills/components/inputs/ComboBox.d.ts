import * as React from "react";

export interface ComboOption {
  value: string | number;
  label: string;
}

/**
 * ComboBox — Fluent dropdown select. Options come from the domain (codec
 * presets, sample rates, platforms). Popup is an Acrylic flyout.
 *
 * @startingPoint section="Forms" subtitle="Dropdown select with Acrylic popup" viewport="700x150"
 */
export interface ComboBoxProps {
  header?: string;
  hint?: string;
  /** Options: strings/numbers, or { value, label } objects. */
  items: Array<string | number | ComboOption>;
  /** Selected value (matched against option value). */
  value?: string | number;
  onChange?: (value: string | number) => void;
  placeholder?: string;
  disabled?: boolean;
  id?: string;
  className?: string;
}

export function ComboBox(props: ComboBoxProps): JSX.Element;
