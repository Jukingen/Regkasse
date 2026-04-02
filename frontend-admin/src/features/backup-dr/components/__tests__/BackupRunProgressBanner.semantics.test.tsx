/**
 * Terminal başarı bandı: simüle / üretim ayrımı — success (yeşil) tipi kullanılmaz.
 */

import React from 'react';
import '@testing-library/jest-dom';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { BackupRunResponseDto } from '@/api/generated/model';
import { BackupRunProgressBanner } from '@/features/backup-dr/components/BackupRunProgressBanner';

const base = (status: number): BackupRunResponseDto =>
  ({
    id: 'x',
    status,
    requestedAt: '2026-01-01T00:00:00Z',
  }) as BackupRunResponseDto;

describe('BackupRunProgressBanner — operator-visible alert semantics', () => {
  it('non-simulated success uses info, not success', () => {
    const { container } = render(
      <BackupRunProgressBanner
        latest={base(3)}
        isSimulatedExecution={false}
        averageSucceededDurationSeconds={null}
        averageSucceededDurationSampleCount={null}
        formatDt={(iso) => String(iso)}
        formatLocale="en-US"
        t={(k) => k}
      />,
    );
    const alert = container.querySelector('.ant-alert-info');
    expect(alert).toBeTruthy();
    expect(container.querySelector('.ant-alert-success')).toBeNull();
  });

  it('simulated success uses warning, not success', () => {
    const { container } = render(
      <BackupRunProgressBanner
        latest={base(3)}
        isSimulatedExecution
        averageSucceededDurationSeconds={null}
        averageSucceededDurationSampleCount={null}
        formatDt={(iso) => String(iso)}
        formatLocale="en-US"
        t={(k) => k}
      />,
    );
    expect(container.querySelector('.ant-alert-warning')).toBeTruthy();
    expect(container.querySelector('.ant-alert-success')).toBeNull();
    expect(screen.getByText('backupDr.progress.finishedSimulatedOk')).toBeInTheDocument();
    expect(screen.getByText('backupDr.progress.finishedSimulatedOkDetail')).toBeInTheDocument();
  });

  it('omitSimulatedSuccessDetail hides simulated detail paragraph (banner/recoverability carry pg_dump context)', () => {
    const { container } = render(
      <BackupRunProgressBanner
        latest={base(3)}
        isSimulatedExecution
        omitSimulatedSuccessDetail
        averageSucceededDurationSeconds={null}
        averageSucceededDurationSampleCount={null}
        formatDt={(iso) => String(iso)}
        formatLocale="en-US"
        t={(k) => k}
      />,
    );
    expect(container.querySelector('.ant-alert-warning')).toBeTruthy();
    expect(screen.queryByText('backupDr.progress.finishedSimulatedOkDetail')).toBeNull();
  });
});
