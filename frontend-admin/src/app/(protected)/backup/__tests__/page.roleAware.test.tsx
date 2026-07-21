/**
 * @vitest-environment jsdom
 */
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import BackupOverviewPage from '@/app/(protected)/backup/page';

const usePermissionsMock = vi.fn();

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => usePermissionsMock(),
}));

vi.mock('@/features/backup/components/BackupPageShell', () => ({
  BackupPageShell: ({ children, titleKey }: { children: React.ReactNode; titleKey: string }) => (
    <div data-testid="shell" data-title={titleKey}>
      {children}
    </div>
  ),
}));

vi.mock('@/features/backup/components/SystemBackupView', () => ({
  SystemBackupView: () => <div data-testid="system-view" />,
}));

vi.mock('@/features/backup/components/TenantBackupView', () => ({
  TenantBackupView: () => <div data-testid="tenant-view" />,
}));

describe('BackupOverviewPage role-aware views', () => {
  beforeEach(() => {
    usePermissionsMock.mockReset();
  });

  it('renders SystemBackupView for Super Admin', () => {
    usePermissionsMock.mockReturnValue({ isSuperAdmin: true });
    render(<BackupOverviewPage />);
    expect(screen.getByTestId('system-view')).toBeTruthy();
    expect(screen.queryByTestId('tenant-view')).toBeNull();
    expect(screen.getByTestId('shell').getAttribute('data-title')).toBe(
      'backupDr.overview.systemView.pageTitle'
    );
  });

  it('renders TenantBackupView for Mandanten-Admin', () => {
    usePermissionsMock.mockReturnValue({ isSuperAdmin: false });
    render(<BackupOverviewPage />);
    expect(screen.getByTestId('tenant-view')).toBeTruthy();
    expect(screen.queryByTestId('system-view')).toBeNull();
    expect(screen.getByTestId('shell').getAttribute('data-title')).toBe(
      'backupDr.overview.tenantView.pageTitle'
    );
  });
});
