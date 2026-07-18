import * as React from "react";

/**
 * InfoBar — Fluent inline message. Surface the domain / process message verbatim.
 * Keep it in the flow of the relevant task; never blocking during a broadcast.
 *
 * @startingPoint section="Feedback" subtitle="Informational · success · warning · error" viewport="700x150"
 */
export interface InfoBarProps {
  severity?: "informational" | "success" | "warning" | "error";
  title?: string;
  message?: string;
  /** Show a close (x) button. Default false — a blocking environment error stays open. */
  isClosable?: boolean;
  onClose?: () => void;
  /** Action buttons rendered under the message. */
  actions?: React.ReactNode;
  children?: React.ReactNode;
  className?: string;
}

export function InfoBar(props: InfoBarProps): JSX.Element;
