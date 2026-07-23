import * as React from "react";

export interface ChecklistItem {
  label: string;
  done: boolean;
}

/**
 * LaunchChecklist — makes broadcast readiness visible so a disabled "Démarrer"
 * always says what is missing (Zeigarnik / Goal-Gradient). Fed by the same facts
 * the Application preflight decides on.
 *
 * @startingPoint section="Streaming" subtitle="Broadcast readiness checklist" viewport="700x230"
 */
export interface LaunchChecklistProps {
  /** Heading. Default "Prêt à diffuser ?". */
  title?: string;
  items: ChecklistItem[];
  /** Show the goal-gradient progress bar. Default true. */
  showProgress?: boolean;
  className?: string;
}

export function LaunchChecklist(props: LaunchChecklistProps): JSX.Element;
