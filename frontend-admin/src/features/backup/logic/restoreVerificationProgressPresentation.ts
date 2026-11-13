import type {
  RestoreVerificationCheckDto,
  RestoreVerificationCheckResult,
  RestoreVerificationRunDto,
} from '@/features/backup/logic/restoreVerificationApi';
import {
  RESTORE_VERIFICATION_CHECK_ORDER,
  restoreVerificationStatusValue,
} from '@/features/backup/logic/restoreVerificationPresentation';

export type RestoreProgressBarStatus = 'active' | 'success' | 'exception' | 'normal';

export type RestoreProgressStepState = 'wait' | 'process' | 'finish' | 'error';

export type RestoreProgressStepView = {
  id: (typeof RESTORE_VERIFICATION_CHECK_ORDER)[number];
  state: RestoreProgressStepState;
  result?: RestoreVerificationCheckResult;
};

export type RestoreProgressViewModel = {
  runId: string;
  isInProgress: boolean;
  isError: boolean;
  percentage: number;
  progressStatus: RestoreProgressBarStatus;
  currentStep: number;
  totalSteps: number;
  currentStepId: string;
  statusTitleKey: string;
  estimatedRemainingMs: number | null;
  steps: RestoreProgressStepView[];
};

/** Minimum RestoreDrillReachedStage value that means this UI check has been reached. */
const STAGE_MIN: Record<(typeof RESTORE_VERIFICATION_CHECK_ORDER)[number], number> = {
  hash: 20,
  schema: 30,
  data: 40,
  tse: 60,
  fiscal: 50,
};

function stageNumber(stage: number | string | null | undefined): number {
  if (typeof stage === 'number' && Number.isFinite(stage)) return stage;
  if (typeof stage === 'string') {
    const n = Number(stage);
    return Number.isFinite(n) ? n : 0;
  }
  return 0;
}

function resultFromCheck(
  checks: RestoreVerificationCheckDto[] | null | undefined,
  id: string
): RestoreVerificationCheckResult | undefined {
  return checks?.find((c) => c.id === id)?.result;
}

function isReached(
  id: (typeof RESTORE_VERIFICATION_CHECK_ORDER)[number],
  stage: number,
  result: RestoreVerificationCheckResult | undefined
): boolean {
  return stage >= STAGE_MIN[id] || result === 'passed' || result === 'skipped';
}

export function buildRestoreVerificationProgressViewModel(
  run: RestoreVerificationRunDto | null | undefined,
  options?: { typicalDurationMs?: number | null }
): RestoreProgressViewModel | null {
  if (!run?.id) return null;

  const status = restoreVerificationStatusValue(run.status);
  const inProgress = status === 0 || status === 1;
  const failed = status === 3;
  const succeeded = status === 2;
  const stage = stageNumber(run.restoreDrillReachedStage);

  const reachedFlags = RESTORE_VERIFICATION_CHECK_ORDER.map((id) => {
    const result = resultFromCheck(run.checks, id);
    return { id, result, reached: isReached(id, stage, result) };
  });

  const currentOpenIndex = reachedFlags.findIndex((s) => !s.reached && s.result !== 'failed');

  const steps: RestoreProgressStepView[] = reachedFlags.map((step, index) => {
    if (step.result === 'failed') {
      return { id: step.id, result: step.result, state: 'error' };
    }
    if (step.reached) {
      return { id: step.id, result: step.result, state: 'finish' };
    }
    if (inProgress && index === currentOpenIndex) {
      return { id: step.id, result: step.result, state: 'process' };
    }
    return { id: step.id, result: step.result, state: 'wait' };
  });

  const finishedCount = steps.filter((s) => s.state === 'finish').length;
  const processIndex = steps.findIndex((s) => s.state === 'process');
  const currentStep = processIndex >= 0 ? processIndex + 1 : Math.min(finishedCount + 1, steps.length);
  const percentage = succeeded
    ? 100
    : failed
      ? Math.max(8, Math.round((finishedCount / steps.length) * 100))
      : Math.min(92, Math.round((finishedCount / steps.length) * 100) + (inProgress ? 8 : 0));

  let estimatedRemainingMs: number | null = null;
  const typical = options?.typicalDurationMs;
  if (inProgress && typical && typical > 0) {
    estimatedRemainingMs = Math.round(typical * Math.max(0, 1 - finishedCount / steps.length));
  }

  return {
    runId: run.id,
    isInProgress: inProgress,
    isError: failed,
    percentage,
    progressStatus: failed ? 'exception' : succeeded ? 'success' : inProgress ? 'active' : 'normal',
    currentStep,
    totalSteps: steps.length,
    currentStepId: steps[Math.max(0, currentStep - 1)]?.id ?? 'hash',
    statusTitleKey: failed
      ? 'backupDr.restoreProgress.titleFailed'
      : succeeded
        ? 'backupDr.restoreProgress.titleSucceeded'
        : status === 0
          ? 'backupDr.restoreProgress.titleQueued'
          : 'backupDr.restoreProgress.titleRunning',
    estimatedRemainingMs,
    steps,
  };
}
