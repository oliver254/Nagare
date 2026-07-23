import * as React from "react";

/**
 * ContentDialog — Fluent modal dialog. Nagare's only modal: destructive
 * confirmation that names the object. Never shown during a live broadcast.
 *
 * @startingPoint section="Feedback" subtitle="Modal confirmation dialog" viewport="700x360"
 */
export interface ContentDialogProps {
  /** Whether the dialog is shown. Default true. */
  open?: boolean;
  title?: string;
  children?: React.ReactNode;
  /** Primary command label. */
  primaryText?: string;
  /** Secondary command label. */
  secondaryText?: string;
  /** Close/cancel command label. */
  closeText?: string;
  /** Primary button variant. Use "danger" for destructive confirms, never "accent". Default "accent". */
  primaryVariant?: "accent" | "standard" | "danger";
  onPrimary?: () => void;
  onSecondary?: () => void;
  onClose?: () => void;
  /** Render inside the nearest positioned ancestor (position:absolute) instead of fixed — for embedding/previews. */
  contained?: boolean;
  className?: string;
}

export function ContentDialog(props: ContentDialogProps): JSX.Element | null;
