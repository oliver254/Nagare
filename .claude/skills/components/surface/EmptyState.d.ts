import * as React from "react";

/**
 * EmptyState — documentation-as-UI for empty lists / first run. States the next
 * action and offers a direct CTA.
 *
 * @startingPoint section="Surface" subtitle="First-run empty state with CTA" viewport="700x300"
 */
export interface EmptyStateProps {
  /** Lucide name or node. Default "inbox". */
  icon?: string | React.ReactNode;
  title?: string;
  message?: string;
  /** CTA, usually an accent Button. */
  action?: React.ReactNode;
  className?: string;
}

export function EmptyState(props: EmptyStateProps): JSX.Element;
