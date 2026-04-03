/**
 * Backup Confidence Dashboard: üst şerit ciddiyeti, stub etiketleri, "not proven" yüzeyleri — yeşil yanıltma yok.
 */

import React from 'react';
import '@testing-library/jest-dom';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import { render, within } from '@testing-library/react';
import type { BackupRecoverabilitySummaryResponseDto, RestoreVerificationRunResponseDto } from '@/api/generated/model';
import {
  BackupArtifactResponseDtoArtifactType,
  BackupRunResponseDtoStatus,
  RestoreVerificationRunResponseDtoStatus,
} from '@/api/generated/model';
import type { BackupVerificationResponseDto } from '@/api/generated/model';
import { BackupConfidenceDashboard } from '@/features/backup-dr/components/BackupConfidenceDashboard';
import { buildDrProofPresentationModel } from '@/features/backup-dr/logic/drProofLevelPresentation';
import type { BackupOperatorTruthModel } from '@/features/backup-dr/logic/backupDrOperatorTruthModel';

const t = (key: string) => key;

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

function mkTruth(p: { simulated?: boolean; realPg?: boolean; hasProofGaps?: boolean; latestDrillFailed?: boolean }): BackupOperatorTruthModel {
  return {
    run: {
      simulatedEvidence: p.simulated ?? false,
      realPostgreSqlLogicalDumpConfigured: p.realPg ?? true,
    },
    recoverability: { hasProofGaps: p.hasProofGaps ?? false },
    restore: { latestDrillFailed: p.latestDrillFailed ?? false },
  } as unknown as BackupOperatorTruthModel;
}

describe('BackupConfidenceDashboard — label / UI honesty', () => {
  it('failed restore drill: error alert, not success', () => {
    const model = buildDrProofPresentationModel({
      truth: mkTruth({ latestDrillFailed: true }),
      latest: undefined,
      detailForPipeline: undefined,
      verification: undefined,
      recoverability: {},
      restoreLatest: { status: RestoreVerificationRunResponseDtoStatus.NUMBER_3 } as RestoreVerificationRunResponseDto,
      restoreExtended: {},
    });
    const { container } = render(
      <BackupConfidenceDashboard
        model={model}
        t={t}
        formatDt={(x) => String(x ?? '—')}
        formatLocale="de-DE"
        recoverability={undefined}
        restoreLatest={{ status: RestoreVerificationRunResponseDtoStatus.NUMBER_3 } as RestoreVerificationRunResponseDto}
      />,
    );
    expect(container.querySelector('.ant-alert-error')).toBeTruthy();
    expect(container.querySelector('.ant-alert-success')).toBeNull();
    expect(within(container).getByText('backupDr.confidenceDashboard.strip.drillFailedTitle')).toBeInTheDocument();
  });

  it('stub mode: warning strip + stub layer detail keys visible', () => {
    const latest = {
      id: 's',
      status: BackupRunResponseDtoStatus.NUMBER_3,
      artifacts: [{ artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0, isFilePresentForDownload: true }],
    };
    const model = buildDrProofPresentationModel({
      truth: mkTruth({ simulated: true, realPg: true }),
      latest,
      detailForPipeline: latest,
      verification: { status: 1, backupRunId: 's' } as BackupVerificationResponseDto,
      recoverability: { lastSuccessfulBackupAt: '2026-01-01T00:00:00Z' } as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: { status: RestoreVerificationRunResponseDtoStatus.NUMBER_2 } as RestoreVerificationRunResponseDto,
      restoreExtended: {},
    });
    const { container } = render(
      <BackupConfidenceDashboard
        model={model}
        t={t}
        formatDt={(x) => String(x ?? '—')}
        formatLocale="de-DE"
        recoverability={{ lastSuccessfulBackupAt: '2026-01-01T00:00:00Z' } as BackupRecoverabilitySummaryResponseDto}
        restoreLatest={undefined}
      />,
    );
    expect(container.querySelector('.ant-alert-warning')).toBeTruthy();
    expect(within(container).getByText('backupDr.confidenceDashboard.strip.stubTitle')).toBeInTheDocument();
    expect(within(container).getByText('backupDr.confidenceDashboard.layers.L1.detailStub')).toBeInTheDocument();
  });

  it('app recovery and external: notProven keys always rendered for current API', () => {
    const model = buildDrProofPresentationModel({
      truth: mkTruth({}),
      latest: undefined,
      detailForPipeline: undefined,
      verification: undefined,
      recoverability: undefined,
      restoreLatest: undefined,
      restoreExtended: {},
    });
    const { container } = render(
      <BackupConfidenceDashboard
        model={model}
        t={t}
        formatDt={(x) => String(x ?? '—')}
        formatLocale="de-DE"
        recoverability={undefined}
        restoreLatest={undefined}
      />,
    );
    expect(within(container).getByText('backupDr.confidenceDashboard.appRecovery.notProven')).toBeInTheDocument();
    expect(within(container).getByText('backupDr.confidenceDashboard.external.notProven')).toBeInTheDocument();
  });

  it('does not surface generic healthy string in strip keys', () => {
    const latest = {
      id: 'r',
      status: BackupRunResponseDtoStatus.NUMBER_3,
      artifacts: [{ artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0, isFilePresentForDownload: true }],
    };
    const restore: RestoreVerificationRunResponseDto = {
      status: RestoreVerificationRunResponseDtoStatus.NUMBER_2,
      dumpInspectionPassed: true,
      restoreAttemptExecuted: true,
      restoreAttemptPassed: true,
      fiscalSqlSkipped: false,
      fiscalSqlPassed: true,
    };
    const model = buildDrProofPresentationModel({
      truth: mkTruth({ simulated: false, realPg: true, hasProofGaps: false }),
      latest,
      detailForPipeline: latest,
      verification: { status: 1, backupRunId: 'r' } as BackupVerificationResponseDto,
      recoverability: {} as BackupRecoverabilitySummaryResponseDto,
      restoreLatest: restore,
      restoreExtended: { postRestoreContinuityChecksExecuted: true, postRestoreContinuityChecksPassed: true },
    });
    const { container } = render(
      <BackupConfidenceDashboard
        model={model}
        t={t}
        formatDt={(x) => String(x ?? '—')}
        formatLocale="de-DE"
        recoverability={{} as BackupRecoverabilitySummaryResponseDto}
        restoreLatest={restore}
      />,
    );
    expect(within(container).getByText('backupDr.confidenceDashboard.strip.strongWithinApiTitle')).toBeInTheDocument();
    expect(container.textContent?.toLowerCase().includes('healthy')).toBe(false);
  });
});
