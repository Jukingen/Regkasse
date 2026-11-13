/**
 * Manuel yedek tetikleme: admin API mutation imzası ve onay akışı.
 */
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

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
      />
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
      />
    );

    const btn = screen.getByRole('button', { name: /backupDr\.actions\.enqueueBackup/ });
    expect(btn).toBeDisabled();
    fireEvent.click(btn);
    expect(mutate).not.toHaveBeenCalled();
  });
});

describe('ManualActionsPanel — mode-aware confirmations', () => {
  it('shows action banner and mode-specific backup copy when modeAwareConfirmations provided', async () => {
    const mutate = vi.fn();
    render(
      <ManualActionsPanel
        canManage
        backupTrigger={{ isPending: false, mutate }}
        restoreTrigger={{ isPending: false, mutate: vi.fn() }}
        modeAwareConfirmations={{
          actionBannerLine: 'MODE_BANNER',
          backupTitle: 'BACKUP_TITLE_MODE',
          backupDescriptionParts: ['BACKUP_PART_A', 'BACKUP_PART_B'],
          restoreTitle: 'RESTORE_TITLE_MODE',
          restoreDescriptionParts: ['RESTORE_PART_A'],
          cardAlert: { severity: 'warning', message: 'CARD_ALERT_MODE' },
        }}
        t={(k) => k}
      />
    );

    expect(screen.getByText('MODE_BANNER')).toBeInTheDocument();
    expect(screen.getByText('CARD_ALERT_MODE')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /backupDr\.actions\.enqueueBackup/ }));

    await waitFor(() => {
      expect(screen.getByText('BACKUP_TITLE_MODE')).toBeInTheDocument();
    });
    expect(screen.getByText('BACKUP_PART_A')).toBeInTheDocument();
    expect(screen.getByText('BACKUP_PART_B')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'backupDr.manual.confirmBackupOk' }));
    await waitFor(() => expect(mutate).toHaveBeenCalledWith({ data: {} }));
  });

  it('restore popconfirm shows mode-aware restore paragraphs', async () => {
    const mutate = vi.fn();
    render(
      <ManualActionsPanel
        canManage
        backupTrigger={{ isPending: false, mutate: vi.fn() }}
        restoreTrigger={{ isPending: false, mutate }}
        modeAwareConfirmations={{
          actionBannerLine: null,
          backupTitle: 'B',
          backupDescriptionParts: ['x'],
          restoreTitle: 'RESTORE_TITLE_X',
          restoreDescriptionParts: ['RESTORE_LATEST_FAKE', 'RESTORE_FOOT'],
          cardAlert: null,
        }}
        t={(k) => k}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: /backupDr\.actions\.enqueueRestoreDrill/ }));

    await waitFor(() => {
      expect(screen.getByText('RESTORE_TITLE_X')).toBeInTheDocument();
    });
    expect(screen.getByText('RESTORE_LATEST_FAKE')).toBeInTheDocument();
    expect(screen.getByText('RESTORE_FOOT')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'backupDr.manual.confirmRestoreOk' }));
    await waitFor(() => expect(mutate).toHaveBeenCalledWith({ data: {} }));
  });
});

describe('ManualActionsPanel — restore drill trigger', () => {
  it('confirm flow calls mutate with empty body (symmetry with backup)', async () => {
    const mutate = vi.fn();
    render(
      <ManualActionsPanel
        canManage
        backupTrigger={{ isPending: false, mutate: vi.fn() }}
        restoreTrigger={{ isPending: false, mutate }}
        t={(k) => k}
      />
    );

    fireEvent.click(screen.getByRole('button', { name: /backupDr\.actions\.enqueueRestoreDrill/ }));

    await waitFor(() => {
      expect(screen.getByText('backupDr.manual.confirmRestoreTitle')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByRole('button', { name: 'backupDr.manual.confirmRestoreOk' }));

    await waitFor(() => {
      expect(mutate).toHaveBeenCalledWith({ data: {} });
    });
    expect(mutate).toHaveBeenCalledTimes(1);
  });

  it('does not call restore mutate when manage permission is off', () => {
    const mutate = vi.fn();
    render(
      <ManualActionsPanel
        canManage={false}
        backupTrigger={{ isPending: false, mutate: vi.fn() }}
        restoreTrigger={{ isPending: false, mutate }}
        t={(k) => k}
      />
    );

    const btn = screen.getByRole('button', { name: /backupDr\.actions\.enqueueRestoreDrill/ });
    expect(btn).toBeDisabled();
    fireEvent.click(btn);
    expect(mutate).not.toHaveBeenCalled();
  });
});
