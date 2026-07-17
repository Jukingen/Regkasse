/**
 * @vitest-environment jsdom
 */
import React from "react";
import { describe, expect, it, vi, beforeEach } from "vitest";
import "@testing-library/jest-dom/vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { App } from "antd";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RestoreModal } from "@/features/backup/components/RestoreModal";

const postManualRestoreRequest = vi.fn();
let compliancePasses = true;

vi.mock("@/features/backup-dr/logic/manualRestoreApi", () => ({
  postManualRestoreRequest: (...args: unknown[]) => postManualRestoreRequest(...args),
}));

vi.mock("@/features/backup/components/RestorePreview", () => ({
  RestorePreview: ({
    onComplianceChange,
  }: {
    onComplianceChange?: (ok: boolean) => void;
  }) => {
    React.useEffect(() => {
      onComplianceChange?.(compliancePasses);
    }, [onComplianceChange]);
    return null;
  },
}));

vi.mock("@/i18n", () => ({
  useI18n: () => ({
    t: (key: string) => key,
    formatLocale: "en-US",
  }),
}));

vi.mock("@/hooks/useAntdApp", () => ({
  useAntdApp: () => ({
    message: { success: vi.fn(), error: vi.fn() },
  }),
}));

function renderModal(open = true) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <App>
        <RestoreModal
          open={open}
          onClose={vi.fn()}
          backup={{
            backupRunId: "run-1",
            fileName: "dump.sql",
            tenantSlug: "acme",
          }}
        />
      </App>
    </QueryClientProvider>,
  );
}

describe("RestoreModal", () => {
  beforeEach(() => {
    postManualRestoreRequest.mockReset();
    compliancePasses = true;
  });

  it("keeps restore disabled until both RKSV acknowledgements are checked", () => {
    renderModal();
    const restoreBtn = screen.getByRole("button", {
      name: "backupDr.manualRestore.actions.restore",
    });
    expect(restoreBtn).toBeDisabled();

    fireEvent.click(
      screen.getByLabelText("backupDr.manualRestore.acknowledgements.sameTenant"),
    );
    expect(restoreBtn).toBeDisabled();

    fireEvent.click(
      screen.getByLabelText("backupDr.manualRestore.acknowledgements.rksvUnderstood"),
    );
    expect(restoreBtn).not.toBeDisabled();
  });

  it("keeps restore disabled when live compliance check fails", () => {
    compliancePasses = false;
    renderModal();

    fireEvent.click(
      screen.getByLabelText("backupDr.manualRestore.acknowledgements.sameTenant"),
    );
    fireEvent.click(
      screen.getByLabelText("backupDr.manualRestore.acknowledgements.rksvUnderstood"),
    );

    expect(
      screen.getByRole("button", {
        name: "backupDr.manualRestore.actions.restore",
      }),
    ).toBeDisabled();
  });

  it("shows RKSV compliance notice", () => {
    renderModal();
    expect(screen.getByText("backupDr.manualRestore.rksvAlert.title")).toBeInTheDocument();
    expect(screen.getByText("backupDr.manualRestore.rksvAlert.sameTenant")).toBeInTheDocument();
    expect(
      screen.getByText("backupDr.manualRestore.rksvAlert.originalTimestamps"),
    ).toBeInTheDocument();
  });
});
