import * as React from "react";

/**
 * NumberBox — Fluent number field with compact spin buttons. `onChange` gets the
 * parsed number. The domain owns the real invariants; surface its message in `error`.
 *
 * @startingPoint section="Forms" subtitle="Number field with compact spin buttons" viewport="700x150"
 */
export interface NumberBoxProps {
  header?: string;
  hint?: string;
  /** Error text (domain message). Also sets aria-invalid. */
  error?: string;
  min?: number;
  max?: number;
  /** Increment for the spin buttons. Default 1. */
  step?: number;
  /** Controlled value. */
  value?: number;
  /** Uncontrolled initial value. Default 0. */
  defaultValue?: number;
  /** Receives the parsed, clamped number. */
  onChange?: (value: number) => void;
  disabled?: boolean;
  id?: string;
  className?: string;
}

export function NumberBox(props: NumberBoxProps): JSX.Element;
