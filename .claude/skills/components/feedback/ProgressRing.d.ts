import * as React from "react";

/**
 * ProgressRing — Fluent indeterminate spinner, bound to IsBusy. Keeps every
 * action under the 400 ms Doherty threshold visibly responsive.
 *
 * @startingPoint section="Feedback" subtitle="Indeterminate busy spinner" viewport="700x150"
 */
export interface ProgressRingProps
  extends React.HTMLAttributes<HTMLSpanElement> {
  /** Diameter in px. Default 32. */
  size?: number;
  /** Ring stroke width in px. Default ~size/10. */
  thickness?: number;
  /** Optional text beside the ring (e.g. "Démarrage…", "Analyse du fichier…"). */
  label?: string;
}

export function ProgressRing(props: ProgressRingProps): JSX.Element;
