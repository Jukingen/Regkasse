import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  getRoleMenuPreviewSession,
  startRoleMenuPreview,
  stopRoleMenuPreview,
  subscribeRoleMenuPreview,
} from '@/features/users/utils/roleMenuPreviewSession';

describe('roleMenuPreviewSession', () => {
  beforeEach(() => {
    stopRoleMenuPreview();
    window.sessionStorage.clear();
  });

  it('starts, persists, and stops preview session', () => {
    const listener = vi.fn();
    const unsubscribe = subscribeRoleMenuPreview(listener);

    startRoleMenuPreview('Manager', ['users.view', 'settings.view']);
    expect(listener).toHaveBeenCalled();
    expect(getRoleMenuPreviewSession()).toMatchObject({
      roleName: 'Manager',
      permissions: ['users.view', 'settings.view'],
    });
    expect(window.sessionStorage.getItem('fa_role_menu_preview_v1')).toContain('Manager');

    stopRoleMenuPreview();
    expect(getRoleMenuPreviewSession()).toBeNull();
    expect(window.sessionStorage.getItem('fa_role_menu_preview_v1')).toBeNull();
    unsubscribe();
  });

  it('hydrates from sessionStorage when memory is empty', () => {
    window.sessionStorage.setItem(
      'fa_role_menu_preview_v1',
      JSON.stringify({
        roleName: 'Cashier',
        permissions: ['pos.pay'],
        startedAt: '2026-08-01T00:00:00Z',
      })
    );
    // Force re-read by clearing in-memory via stop then reading storage again:
    // stop clears storage; write raw again and import path uses get after stop.
    // After stop, session is null and storage cleared — rewrite storage then call get.
    stopRoleMenuPreview();
    window.sessionStorage.setItem(
      'fa_role_menu_preview_v1',
      JSON.stringify({
        roleName: 'Cashier',
        permissions: ['pos.pay'],
        startedAt: '2026-08-01T00:00:00Z',
      })
    );
    expect(getRoleMenuPreviewSession()?.roleName).toBe('Cashier');
  });

  it('ignores invalid storage payloads', () => {
    stopRoleMenuPreview();
    window.sessionStorage.setItem('fa_role_menu_preview_v1', '{bad');
    expect(getRoleMenuPreviewSession()).toBeNull();
    window.sessionStorage.setItem('fa_role_menu_preview_v1', JSON.stringify({ roleName: '' }));
    expect(getRoleMenuPreviewSession()).toBeNull();
  });
});
