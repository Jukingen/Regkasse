/**
 * @vitest-environment jsdom
 */
import React from "react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import "@testing-library/jest-dom/vitest";
import { App } from "antd";
import { BackupDiff } from "@/features/backup/components/BackupDiff";

const useBackupDiffMock = vi.fn();

vi.mock("@/features/backup/hooks/useBackupDiff", () => ({
  useBackupDiff: (...args: unknown[]) => useBackupDiffMock(...args),
}));

vi.mock("@/i18n", () => ({
  useI18n: () => ({
    t: (key: string) => key,
    formatLocale: "de-AT",
  }),
}));

describe("BackupDiff", () => {
  beforeEach(() => {
    useBackupDiffMock.mockReset();
  });

  it("renders comparison table when diff is ready", () => {
    useBackupDiffMock.mockReturnValue({
      data: {
        backup1Id: "a",
        backup2Id: "b",
        sizeBytes1: 1000,
        sizeBytes2: 800,
        sizeDiffBytes: 200,
        dump1Analyzed: true,
        dump2Analyzed: true,
        changedCount: 1,
        differences: [
          {
            key: "public.products",
            table: "public.products",
            count1: 1,
            count2: 0,
            diff: 1,
            onlyInBackup1: true,
            onlyInBackup2: false,
          },
        ],
      },
      sameId: false,
      isLoading: false,
      isError: false,
    });

    render(
      <App>
        <BackupDiff backup1Id="a" backup2Id="b" />
      </App>,
    );

    expect(screen.getByText("backupDr.backupDiff.cardTitle")).toBeInTheDocument();
    expect(screen.getByText("public.products")).toBeInTheDocument();
  });

  it("prompts to pick two runs when ids missing", () => {
    useBackupDiffMock.mockReturnValue({
      data: null,
      sameId: false,
      isLoading: false,
      isError: false,
    });

    render(
      <App>
        <BackupDiff />
      </App>,
    );

    expect(screen.getByText("backupDr.backupDiff.pickTwo")).toBeInTheDocument();
  });
});
