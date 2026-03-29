/**
 * Kurtarılabilirlik özeti hata yüzeyi — son çalıştırma kartı ile karıştırılmaz.
 */

import React from 'react';
import '@testing-library/jest-dom';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { RecoverabilitySummaryCard } from '@/features/backup-dr/components/RecoverabilitySummaryCard';

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

describe('RecoverabilitySummaryCard — query error', () => {
  it('shows recoverability error and retry without requiring unsupported DTO fields', () => {
    const onRetry = vi.fn();
    render(
      <RecoverabilitySummaryCard
        summary={undefined}
        loading={false}
        queryError
        onRetry={onRetry}
        formatDt={() => '—'}
        formatLocale="en-US"
        backupStatusLabel={() => 'x'}
        restoreStatusLabel={() => 'y'}
        t={(k) => k}
      />,
    );

    expect(screen.getByText('backupDr.errors.recoverabilityLoadFailed')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'backupDr.actions.refresh' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
