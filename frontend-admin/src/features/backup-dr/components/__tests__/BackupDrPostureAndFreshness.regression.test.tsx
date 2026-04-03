/**
 * Üst özet + kısmi sorgu uyarısı — operatör netliği regresyonu.
 */
import React from 'react';
import '@testing-library/jest-dom';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { BackupDrPostureSummary } from '@/features/backup-dr/components/BackupDrPostureSummary';
import { BackupDrDataFreshnessStrip } from '@/features/backup-dr/components/BackupDrDataFreshnessStrip';
import type { DrProofPresentationModel } from '@/features/backup-dr/logic/drProofLevelPresentation';
import type { BackupExecutionModeTruth } from '@/features/backup-dr/logic/backupDrExecutionModeTruth';
import { unloadedBackupExecutionModeTruth } from '@/features/backup-dr/logic/backupDrExecutionModeTruth';

const t = (k: string) => k;

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
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

function baseDrProof(
  over: Partial<DrProofPresentationModel> = {},
): DrProofPresentationModel {
  return {
    highestFullyProvenLevel: 2,
    layers: [],
    nextStepKey: 'backupDr.confidenceDashboard.nextStepHints.L2',
    decisionStrip: {
      alertType: 'warning',
      titleKey: 'backupDr.confidenceDashboard.strip.gapsTitle',
      bodyKey: 'backupDr.confidenceDashboard.strip.gapsBody',
      suppressOptimisticTone: false,
    },
    latestRealBackupArtifactSummary: {
      labelKey: 'backupDr.confidenceDashboard.artifacts.lastGoodReal',
      isDistinctFromLatestRequest: false,
    },
    latestRestoreVerifiedSummary: {
      labelKey: 'backupDr.confidenceDashboard.restore.notVerified',
      drillSucceeded: false,
    },
    fiscalVerifiedSummary: { labelKey: 'backupDr.confidenceDashboard.fiscal.notProven' },
    appRecoverySummary: { labelKey: 'x', state: 'not_proven' },
    externalDepsSummary: { labelKey: 'y', state: 'not_proven' },
    ...over,
  };
}

const execLoaded: BackupExecutionModeTruth = {
  ...unloadedBackupExecutionModeTruth,
  loaded: true,
  effectiveUserFacingMode: 'Fake',
  requestedUserFacingMode: 'Fake',
  configurationDefaultUserFacingMode: 'Fake',
  effectiveExecutionAdapterKind: 'Fake',
  configurationExecutionAdapterKind: 'Fake',
  effectiveIsSimulatedAdapter: true,
  effectiveIsPgDumpAdapter: false,
  effectiveModeRunnable: true,
  requestedRealButBlocked: false,
  recommendedFallbackUserFacingMode: null,
  resolutionSummaryEnglish: '',
  requestedRealButEffectiveSimulated: false,
  requestedFakeButEffectivePgDump: false,
  fallbackBehavior: 'none',
};

describe('BackupDrPostureSummary', () => {
  it('shows simulated mode tag when simulated operational mode', () => {
    render(
      <BackupDrPostureSummary
        drProof={baseDrProof()}
        latestRun={{ id: 'a', status: 3 } as never}
        restoreLatest={undefined}
        recoverability={undefined}
        executionMode={execLoaded}
        simulatedOperationalMode
        backupStatusLabel={() => 'ok'}
        restoreStatusLabel={() => 'x'}
        formatDt={() => '—'}
        formatLocale="en-US"
        t={t}
      />,
    );
    expect(screen.getByText('backupDr.postureSummary.simulatedModeTag')).toBeInTheDocument();
  });

  it('does not show simulated tag when not simulated', () => {
    render(
      <BackupDrPostureSummary
        drProof={baseDrProof()}
        latestRun={undefined}
        restoreLatest={undefined}
        recoverability={undefined}
        executionMode={execLoaded}
        simulatedOperationalMode={false}
        backupStatusLabel={() => 'ok'}
        restoreStatusLabel={() => 'x'}
        formatDt={() => '—'}
        formatLocale="en-US"
        t={t}
      />,
    );
    expect(screen.queryByText('backupDr.postureSummary.simulatedModeTag')).not.toBeInTheDocument();
  });

  it('surfaces drill-failed strip title from dr proof model (pessimistic)', () => {
    render(
      <BackupDrPostureSummary
        drProof={baseDrProof({
          decisionStrip: {
            alertType: 'error',
            titleKey: 'backupDr.confidenceDashboard.strip.drillFailedTitle',
            bodyKey: 'backupDr.confidenceDashboard.strip.drillFailedBody',
            suppressOptimisticTone: true,
          },
        })}
        latestRun={{ id: 'a', status: 3 } as never}
        restoreLatest={undefined}
        recoverability={undefined}
        executionMode={execLoaded}
        simulatedOperationalMode={false}
        backupStatusLabel={() => 'ok'}
        restoreStatusLabel={() => 'failed'}
        formatDt={() => '—'}
        formatLocale="en-US"
        t={t}
      />,
    );
    expect(screen.getByText('backupDr.confidenceDashboard.strip.drillFailedTitle')).toBeInTheDocument();
  });

  it('renders scan tags with Ant Design preset colors (error → red)', () => {
    const { container } = render(
      <BackupDrPostureSummary
        drProof={baseDrProof({
          decisionStrip: {
            alertType: 'error',
            titleKey: 'backupDr.confidenceDashboard.strip.drillFailedTitle',
            bodyKey: 'backupDr.confidenceDashboard.strip.drillFailedBody',
            suppressOptimisticTone: true,
          },
        })}
        scanTags={[{ labelKey: 'backupDr.scan.drill.latestFailed', tone: 'error' }]}
        latestRun={{ id: 'a', status: 3 } as never}
        restoreLatest={undefined}
        recoverability={undefined}
        executionMode={execLoaded}
        simulatedOperationalMode={false}
        backupStatusLabel={() => 'ok'}
        restoreStatusLabel={() => 'x'}
        formatDt={() => '—'}
        formatLocale="en-US"
        t={t}
      />,
    );
    const el = screen.getByText('backupDr.scan.drill.latestFailed');
    expect(el.closest('.ant-tag')?.className).toMatch(/ant-tag-red/);
    expect(container.querySelectorAll('.ant-tag-red').length).toBeGreaterThanOrEqual(1);
  });
});

describe('BackupDrDataFreshnessStrip', () => {
  it('renders warning with refresh when supporting queries failed', () => {
    const onRetry = vi.fn();
    render(
      <BackupDrDataFreshnessStrip
        show
        recoverabilityFailed
        verificationFailed={false}
        restoreLatestFailed={false}
        onRetry={onRetry}
        t={t}
      />,
    );
    expect(screen.getByText('backupDr.dataFreshness.title')).toBeInTheDocument();
    expect(screen.getByText('backupDr.dataFreshness.sliceRecoverability')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'backupDr.actions.refresh' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('renders nothing when show is false', () => {
    const { container } = render(
      <BackupDrDataFreshnessStrip
        show={false}
        recoverabilityFailed={false}
        verificationFailed={false}
        restoreLatestFailed={false}
        onRetry={() => {}}
        t={t}
      />,
    );
    expect(container.firstChild).toBeNull();
  });
});
