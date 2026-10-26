/**
 * @vitest-environment jsdom
 */
import '@testing-library/jest-dom/vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { RestoreDrillVisualization } from '@/features/backup/components/RestoreDrillVisualization';
import type { DrillTimelineItem } from '@/features/backup/logic/backupDrInsights';

const downloadRestoreDrillPdf = vi.hoisted(() => vi.fn(async () => new Blob(['%PDF-1.4'])));
const notifySuccess = vi.hoisted(() => vi.fn());

vi.mock('@/i18n', () => ({
  useI18n: () => ({ t: (key: string) => key, formatLocale: 'de-AT' }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ success: notifySuccess, error: vi.fn() }),
}));

vi.mock('@/features/backup/logic/backupDrillApi', () => ({
  downloadRestoreDrillPdf: (...args: unknown[]) => downloadRestoreDrillPdf(...args),
}));

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
  Object.defineProperty(URL, 'createObjectURL', { writable: true, value: vi.fn(() => 'blob:drill') });
  Object.defineProperty(URL, 'revokeObjectURL', { writable: true, value: vi.fn() });
});

const base = {
  at: '2026-10-21T06:02:00Z',
  durationMs: 1000,
  reason: '',
};

function item(id: string, result: DrillTimelineItem['result'], reportable: boolean): DrillTimelineItem {
  return { ...base, id, result, reportable };
}

describe('backup drills PDF', () => {
  it('shows the download button for passed and failed drills and hides it for cancelled', async () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    render(
      <App>
        <RestoreDrillVisualization
          items={[
            item('passed', 'Succeeded', true),
            item('failed', 'Failed', true),
            item('cancelled', 'Failed', false),
          ]}
          successRate={50}
          lastSuccessAt={null}
          daysSince={null}
          nextDrillAt={null}
          overdue={false}
          cadenceDays={7}
        />
      </App>
    );

    expect(screen.getByTestId('backup-drill-pdf-passed')).toHaveTextContent('backupDr.drills.downloadPdf');
    expect(screen.getByTestId('backup-drill-pdf-failed')).toBeInTheDocument();
    expect(screen.queryByTestId('backup-drill-pdf-cancelled')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('backup-drill-pdf-passed'));
    await waitFor(() => expect(downloadRestoreDrillPdf).toHaveBeenCalledWith('passed'));
    expect(click).toHaveBeenCalled();
    expect(notifySuccess).toHaveBeenCalledWith('backupDr.drills.downloadPdfSuccess');
  });
});
