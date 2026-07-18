import * as React from "react";

/**
 * PasswordBox — Fluent masked field for the stream key. A saved key is never
 * shown again (ADR-0005); the reveal toggle only unmasks the in-progress value.
 */
export interface PasswordBoxProps
  extends React.InputHTMLAttributes<HTMLInputElement> {
  /** Label above the field (WinUI Header). */
  header?: string;
  /** Helper text below — e.g. "Laisser vide conserve la clé actuelle…". */
  hint?: string;
  /** Show the reveal (eye) toggle. Default true. Reveals only what is being typed. */
  revealable?: boolean;
}

export function PasswordBox(props: PasswordBoxProps): JSX.Element;
