/**
 * @vitest-environment jsdom
 */
import React from "react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import "@testing-library/jest-dom/vitest";
import { App } from "antd";
import { BackupProgress } from "@/features/backup/components/BackupProgress";

const useBackupProgressMock = vi.fn();

vi.mock("@/features/backup/hooks/useBackupProgress", () => ({
  useBackupProgress: (...args: unknown[]) => useBackupProgressMock(...args),
}));

vi.mock("@/i18n", () => ({
  useI18n: () => ({
    t: (key: string, opts?: Record<string, string | number>) => {
      if (opts) return `${key}:${JSON.stringify(opts)}`;
      return key;
    },
    formatLocale: "de-AT",
  }),
}));

describe("BackupProgress", () => {
  beforeEach(() => {
    useBackupProgressMock.mockReset();
  });

  it("hides when idle and hideWhenIdle is true", () => {
    useBackupProgressMock.mockReturnValue({
      data: null,
      isLoading: false,
      isError: false,
    });
    const { container } = render(
      <App>
        <BackupProgress hideWhenIdle />
      </App>,
    );
    expect(container.querySelector(".ant-card")).toBeNull();
  });

  it("renders progress bar for an in-progress run", () => {
    useBackupProgressMock.mockReturnValue({
      data: {
        percentage: 45,
        progressStatus: "active",
        currentStep: 2,
        totalSteps: 3,
        currentStepTitleKey: "backupDr.pipelineSteps.workerRunning.title",
        isInProgress: true,
        isError: false,
        isTerminal: false,
        estimatedRemainingMs: 30_000,
        statusTitleKey: "backupDr.progress.titleRunning",
        bodyKey: "backupDr.progress.bodyRunning",
      },
      isLoading: false,
      isError: false,
    });

    render(
      <App>
        <BackupProgress backupRunId="run-1" autoTrackLatestInProgress={false} />
      </App>,
    );

    expect(screen.getByText("backupDr.progress.cardTitle")).toBeInTheDocument();
    expect(screen.getByText("backupDr.progress.titleRunning")).toBeInTheDocument();
    expect(screen.getByText(/backupDr.progress.stepOf/)).toBeInTheDocument();
    expect(screen.getByRole("progressbar")).toBeInTheDocument();
  });

  it("shows error alert when progress is failed", () => {
    useBackupProgressMock.mockReturnValue({
      data: {
        percentage: 100,
        progressStatus: "exception",
        currentStep: 3,
        totalSteps: 3,
        currentStepTitleKey: null,
        isInProgress: false,
        isError: true,
        isTerminal: true,
        estimatedRemainingMs: undefined,
        statusTitleKey: "backupDr.progress.finishedFailed",
        bodyKey: null,
      },
      isLoading: false,
      isError: false,
    });

    render(
      <App>
        <BackupProgress
          backupRunId="run-fail"
          autoTrackLatestInProgress={false}
          hideWhenIdle={false}
        />
      </App>,
    );

    expect(screen.getByText("backupDr.progress.errorAlert")).toBeInTheDocument();
  });
});
