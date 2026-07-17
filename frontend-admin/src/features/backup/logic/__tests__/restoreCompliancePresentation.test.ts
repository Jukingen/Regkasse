import { describe, expect, it } from "vitest";
import {
  complianceAlertTone,
  complianceCheckLabelKey,
  sortComplianceChecks,
} from "@/features/backup/logic/restoreCompliancePresentation";

describe("restoreCompliancePresentation", () => {
  it("maps known check names to i18n keys", () => {
    expect(complianceCheckLabelKey("SameTenant")).toContain("sameTenant");
    expect(complianceCheckLabelKey("BackupIntegrity")).toContain("backupIntegrity");
    expect(complianceCheckLabelKey("Other")).toContain("unknown");
  });

  it("picks alert tone from compliance state", () => {
    expect(complianceAlertTone({ isLoading: true, isError: false, succeeded: undefined })).toBe(
      "info",
    );
    expect(complianceAlertTone({ isLoading: false, isError: true, succeeded: undefined })).toBe(
      "error",
    );
    expect(complianceAlertTone({ isLoading: false, isError: false, succeeded: true })).toBe(
      "success",
    );
    expect(complianceAlertTone({ isLoading: false, isError: false, succeeded: false })).toBe(
      "error",
    );
  });

  it("sorts checks in display order", () => {
    const sorted = sortComplianceChecks([
      { name: "RksvValidationGate", passed: true },
      { name: "SameTenant", passed: true },
      { name: "BackupIntegrity", passed: false },
    ]);
    expect(sorted.map((c) => c.name)).toEqual([
      "SameTenant",
      "BackupIntegrity",
      "RksvValidationGate",
    ]);
  });
});
