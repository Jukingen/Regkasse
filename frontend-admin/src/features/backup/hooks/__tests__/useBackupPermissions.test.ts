import { renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { useBackupPermissions } from '@/features/backup/hooks/useBackupPermissions';
import { PERMISSIONS } from '@/shared/auth/permissions';

const mockUseAuth = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

describe('useBackupPermissions', () => {
  beforeEach(() => {
    mockUseAuth.mockReset();
  });

  it('denies all ops when user has no permissions', () => {
    mockUseAuth.mockReturnValue({ user: { role: 'Cashier', permissions: [] } });
    const { result } = renderHook(() => useBackupPermissions());
    expect(result.current.canView).toBe(false);
    expect(result.current.canManageBackup).toBe(false);
    expect(result.current.canTrigger).toBe(false);
    expect(result.current.canRestore).toBe(false);
    expect(result.current.isReadOnly).toBe(false);
  });

  it('Manager with settings.view + backup.manage can trigger but not restore', () => {
    mockUseAuth.mockReturnValue({
      user: {
        role: 'Manager',
        permissions: [PERMISSIONS.SETTINGS_VIEW, PERMISSIONS.BACKUP_MANAGE],
      },
    });
    const { result } = renderHook(() => useBackupPermissions());
    expect(result.current.canView).toBe(true);
    expect(result.current.canManageBackup).toBe(true);
    expect(result.current.canTrigger).toBe(true);
    expect(result.current.canDownloadBackup).toBe(true);
    expect(result.current.canConfigure).toBe(false);
    expect(result.current.isPlatformAdmin).toBe(false);
    expect(result.current.canRestore).toBe(false);
    expect(result.current.isReadOnly).toBe(false);
    expect(result.current.canEditExecutionMode).toBe(false);
  });

  it('settings.view only is read-only', () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'Manager', permissions: [PERMISSIONS.SETTINGS_VIEW] },
    });
    const { result } = renderHook(() => useBackupPermissions());
    expect(result.current.isReadOnly).toBe(true);
    expect(result.current.canManageBackup).toBe(false);
  });

  it('SuperAdmin with settings.manage can configure and restore', () => {
    mockUseAuth.mockReturnValue({
      user: {
        role: 'SuperAdmin',
        permissions: [PERMISSIONS.SETTINGS_VIEW, PERMISSIONS.SETTINGS_MANAGE],
      },
    });
    const { result } = renderHook(() => useBackupPermissions());
    expect(result.current.isSuperAdmin).toBe(true);
    expect(result.current.canConfigure).toBe(true);
    expect(result.current.canManageBackup).toBe(true);
    expect(result.current.canRestore).toBe(true);
    expect(result.current.canFilterRunsByTenant).toBe(true);
    expect(result.current.canEditExecutionMode).toBe(true);
  });
});
