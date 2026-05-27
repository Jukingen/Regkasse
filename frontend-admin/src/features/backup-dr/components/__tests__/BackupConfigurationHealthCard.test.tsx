import React from "react";
import "@testing-library/jest-dom";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { BackupConfigurationHealthCard } from "@/features/backup-dr/components/BackupConfigurationHealthCard";

const t = (k: string) => k;

beforeAll(() => {
  Object.defineProperty(window, "matchMedia", {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

vi.mock("next/link", () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

describe("BackupConfigurationHealthCard", () => {
  it("renders adapter, worker, archive, verification and issues", () => {
    render(
      <BackupConfigurationHealthCard
        config={{
          level: "Unhealthy",
          effectiveAdapterKind: "Fake",
          workerEnabled: false,
          issues: ["missing archive"],
        }}
        artifactPipelinePolicy={{
          externalArchiveRootConfigured: false,
          stagingOnDiskHashReverificationExpected: false,
        }}
        canManage
        t={t}
      />,
    );
    expect(screen.getByText("Fake")).toBeInTheDocument();
    expect(screen.getByText("backupDr.monitoring.configHealth.no")).toBeInTheDocument();
    expect(screen.getByText("backupDr.monitoring.configHealth.externalArchiveNotConfigured")).toBeInTheDocument();
    expect(screen.getByText("missing archive")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "backupDr.monitoring.configHealth.editSettings" })).toHaveAttribute(
      "href",
      "/settings/backup-dr#backup-dr-schedule-settings",
    );
  });

  it("shows PgDump adapter with success badge semantics via label", () => {
    render(
      <BackupConfigurationHealthCard
        config={{ level: "Healthy", effectiveAdapterKind: "PgDump", workerEnabled: true }}
        artifactPipelinePolicy={{
          externalArchiveRootConfigured: true,
          stagingOnDiskHashReverificationExpected: true,
        }}
        t={t}
      />,
    );
    expect(screen.getByText("PgDump")).toBeInTheDocument();
    expect(screen.getByText("backupDr.monitoring.configHealth.externalArchiveConfigured")).toBeInTheDocument();
  });
});
