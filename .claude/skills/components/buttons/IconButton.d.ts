import * as React from "react";

/**
 * IconButton — square, icon-only button for toolbars and list rows. `label` is
 * required and supplies both the accessible name and the tooltip.
 */
export interface IconButtonProps
  extends Omit<React.ButtonHTMLAttributes<HTMLButtonElement>, "children"> {
  /** Lucide glyph name (string) or a React node. */
  icon: string | React.ReactNode;
  /** Accessible name + tooltip. Required — never ship an unlabeled icon button. */
  label: string;
  /** Default "subtle". */
  variant?: "standard" | "subtle" | "accent";
  disabled?: boolean;
}

export function IconButton(props: IconButtonProps): JSX.Element;
