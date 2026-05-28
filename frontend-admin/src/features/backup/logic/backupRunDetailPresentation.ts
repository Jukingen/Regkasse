import type { ReactNode } from "react";
import {
  CheckCircleOutlined,
  ClockCircleOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  SyncOutlined,
} from "@ant-design/icons";
import { createElement } from "react";
import { BackupRunStatus } from "@/api/generated/model/backupRunStatus";
import { BackupTriggerSource } from "@/api/generated/model/backupTriggerSource";
import type { DerivedPipelineStepState } from "@/features/backup-dr/logic/backupPipelineDerived";
import {
  computeBackupRunDurationMinutes,
  resolveBackupRunStatusUiKey,
} from "@/features/backup/logic/backupRunTablePresentation";

export type BackupRunStatusTagPresentation = {
  color: string;
  icon: ReactNode;
  labelKey: string;
};

export function backupRunStatusTagPresentation(
  status: number | undefined,
): BackupRunStatusTagPresentation {
  const uiKey = resolveBackupRunStatusUiKey(status);
  switch (uiKey) {
    case "succeeded":
      return {
        color: "success",
        icon: createElement(CheckCircleOutlined),
        labelKey: "backupDr.runsTable.statusLabels.succeeded",
      };
    case "failed":
      return {
        color: "error",
        icon: createElement(CloseCircleOutlined),
        labelKey: "backupDr.runsTable.statusLabels.failed",
      };
    case "verificationFailed":
      return {
        color: "warning",
        icon: createElement(ExclamationCircleOutlined),
        labelKey: "backupDr.runsTable.statusLabels.verificationFailed",
      };
    case "running":
    case "awaitingVerification":
      return {
        color: "processing",
        icon: createElement(SyncOutlined, { spin: true }),
        labelKey:
          uiKey === "awaitingVerification"
            ? "backupDr.runsTable.statusLabels.awaitingVerification"
            : "backupDr.runsTable.statusLabels.running",
      };
    case "queued":
      return {
        color: "default",
        icon: createElement(ClockCircleOutlined),
        labelKey: "backupDr.runsTable.statusLabels.queued",
      };
    case "cancelled":
      return {
        color: "default",
        icon: createElement(CloseCircleOutlined),
        labelKey: "backupDr.runsTable.statusLabels.cancelled",
      };
    default:
      return {
        color: "default",
        icon: null,
        labelKey: "backupDr.summary.unknown",
      };
  }
}

export function backupTriggerSourceLabelKey(
  source: number | undefined,
): string {
  switch (source) {
    case BackupTriggerSource.NUMBER_0:
      return "backupDr.detailModal.triggerSource.manual";
    case BackupTriggerSource.NUMBER_1:
      return "backupDr.detailModal.triggerSource.scheduled";
    case BackupTriggerSource.NUMBER_2:
      return "backupDr.detailModal.triggerSource.operatorApi";
    default:
      return "backupDr.summary.unknown";
  }
}

export function formatBackupRunDuration(
  startedAt: string | null | undefined,
  completedAt: string | null | undefined,
  t: (key: string, options?: Record<string, string | number>) => string,
): string {
  const minutes = computeBackupRunDurationMinutes(startedAt, completedAt);
  if (minutes === undefined) return t("backupDr.runsTable.noDuration");
  return t("backupDr.runsTable.durationMinutes", { minutes: minutes.toFixed(1) });
}

export function pipelineStepTimelineColor(
  state: DerivedPipelineStepState,
): "green" | "red" | "blue" | "gray" | "orange" {
  switch (state) {
    case "success":
      return "green";
    case "failed":
      return "red";
    case "running":
      return "blue";
    case "degraded":
      return "orange";
    case "skipped":
      return "gray";
    default:
      return "gray";
  }
}

export function isBackupRunSucceeded(status: number | undefined): boolean {
  return status === BackupRunStatus.NUMBER_3;
}
