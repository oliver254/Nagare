import * as React from "react";

/**
 * StatTile — one live ffmpeg stat as a scannable tile (fps, kbits/s, speed,
 * drops, reconnexions). `warning` tints it critical for the health signal.
 *
 * @startingPoint section="Streaming" subtitle="Live ffmpeg stat tile" viewport="700x150"
 */
export interface StatTileProps {
  label: string;
  /** The number/text to show (already formatted, e.g. "1,02"). */
  value: React.ReactNode;
  /** Unit suffix: "fps", "kbits/s", "x", "drops", "reconnexions". */
  unit?: string;
  /** Optional Lucide glyph beside the label. */
  icon?: string;
  /** Critical tint — use only for a real health anomaly. */
  warning?: boolean;
  className?: string;
}

export function StatTile(props: StatTileProps): JSX.Element;
