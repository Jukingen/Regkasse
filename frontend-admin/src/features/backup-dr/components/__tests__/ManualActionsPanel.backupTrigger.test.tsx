/**
 * Manuel yedek tetikleme: admin API mutation imzası ve onay akışı.
 */

import React from 'react';
import '@testing-library/jest-dom';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ManualActionsPanel } from '@/features/backup-dr/components/ManualActionsPanel';

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

describe('ManualActionsPanel — backup trigger', () => {
  it('confirm flow calls mutate with empty body (admin trigger DTO shape)', async () => {
    const mutate = vi.fn();
    render(
      <ManualActionsPanel
        canManage
        backupTrigger={{ isPending: false, mutate }}
        restoreTrigger={{ isPending: false, mutate: vi.fn() }}
        t={(k) => k}
      />,
    );

    fireEvent.click(screen.getByRole('button', { name: /backupDr\.actions\.enqueueBackup/ }));

    await waitFor(() => {
      expect(screen.getByText('backupDr.manual.confirmBackupTitle')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: 'backupDr.manual.confirmBackupOk' }));

    await waitFor(() => {
      expect(mutate).toHaveBeenCalledWith({ data: {} });
    });
    expect(mutate).toHaveBeenCalledTimes(1);
  });

  it('does not call mutate when manage permission is off', () => {
    const mutate = vi.fn();
    render(
      <ManualActionsPanel
        canManage={false}
        backupTrigger={{ isPending: false, mutate }}
        restoreTrigger={{ isPending: false, mutate: vi.fn() }}
        t={(k) => k}
      />,
    );

    const btn = screen.getByRole('button', { name: /backupDr\.actions\.enqueueBackup/ });
    expect(btn).toBeDisabled();
    fireEvent.click(btn);
    expect(mutate).not.toHaveBeenCalled();
  });
});
