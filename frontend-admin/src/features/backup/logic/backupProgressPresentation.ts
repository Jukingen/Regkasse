/**
 * Maps a backup run (+ optional pipeline steps / avg duration) to UI progress fields.
 * Percent is indicative only — workers do not stream fine-grained progress.
 */

import type { BackupRunResponseDto } from "@/api/generated/model";
import { BackupRunStatus } from "@/api/generated/model/backupRunStatus";
import type { DerivedPipelineStep } from "@/features/backup-dr/logic/backupPipelineDerived";
import { resolveBackupPipelineStepsForUi } from "@/features/backup-dr/logic/backupPipelineDerived";
import { isBackupPipelineClientFallbackEnabled } from "@/features/backup-dr/logic/backupPipelineEnv";

export type BackupProgressBarStatus = "active" | "success" | "exception" | "normal";

export type BackupProgressViewModel = {
  percentage: number;
  progressStatus: BackupProgressBarStatus;
  /** 1-based index of the current (or last) applicable step; 0 when unknown. */
  currentStep: number;
  totalSteps: number;
  currentStepTitleKey: string | null;
  isInProgress: boolean;
  isError: boolean;
  isTerminal: boolean;
  /** Rough remaining duration from historical average; undefined when unknown. */
  estimatedRemainingMs: number | undefined;
  statusTitleKey: string;
  bodyKey: string | null;
};

function isInProgressStatus(status: number | undefined): boolean {
  return (
    status === BackupRunStatus.NUMBER_0 ||
    status === BackupRunStatus.NUMBER_1 ||
    status === BackupRunStatus.NUMBER_2
  );
}

function isErrorStatus(status: number | undefined): boolean {
  return status === BackupRunStatus.NUMBER_4 || status === BackupRunStatus.NUMBER_5;
}

function isTerminalStatus(status: number | undefined): boolean {
  return (
    status === BackupRunStatus.NUMBER_3 ||
    status === BackupRunStatus.NUMBER_4 ||
    status === BackupRunStatus.NUMBER_5 ||
    status === BackupRunStatus.NUMBER_6
  );
}

function statusTitleKey(status: number | undefined): string {
  switch (status) {
    case BackupRunStatus.NUMBER_0:
      return "backupDr.progress.titleQueued";
    case BackupRunStatus.NUMBER_1:
      return "backupDr.progress.titleRunning";
    case BackupRunStatus.NUMBER_2:
      return "backupDr.progress.titleAwaiting";
    case BackupRunStatus.NUMBER_3:
      return "backupDr.progress.finishedOk";
    case BackupRunStatus.NUMBER_4:
      return "backupDr.progress.finishedFailed";
    case BackupRunStatus.NUMBER_5:
      return "backupDr.progress.finishedVerificationFailed";
    case BackupRunStatus.NUMBER_6:
      return "backupDr.progress.finishedCancelled";
    default:
      return "backupDr.summary.unknown";
  }
}

function statusBodyKey(status: number | undefined): string | null {
  switch (status) {
    case BackupRunStatus.NUMBER_0:
      return "backupDr.progress.bodyQueued";
    case BackupRunStatus.NUMBER_1:
      return "backupDr.progress.bodyRunning";
    case BackupRunStatus.NUMBER_2:
      return "backupDr.progress.bodyAwaiting";
    default:
      return null;
  }
}

/** Fallback percent when pipeline steps are unavailable. */
export function percentFromRunStatus(status: number | undefined): number {
  switch (status) {
    case BackupRunStatus.NUMBER_0:
      return 8;
    case BackupRunStatus.NUMBER_1:
      return 45;
    case BackupRunStatus.NUMBER_2:
      return 85;
    case BackupRunStatus.NUMBER_3:
      return 100;
    case BackupRunStatus.NUMBER_4:
    case BackupRunStatus.NUMBER_5:
    case BackupRunStatus.NUMBER_6:
      return 100;
    default:
      return 0;
  }
}

