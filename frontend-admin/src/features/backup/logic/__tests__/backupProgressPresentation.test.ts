import { describe, expect, it } from "vitest";
import { BackupRunStatus } from "@/api/generated/model/backupRunStatus";
import type { DerivedPipelineStep } from "@/features/backup-dr/logic/backupPipelineDerived";
import {
  buildBackupProgressViewModel,
  estimateRemainingMs,
  percentFromPipelineSteps,
  percentFromRunStatus,
} from "@/features/backup/logic/backupProgressPresentation";

describe("backupProgressPresentation", () => {
  it("maps run status to coarse percent", () => {
    expect(percentFromRunStatus(BackupRunStatus.NUMBER_0)).toBe(8);
    expect(percentFromRunStatus(BackupRunStatus.NUMBER_1)).toBe(45);
    expect(percentFromRunStatus(BackupRunStatus.NUMBER_2)).toBe(85);
    expect(percentFromRunStatus(BackupRunStatus.NUMBER_3)).toBe(100);
  });

  it("computes percent from pipeline steps", () => {
    const steps: DerivedPipelineStep[] = [
      { id: "queued", state: "success", titleKey: "backupDr.pipelineSteps.queued.title" },
      { id: "workerRunning", state: "running", titleKey: "backupDr.pipelineSteps.workerRunning.title" },
      { id: "dumpComplete", state: "pending", titleKey: "backupDr.pipelineSteps.dumpComplete.title" },
      { id: "externalCopy", state: "skipped", titleKey: "backupDr.pipelineSteps.externalCopy.title" },
    ];
    const result = percentFromPipelineSteps(steps);
    expect(result.totalSteps).toBe(3);
    expect(result.currentStep).toBe(2);
    expect(result.percentage).toBe(50);
  });

  it("estimates remaining time from average duration", () => {
    const startedAt = "2026-07-17T10:00:00.000Z";
    const nowMs = new Date("2026-07-17T10:00:30.000Z").getTime();
    expect(
      estimateRemainingMs({
        status: BackupRunStatus.NUMBER_1,
        startedAt,
        averageSucceededDurationSeconds: 120,
        nowMs,
      }),
    ).toBe(90_000);
  });

  it("builds view-model for running run without pipeline", () => {
    const vm = buildBackupProgressViewModel(
      {
        id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        status: BackupRunStatus.NUMBER_1,
        startedAt: "2026-07-17T10:00:00.000Z",
        requestedAt: "2026-07-17T09:59:00.000Z",
      },
      {
        averageSucceededDurationSeconds: 60,
        nowMs: new Date("2026-07-17T10:00:20.000Z").getTime(),
        allowClientPipelineFallback: false,
      },
    );
    expect(vm).not.toBeNull();
    expect(vm!.isInProgress).toBe(true);
    expect(vm!.percentage).toBe(45);
    expect(vm!.progressStatus).toBe("active");
    expect(vm!.estimatedRemainingMs).toBe(40_000);
    expect(vm!.statusTitleKey).toBe("backupDr.progress.titleRunning");
  });

  it("marks failed runs as exception", () => {
    const vm = buildBackupProgressViewModel(
      {
        id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
        status: BackupRunStatus.NUMBER_4,
      },
      { allowClientPipelineFallback: false },
    );
    expect(vm!.isError).toBe(true);
    expect(vm!.progressStatus).toBe("exception");
  });
});
