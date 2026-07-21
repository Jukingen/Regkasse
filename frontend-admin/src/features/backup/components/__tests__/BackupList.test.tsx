/**
 * @vitest-environment jsdom
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { BackupRunStatus } from '@/api/generated/model/backupRunStatus';
import { BackupList } from '@/features/backup/components/BackupList';

const useBackupListMock = vi.fn();
const useBackupPermissionsMock = vi.fn();

vi.mock('@/features/backup/hooks/useBackupList', () => ({
  useBackupList: () => useBackupListMock(),
}));

vi.mock('@/features/backup/hooks/useBackupPermissions', () => ({
  useBackupPermissions: () => useBackupPermissionsMock(),
}));

vi.mock('@/api/generated/admin/admin', () => ({
  getGetApiAdminBackupListQueryKey: () => ['backup-list'],
  usePostApiAdminBackupArtifactsImport: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
  }),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => key,
    formatLocale: 'de-AT',
  }),
}));

vi.mock('@/i18n/formatting', () => ({
  formatBytes: (n: number) => `${n} B`,
  formatDateTime: (iso: string) => iso,
}));

function renderList(compact = false) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <App>
        <BackupList limit={5} compact={compact} />
      </App>
    </QueryClientProvider>
  );
}

describe('BackupList', () => {
  beforeEach(() => {
    useBackupPermissionsMock.mockReturnValue({
      canDownloadBackup: false,
      canRestore: true,
    });
    useBackupListMock.mockReturnValue({
      data: [
        {
          backupRunId: '11111111-1111-1111-1111-111111111111',
          artifactId: '22222222-2222-2222-2222-222222222222',
          fileName: 'backup_dev.dump',
          fileSize: 4096,
          createdAt: '2026-07-03T15:01:00Z',
          tenantSlug: 'dev',
          isFake: true,
          status: BackupRunStatus.NUMBER_3,
          durationSeconds: 180,
          durationFormatted: '3m',
        },
      ],
      isLoading: false,
      isFetching: false,
      isError: false,
      refetch: vi.fn(),
    });
  });

  it('compact mode shows date, size, status, duration and restore action', () => {
    renderList(true);

    expect(screen.getByText('backupDr.backupList.date')).toBeTruthy();
    expect(screen.getByText('backupDr.backupList.size')).toBeTruthy();
    expect(screen.getByText('backupDr.backupList.status')).toBeTruthy();
    expect(screen.getByText('backupDr.backupList.duration')).toBeTruthy();
    expect(screen.getByText('3m')).toBeTruthy();
    expect(screen.getByText('backupDr.manualRestore.table.requestRestore')).toBeTruthy();
    expect(screen.queryByText('backup_dev.dump')).toBeNull();
  });
});
