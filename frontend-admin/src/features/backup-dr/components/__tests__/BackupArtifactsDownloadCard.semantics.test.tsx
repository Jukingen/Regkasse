/**
 * Operatör görünür anlamlar: Fake/stub başlıkları üretim “indirilebilir pg_dump” dili kullanmaz;
 * API başarılı çalıştırma başlığı Fake modunda seçilmez; şüpheli gerçek satırlar stub sanılmaz.
 */
import React from 'react';
import { describe, it, expect, beforeAll, vi } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen, within } from '@testing-library/react';

vi.mock('@/lib/axios', () => ({
  AXIOS_INSTANCE: { get: vi.fn(), interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } } },
  customInstance: vi.fn(),
}));

import { BackupArtifactsDownloadCard } from '@/features/backup-dr/components/BackupArtifactsDownloadCard';
import type { BackupArtifactResponseDto } from '@/api/generated/model';
import { BackupArtifactResponseDtoArtifactType } from '@/api/generated/model/backupArtifactResponseDtoArtifactType';

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

/** Identity translator — asserts exact i18n keys wired to visible surfaces. */
const t = (k: string, o?: Record<string, string | number>) =>
  o ? `${k} ${JSON.stringify(o)}` : k;

const baseArtifact = (over: Partial<BackupArtifactResponseDto>): BackupArtifactResponseDto => ({
  id: 'art-1',
  artifactType: BackupArtifactResponseDtoArtifactType.NUMBER_0,
  isFilePresentForDownload: true,
  byteSize: 48,
  ...over,
});

describe('BackupArtifactsDownloadCard — Fake / stub honesty', () => {
  it('uses Fake titles and scope keys — not API-success / recoverability-summary download titles', () => {
    render(
      <BackupArtifactsDownloadCard
        variant="latest_success"
        runId="run-fake"
        artifacts={[baseArtifact({ byteSize: 48 })]}
        canManage
        isSimulatedExecution
        runAdapterKind="Fake"
        realPostgreSqlLogicalDumpConfigured={false}
        simulatedOperationalMode
        t={t}
      />,
    );

    expect(screen.getByText('backupDr.download.titleLatestSuccessFake')).toBeInTheDocument();
    expect(screen.getByText('backupDr.download.scopeLatestSuccessFake')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.titleLatestSuccess')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.titleLastKnownGood')).not.toBeInTheDocument();
  });

  it('shows stub byte column + stub payload note, not non-Fake suspicion bulk alert', () => {
    render(
      <BackupArtifactsDownloadCard
        variant="latest_success"
        runId="run-fake"
        artifacts={[baseArtifact({ byteSize: 48 })]}
        canManage
        isSimulatedExecution
        runAdapterKind="Fake"
        simulatedOperationalMode
        t={t}
      />,
    );

    expect(screen.getByText('backupDr.download.byteSizeColumnSimulated')).toBeInTheDocument();
    expect(screen.getByText('backupDr.download.stubPayloadNote')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.suspicionBulkIntro')).not.toBeInTheDocument();
  });

  it('does not regress to “downloadable logical dump” wording: Fake mode avoids production API-success title', () => {
    render(
      <BackupArtifactsDownloadCard
        variant="last_known_good"
        runId="run-fake"
        artifacts={[baseArtifact({})]}
        canManage
        isSimulatedExecution
        runAdapterKind="Fake"
        simulatedOperationalMode
        t={t}
      />,
    );
    expect(screen.getByText('backupDr.download.titleLastKnownGoodFake')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.titleLastKnownGood')).not.toBeInTheDocument();
  });

  it('Fake artifact row shows stub/simulated reality tag, stub content summary, and expected-tiny byte footnote — not operational dump labeling', () => {
    render(
      <BackupArtifactsDownloadCard
        variant="latest_success"
        runId="run-fake"
        artifacts={[baseArtifact({ byteSize: 48 })]}
        canManage
        isSimulatedExecution
        runAdapterKind="Fake"
        realPostgreSqlLogicalDumpConfigured={false}
        simulatedOperationalMode
        t={t}
      />,
    );

    expect(screen.getByText('backupDr.download.reality.stub')).toBeInTheDocument();
    expect(screen.getByText('backupDr.download.contentExpectSummary.stubLogicalDumpFakeAdapter')).toBeInTheDocument();
    expect(screen.getByText('backupDr.download.byteSizeFootnote.stubExpectedTiny')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.types.logicalDumpOperational')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.reality.realPipeline')).not.toBeInTheDocument();
  });
});

describe('BackupArtifactsDownloadCard — non-Fake suspicion vs stub', () => {
  it('flags very small logical dump with suspicion tag — not stub recoverability line', () => {
    render(
      <BackupArtifactsDownloadCard
        variant="latest_success"
        runId="run-real"
        artifacts={[baseArtifact({ byteSize: 500 })]}
        canManage
        isSimulatedExecution={false}
        runAdapterKind="PgDump"
        realPostgreSqlLogicalDumpConfigured
        simulatedOperationalMode={false}
        t={t}
      />,
    );

    expect(screen.getByText('backupDr.download.suspicionBulkIntro')).toBeInTheDocument();
    expect(screen.getByText('backupDr.download.suspicion.tiny_reported_logical_dump.short')).toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.recoverabilityUse.short.not_dr_evidence_simulated')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.stubPayloadNote')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.reality.stub')).not.toBeInTheDocument();
    expect(screen.queryByText('backupDr.download.contentExpectSummary.stubLogicalDumpFakeAdapter')).not.toBeInTheDocument();
  });

  it('uses Request download (not stub button) for non-simulated eligible row', () => {
    render(
      <BackupArtifactsDownloadCard
        variant="latest_success"
        runId="run-real"
        artifacts={[baseArtifact({ byteSize: 500 })]}
        canManage
        isSimulatedExecution={false}
        runAdapterKind="PgDump"
        realPostgreSqlLogicalDumpConfigured
        simulatedOperationalMode={false}
        t={t}
      />,
    );

    const table = screen.getByRole('table');
    expect(within(table).getAllByText('backupDr.download.button').length).toBeGreaterThanOrEqual(1);
    expect(within(table).queryByText('backupDr.download.buttonStub')).not.toBeInTheDocument();
  });
});
