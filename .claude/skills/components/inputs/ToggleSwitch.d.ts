import * as React from "react";

/**
 * ToggleSwitch — Fluent on/off switch. Binds to a boolean; `onChange(checked)`.
 *
 * @startingPoint section="Forms" subtitle="On/off switch with optional state text" viewport="700x150"
 */
export interface ToggleSwitchProps {
  /** Label shown above the switch (WinUI Header). */
  header?: string;
  /** Fixed text to the right of the switch. */
  label?: string;
  /** State text shown when `showStateText` and no fixed `label`. */
  onText?: string;
  offText?: string;
  /** Show on/off state text to the right. Default false. */
  showStateText?: boolean;
  /** Controlled checked state. */
  checked?: boolean;
  defaultChecked?: boolean;
  onChange?: (checked: boolean) => void;
  disabled?: boolean;
  id?: string;
  className?: string;
}

export function ToggleSwitch(props: ToggleSwitchProps): JSX.Element;
