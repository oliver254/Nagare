import * as React from "react";

/**
 * Card — bounded region with an optional titled header. Groups related controls
 * so the dashboard reads as Source / Diffusion / Santé / Journal, not a flat wall.
 *
 * @startingPoint section="Surface" subtitle="Belted region with optional header" viewport="700x260"
 */
export interface CardProps extends React.HTMLAttributes<HTMLElement> {
  /** Quiet card title (BodyStrong). */
  title?: string;
  /** Leading header icon: Lucide name or node. */
  icon?: string | React.ReactNode;
  /** Right-aligned header slot (e.g. a StatusBadge). */
  badge?: React.ReactNode;
  children?: React.ReactNode;
}

export function Card(props: CardProps): JSX.Element;
