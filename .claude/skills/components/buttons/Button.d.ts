import * as React from "react";

/**
 * Button — Fluent/WinUI button. The `accent` variant is the single highlighted
 * action per screen (Von Restorff); `standard` and `subtle` carry everything
 * else. `danger` tints the label critical for destructive actions (which live
 * behind confirmation, never adjacent to the primary).
 *
 * @startingPoint section="Forms" subtitle="Accent · standard · subtle · hyperlink" viewport="700x150"
 */
export interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  /** Visual role. Default "standard". Use "accent" for the one primary action per screen. */
  variant?: "standard" | "accent" | "subtle" | "hyperlink" | "danger";
  /** "standard" (32px) or "small" (24px). Default "standard". */
  size?: "standard" | "small";
  /** Leading/trailing icon: a Lucide glyph name (string) or a React node. */
  icon?: string | React.ReactNode;
  /** Where the icon sits relative to the label. Default "start". */
  iconPosition?: "start" | "end";
  disabled?: boolean;
  children?: React.ReactNode;
}

export function Button(props: ButtonProps): JSX.Element;
