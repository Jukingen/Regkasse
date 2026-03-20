import { describe, expect, it } from 'vitest';
import {
  buildCoverageCardCopy,
  buildFinanzOnlineCardCopy,
  buildPayloadHashCardCopy,
  mapCoverageSummaryToHealth,
  mapFinanzOnlineMetricsToHealth,
  mapPayloadHashAnalyzeToHealth,
} from '../normalizers';

const basePayload = {
  legacyDataQualityRiskHigh: false,
  runtimeMismatchCount: 0,
  conflictGroups: [] as { skipReason: string }[],
  mismatchRatioPercent: 0,
  nullOrEmptyPayloadHash: 0,
  repairableNoConflictCount: 0,
  skippedWouldConflictCount: 0,
  sampleMismatchIds: [] as string[],
  warningMessage: null as string | null,
  repairableItems: [] as unknown[],
};

describe('mapPayloadHashAnalyzeToHealth', () => {
  it('returns unavailable for null/undefined', () => {
    expect(mapPayloadHashAnalyzeToHealth(null)).toBe('unavailable');
    expect(mapPayloadHashAnalyzeToHealth(undefined)).toBe('unavailable');
  });

  it('escalates to critical for legacy flag, runtime mismatch, or conflict groups', () => {
    expect(mapPayloadHashAnalyzeToHealth({ ...basePayload, scanned: 1, legacyDataQualityRiskHigh: true })).toBe(
      'critical'
    );
    expect(mapPayloadHashAnalyzeToHealth({ ...basePayload, scanned: 1, runtimeMismatchCount: 1 })).toBe('critical');
    expect(
      mapPayloadHashAnalyzeToHealth({
        ...basePayload,
        scanned: 1,
        conflictGroups: [{ skipReason: 'x' }],
      })
    ).toBe('critical');
  });

  it('returns warning for ratio, null hashes, repairable, or skipped-would-conflict', () => {
    expect(mapPayloadHashAnalyzeToHealth({ ...basePayload, scanned: 10, mismatchRatioPercent: 0.1 })).toBe(
      'warning'
    );
    expect(mapPayloadHashAnalyzeToHealth({ ...basePayload, scanned: 10, nullOrEmptyPayloadHash: 1 })).toBe(
      'warning'
    );
    expect(mapPayloadHashAnalyzeToHealth({ ...basePayload, scanned: 10, repairableNoConflictCount: 1 })).toBe(
      'warning'
    );
    expect(mapPayloadHashAnalyzeToHealth({ ...basePayload, scanned: 10, skippedWouldConflictCount: 1 })).toBe(
      'warning'
    );
  });

  it('healthy clean payload copy vs zero-row sample copy', () => {
    const clean = { ...basePayload, scanned: 100 };
    expect(mapPayloadHashAnalyzeToHealth(clean)).toBe('healthy');
    expect(buildPayloadHashCardCopy(clean, 'healthy').summaryLine).toContain('Stichprobe');

    const zero = { ...basePayload, scanned: 0 };
    expect(mapPayloadHashAnalyzeToHealth(zero)).toBe('healthy');
    expect(buildPayloadHashCardCopy(zero, 'healthy').summaryLine).toContain('0 Zeilen');
  });

  it('unavailable copy is not conflated with all-clear', () => {
    expect(buildPayloadHashCardCopy(null, 'unavailable').summaryLine).toContain('nicht gleichbedeutend');
  });
});

describe('mapCoverageSummaryToHealth', () => {
  it('null → unavailable; alert → critical; low percent → warning', () => {
    expect(mapCoverageSummaryToHealth(null)).toBe('unavailable');
    expect(
      mapCoverageSummaryToHealth({
        lowCoverageAlert: true,
        deviceIdCoveragePercent: 99,
        sequenceCoveragePercent: 99,
        total: 10,
      })
    ).toBe('critical');
    expect(
      mapCoverageSummaryToHealth({
        lowCoverageAlert: false,
        deviceIdCoveragePercent: 90,
        sequenceCoveragePercent: 100,
        total: 10,
      })
    ).toBe('warning');
  });

  it('embeds alertReason in detail lines when present', () => {
    const { detailLines } = buildCoverageCardCopy(
      {
        lowCoverageAlert: true,
        alertReason: 'missing device',
        deviceIdCoveragePercent: 80,
        sequenceCoveragePercent: 80,
        total: 5,
      },
      'critical'
    );
    expect(detailLines.some((l) => l.includes('missing device'))).toBe(true);
  });

  it('zero samples: healthy level but explicit no-signal copy', () => {
    const data = {
      lowCoverageAlert: false,
      deviceIdCoveragePercent: 100,
      sequenceCoveragePercent: 100,
      total: 0,
    };
    expect(mapCoverageSummaryToHealth(data)).toBe('healthy');
    expect(buildCoverageCardCopy(data, 'healthy').summaryLine).toContain('0 Samples');
  });
});

describe('mapFinanzOnlineMetricsToHealth', () => {
  it('null → unavailable; permanent failure → critical; other failures → warning; clean → healthy', () => {
    expect(mapFinanzOnlineMetricsToHealth(null)).toBe('unavailable');
    expect(mapFinanzOnlineMetricsToHealth(undefined)).toBe('unavailable');
    expect(
      mapFinanzOnlineMetricsToHealth({
        submitTotal: 10,
        submitFailedTotal: 2,
        submitFailedTransient: 1,
        submitFailedPermanent: 1,
        submitFailedUnknown: 0,
      })
    ).toBe('critical');
    expect(
      mapFinanzOnlineMetricsToHealth({
        submitTotal: 10,
        submitFailedTotal: 1,
        submitFailedTransient: 1,
        submitFailedPermanent: 0,
        submitFailedUnknown: 0,
      })
    ).toBe('warning');
    expect(
      mapFinanzOnlineMetricsToHealth({
        submitTotal: 10,
        submitFailedTotal: 0,
        submitFailedTransient: 0,
        submitFailedPermanent: 0,
        submitFailedUnknown: 0,
      })
    ).toBe('healthy');
  });

  it('unavailable copy warns against reading as all-clear', () => {
    expect(buildFinanzOnlineCardCopy(null, 'unavailable').summaryLine).toContain('nicht gleichbedeutend');
  });
});
