/**
 * @vitest-environment jsdom
 */
import React from "react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import "@testing-library/jest-dom/vitest";
import { App } from "antd";
import { RestoreHistoryView } from "@/features/backup/components/RestoreHistoryView";

const useRestoreHistoryMock = vi.fn();
const useBackupPermissionsMock = vi.fn();
const getManualRestoreReportMock = vi.fn();

vi.mock("@/features/backup/hooks/useRestoreHistory", () => ({
  useRestoreHistory: (...args: unknown[]) => useRestoreHistoryMock(...args),
}));

vi.mock("@/features/backup/hooks/useBackupPermissions", () => ({
  useBackupPermissions: () => useBackupPermissionsMock(),
}));

vi.mock("@/features/backup-dr/logic/manualRestoreApi", () => ({
  getManualRestoreReport: (...args: unknown[]) => getManualRestoreReportMock(...args),
}));

vi.mock("@/i18n", () => ({
  useI18n: () => ({
    t: (key: string) => key,
    formatLocale: "de-AT",
  }),
}));

vi.mock("@/hooks/useAntdApp", () => ({
  useAntdApp: () => ({
    message: { success: vi.fn(), error: vi.fn() },
  }),
}));

describe("RestoreHistoryView", () => {
  beforeEach(() => {
    useRestoreHistoryMock.mockReset();
    useBackupPermissionsMock.mockReset();
    getManualRestoreReportMock.mockReset();
    useBackupPermissionsMock.mockReturnValue({ canRestore: true });
  });

  it("shows forbidden alert for non-Super Admin", () => {
    useBackupPermissionsMock.mockReturnValue({ canRestore: false });
    useRestoreHistoryMock.mockReturnValue({
      items: [],
      totalCount: 0,
      isLoading: false,
      isError: false,
    });

    render(
      <App>
        <RestoreHistoryView />
      </App>,
    );

    expect(screen.getByText("backupDr.restoreHistory.forbiddenTitle")).toBeInTheDocument();
  });

  it("renders history rows and opens report modal", async () => {
    useRestoreHistoryMock.mockReturnValue({
      items: [
        {
          requestId: "req-1",
          status: "Completed",
          requestedAt: "2026-07-01T10:00:00Z",
          approvedAt: "2026-07-01T11:00:00Z",
          backupRunId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
          targetDatabaseName: "restore_validation_demo",
          validationOnly: true,
          requestedByEmail: "sa@test.com",
        },
      ],
      totalCount: 1,
      isLoading: false,
      isError: false,
    });
    getManualRestoreReportMock.mockResolvedValue({
      restoreId: "req-1",
      backupId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
      status: "Completed",
      complianceChecked: true,
      rksvCompliant: true,
      validationOnly: true,
      targetDatabaseName: "restore_validation_demo",
      tablesRestored: 42,
      recordsRestored: null,
      complianceFindings: ["linked_drill_succeeded"],
    });

    render(
      <App>
        <RestoreHistoryView />
      </App>,
    );

    expect(screen.getByText("restore_validation_demo")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /backupDr\.restoreHistory\.actions\.report/ }));

    await waitFor(() => {
      expect(getManualRestoreReportMock).toHaveBeenCalledWith("req-1");
      expect(screen.getByText("backupDr.restoreHistory.report.title")).toBeInTheDocument();
      expect(screen.getByText("42")).toBeInTheDocument();
    });
  });
});
