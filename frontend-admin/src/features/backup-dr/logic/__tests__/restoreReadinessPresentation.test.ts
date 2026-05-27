import { describe, expect, it } from "vitest";
import {
  buildRestoreReadinessViewModel,
  rpoProgressPercent,
  thresholdStatusFromRpoHours,
  thresholdStatusFromRtoMinutes,
} from "@/features/backup-dr/logic/restoreReadinessPresentation";

describe("restoreReadinessPresentation", () => {
  it("applies RPO thresholds", () => {
    expect(thresholdStatusFromRpoHours(6)).toBe("success");
    expect(thresholdStatusFromRpoHours(18)).toBe("warning");
    expect(thresholdStatusFromRpoHours(30)).toBe("error");
    expect(rpoProgressPercent(12)).toBe(50);
  });

  it("applies RTO thresholds", () => {
    expect(thresholdStatusFromRtoMinutes(15)).toBe("success");
    expect(thresholdStatusFromRtoMinutes(45)).toBe("warning");
    expect(thresholdStatusFromRtoMinutes(90)).toBe("error");
  });

  it("builds view model from recoverability timestamps", () => {
    const now = Date.now();
    const twoHoursAgo = new Date(now - 2 * 3600_000).toISOString();
    const vm = buildRestoreReadinessViewModel({
      recoverability: {
        lastSuccessfulBackupAt: twoHoursAgo,
        lastSuccessfulRestoreProofAt: twoHoursAgo,
        lastSuccessfulArtifactVerificationAt: twoHoursAgo,
      },
      restoreLatest: { status: 2 },
      averageSucceededBackupDurationSeconds: 1200,
    });
    expect(vm.rpoHours).not.toBeNull();
    expect(vm.rpoHours!).toBeLessThan(3);
    expect(vm.rpoStatus).toBe("success");
    expect(vm.rtoMinutes).toBe(20);
    expect(vm.drillBadgeStatus).toBe("success");
  });
});
