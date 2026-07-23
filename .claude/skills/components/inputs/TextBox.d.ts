import * as React from "react";

/**
 * TextBox — Fluent single-/multi-line text field with an optional header label.
 * Presentational only; pass domain validation text into `error`.
 *
 * @startingPoint section="Forms" subtitle="Header label, focus underline, mono variant" viewport="700x150"
 */
export interface TextBoxProps
  extends React.InputHTMLAttributes<HTMLInputElement & HTMLTextAreaElement> {
  /** Label shown above the field (WinUI Header). */
  header?: string;
  /** Helper text below the field. */
  hint?: string;
  /** Error text below the field (also sets aria-invalid). Pass the domain message verbatim. */
  error?: string;
  /** Render a multi-line <textarea>. Default false. */
  multiline?: boolean;
  /** Monospace font — for command previews and URLs. Default false. */
  mono?: boolean;
}

export function TextBox(props: TextBoxProps): JSX.Element;
