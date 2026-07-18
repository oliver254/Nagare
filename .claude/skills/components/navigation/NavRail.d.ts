import * as React from "react";

export interface NavItem {
  /** Stable id, matched against `selected`. */
  tag: string;
  label: string;
  /** Lucide glyph name. */
  icon?: string;
  /** Disable (e.g. an upcoming destination). */
  disabled?: boolean;
  /** Show an "upcoming" tag; pass a string to override "Bientôt". */
  soon?: boolean | string;
}

/**
 * NavRail — Fluent NavigationView left pane. Keeps a reserved, disabled slot for
 * the upcoming Planifications page.
 *
 * @startingPoint section="Navigation" subtitle="Fluent NavigationView left rail" viewport="300x460"
 */
export interface NavRailProps {
  items: NavItem[];
  /** Selected item tag. */
  selected?: string;
  onSelect?: (tag: string) => void;
  /** Wordmark name. Default "Nagare". */
  brand?: string;
  /** Wordmark glyph. Default "流". */
  brandMark?: string;
  /** Pinned footer content. */
  footer?: React.ReactNode;
  className?: string;
}

export function NavRail(props: NavRailProps): JSX.Element;
