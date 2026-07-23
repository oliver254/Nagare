import * as React from "react";

/**
 * StatusBadge — status/health as shape + icon + text (never color alone).
 * Maps to SessionStatus: Starting/Reconnecting → attention, Running healthy →
 * success (or `live`), Stopped → neutral, Failed / speed<1.0x → critical.
 *
 * @startingPoint section="Streaming" subtitle="Session status & health badge" viewport="700x150"
 */
export interface StatusBadgeProps {
  /** Tone. `live` adds a pulsing dot; critical is reserved for real anomalies. */
  tone?: "neutral" | "success" | "caution" | "critical" | "attention" | "live";
  /** Override the tone's default icon (Lucide name or node). */
  icon?: string | React.ReactNode;
  /** Force the dot shape instead of an icon. */
  dot?: boolean;
  children?: React.ReactNode;
  className?: string;
}

export function StatusBadge(props: StatusBadgeProps): JSX.Element;
