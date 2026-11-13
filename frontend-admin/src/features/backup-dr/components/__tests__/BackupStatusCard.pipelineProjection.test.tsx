/**
 * DR panosundaki pipeline sözleşmesi: geçerli sunucu projection varken uyarı yok; fallback görünür etiketlidir.
 */
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import type { BackupPipelineSnapshotDto, BackupRunResponseDto } from '@/api/generated/model';
import { BackupLatestRunCardPresentation } from '@/features/backup-dr/components/BackupStatusCard';
import { SERVER_PIPELINE_PROJECTION_VERSION } from '@/features/backup-dr/logic/backupPipelineDerived';

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
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

const RUN_ID = 'dddddddd-dddd-dddd-dddd-dddddddddddd';

const eightSteps: NonNullable<BackupPipelineSnapshotDto['steps']> = [
  { key: 'queued', status: 'success' },
  { key: 'workerRunning', status: 'success' },
  { key: 'dumpComplete', status: 'success' },
  { key: 'artifactCreated', status: 'success' },
  { key: 'artifactVerification', status: 'success' },
  { key: 'manifestCreated', status: 'success' },
  { key: 'externalCopy', status: 'not_required' },
  { key: 'externalChecksum', status: 'not_required' },
];

function latestBase(): BackupRunResponseDto {
  return {
    id: RUN_ID,
    status: 3,
    requestedAt: '2026-01-01T00:00:00Z',
    startedAt: '2026-01-01T00:00:01Z',
    completedAt: '2026-01-01T00:05:00Z',
    adapterKind: 'Fake',
  };
}

describe('BackupLatestRunCardPresentation — dashboard pipeline projection preference', () => {
  it('does not show client-fallback notice when server snapshot is valid', () => {
    render(
      <BackupLatestRunCardPresentation
        latest={latestBase()}
        detail={{
          ...latestBase(),
          pipeline: { projectionVersion: SERVER_PIPELINE_PROJECTION_VERSION, steps: eightSteps },
        }}
        policy={{}}
        loadingDetail={false}
        detailError={false}
        formatDt={(iso) => iso ?? '—'}
        formatLocale="en-US"
        backupStatusTagColor={() => 'success'}
        backupStatusLabel={() => 'ok'}
        allowClientPipelineFallback
        t={(k) => k}
      />
    );
    expect(
      screen.queryByText('backupDr.pipelineSteps.sourceNotice.clientDerived')
    ).not.toBeInTheDocument();
    expect(
      screen.queryByText('backupDr.pipelineSteps.sourceNotice.fallbackDisabled')
    ).not.toBeInTheDocument();
    expect(
      screen.getByText('backupDr.pipelineSteps.sourceBadge.serverProjection')
    ).toBeInTheDocument();
    expect(
      screen.queryByText('backupDr.pipelineSteps.sourceBadge.clientDerived')
    ).not.toBeInTheDocument();
  });

  it('shows explicit notice when using client-derived steps', () => {
    render(
      <BackupLatestRunCardPresentation
        latest={latestBase()}
        detail={{ ...latestBase() }}
        policy={{}}
        loadingDetail={false}
        detailError={false}
        formatDt={(iso) => iso ?? '—'}
        formatLocale="en-US"
        backupStatusTagColor={() => 'success'}
        backupStatusLabel={() => 'ok'}
        allowClientPipelineFallback
        t={(k) => k}
      />
    );
    expect(
      screen.getByText('backupDr.pipelineSteps.sourceNotice.clientDerived')
    ).toBeInTheDocument();
    expect(
      screen.getByText('backupDr.pipelineSteps.sourceBadge.clientDerived')
    ).toBeInTheDocument();
  });

  it('shows disabled notice and no stepper when client fallback is off and snapshot missing', () => {
    render(
      <BackupLatestRunCardPresentation
        latest={latestBase()}
        detail={{ ...latestBase() }}
        policy={{}}
        loadingDetail={false}
        detailError={false}
        formatDt={(iso) => iso ?? '—'}
        formatLocale="en-US"
        backupStatusTagColor={() => 'success'}
        backupStatusLabel={() => 'ok'}
        allowClientPipelineFallback={false}
        t={(k) => k}
      />
    );
    expect(
      screen.getByText('backupDr.pipelineSteps.sourceNotice.fallbackDisabled')
    ).toBeInTheDocument();
    expect(screen.getByText('backupDr.pipelineSteps.sourceBadge.noProjection')).toBeInTheDocument();
  });

  it('shows version-blocked notice when snapshot is incompatible and client fallback is off', () => {
    render(
      <BackupLatestRunCardPresentation
        latest={latestBase()}
        detail={{
          ...latestBase(),
          pipeline: { projectionVersion: '2099-01-01', steps: eightSteps },
        }}
        policy={{}}
        loadingDetail={false}
        detailError={false}
        formatDt={(iso) => iso ?? '—'}
        formatLocale="en-US"
        backupStatusTagColor={() => 'success'}
        backupStatusLabel={() => 'ok'}
        allowClientPipelineFallback={false}
        t={(k) => k}
      />
    );
    expect(
      screen.getByText('backupDr.pipelineSteps.sourceNotice.projectionVersionBlocked')
    ).toBeInTheDocument();
    expect(screen.getByText('backupDr.pipelineSteps.sourceBadge.noProjection')).toBeInTheDocument();
  });
});