export function percentFromPipelineSteps(steps: DerivedPipelineStep[]): {
  percentage: number;
  currentStep: number;
  totalSteps: number;
  currentStepTitleKey: string | null;
} {
  const applicable = steps.filter((s) => s.state !== "skipped");
  const totalSteps = applicable.length > 0 ? applicable.length : steps.length;
  if (totalSteps === 0) {
    return { percentage: 0, currentStep: 0, totalSteps: 0, currentStepTitleKey: null };
  }

  let completedWeight = 0;
  let currentIndex = 0;
  let currentTitleKey: string | null = null;

  applicable.forEach((step, idx) => {
    if (step.state === "success" || step.state === "degraded") {
      completedWeight += 1;
      currentIndex = idx + 1;
      currentTitleKey = step.titleKey;
    } else if (step.state === "running") {
      completedWeight += 0.5;
      currentIndex = idx + 1;
      currentTitleKey = step.titleKey;
    } else if (step.state === "failed") {
      currentIndex = idx + 1;
      currentTitleKey = step.titleKey;
    } else if (step.state === "pending" && currentIndex === 0) {
      currentIndex = idx + 1;
      currentTitleKey = step.titleKey;
    }
  });

  if (currentIndex === 0 && applicable.length > 0) {
    currentIndex = 1;
    currentTitleKey = applicable[0]?.titleKey ?? null;
  }

  const percentage = Math.min(100, Math.round((completedWeight / totalSteps) * 100));
  return { percentage, currentStep: currentIndex, totalSteps, currentStepTitleKey: currentTitleKey };
}

export function estimateRemainingMs(params: {
  status: number | undefined;
  requestedAt?: string | null;
  startedAt?: string | null;
  averageSucceededDurationSeconds?: number | null;
  nowMs?: number;
}): number | undefined {
  const avgSec = params.averageSucceededDurationSeconds;
  if (avgSec == null || avgSec <= 0) return undefined;

  const now = params.nowMs ?? Date.now();
  if (
    (params.status === BackupRunStatus.NUMBER_1 ||
      params.status === BackupRunStatus.NUMBER_2) &&
    params.startedAt
  ) {
    const started = new Date(params.startedAt).getTime();
    if (Number.isNaN(started)) return undefined;
    return Math.max(0, avgSec * 1000 - (now - started));
  }

  if (params.status === BackupRunStatus.NUMBER_0) {
    return Math.round(avgSec * 1000);
  }

  return undefined;
}

export function buildBackupProgressViewModel(
  run: BackupRunResponseDto | null | undefined,
  options?: {
    averageSucceededDurationSeconds?: number | null;
    /** Prefer server estimate from GET status/latest when present. */
    estimatedRemainingSecondsFromApi?: number | null;
    nowMs?: number;
    allowClientPipelineFallback?: boolean;
  },
): BackupProgressViewModel | null {
  if (!run?.id) return null;

  const status = run.status;
  const allowFallback =
    options?.allowClientPipelineFallback ?? isBackupPipelineClientFallbackEnabled();
  const pipeline = resolveBackupPipelineStepsForUi(run, run, undefined, {
    allowClientFallback: allowFallback,
  });

  let percentage = percentFromRunStatus(status);
  let currentStep = 0;
  let totalSteps = 0;
  let currentStepTitleKey: string | null = null;

  if (pipeline.steps.length > 0) {
    const fromPipeline = percentFromPipelineSteps(pipeline.steps);
    percentage = fromPipeline.percentage;
    currentStep = fromPipeline.currentStep;
    totalSteps = fromPipeline.totalSteps;
    currentStepTitleKey = fromPipeline.currentStepTitleKey;
    if (status === BackupRunStatus.NUMBER_3) percentage = 100;
  } else {
    // Coarse 3-phase ladder when no pipeline projection
    totalSteps = 3;
    if (status === BackupRunStatus.NUMBER_0) currentStep = 1;
    else if (status === BackupRunStatus.NUMBER_1) currentStep = 2;
    else if (status === BackupRunStatus.NUMBER_2) currentStep = 3;
    else if (isTerminalStatus(status)) currentStep = 3;
  }

  let progressStatus: BackupProgressBarStatus = "active";
  if (status === BackupRunStatus.NUMBER_3) progressStatus = "success";
  else if (isErrorStatus(status)) progressStatus = "exception";
  else if (status === BackupRunStatus.NUMBER_6) progressStatus = "normal";
  else if (!isInProgressStatus(status)) progressStatus = "normal";

  const remainingFromApi = options?.estimatedRemainingSecondsFromApi;
  const estimatedRemainingMs =
    remainingFromApi != null && remainingFromApi >= 0 && isInProgressStatus(status)
      ? Math.round(remainingFromApi * 1000)
      : estimateRemainingMs({
          status,
          requestedAt: run.requestedAt,
          startedAt: run.startedAt,
          averageSucceededDurationSeconds: options?.averageSucceededDurationSeconds,
          nowMs: options?.nowMs,
        });

  return {
    percentage,
    progressStatus,
    currentStep,
    totalSteps,
    currentStepTitleKey,
    isInProgress: isInProgressStatus(status),
    isError: isErrorStatus(status),
    isTerminal: isTerminalStatus(status),
    estimatedRemainingMs,
    statusTitleKey: statusTitleKey(status),
    bodyKey: statusBodyKey(status),
  };
}
