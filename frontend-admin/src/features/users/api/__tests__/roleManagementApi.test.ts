/**
 * Role management API – updateRolePermissions and deleteRole URL/body and error propagation.
 */
import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  getPermissionsCatalog,
  getRolesWithPermissions,
  updateRolePermissions,
  deleteRole,
} from '../roleManagementApi';

const mockCustomInstance = vi.fn();

vi.mock('@/lib/axios', () => ({
  customInstance: (config: { url: string; method: string; data?: unknown }) => mockCustomInstance(config),
}));

describe('roleManagementApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('updateRolePermissions', () => {
    it('calls PUT with encoded roleName and permissions body', async () => {
      mockCustomInstance.mockResolvedValue(undefined);
      await updateRolePermissions('Custom Role', ['sale.view', 'report.view']);
      expect(mockCustomInstance).toHaveBeenCalledWith({
        url: '/api/UserManagement/roles/Custom%20Role/permissions',
        method: 'PUT',
        data: { permissions: ['sale.view', 'report.view'] },
      });
    });

    it('rejects when customInstance rejects (save permissions fail)', async () => {
      mockCustomInstance.mockRejectedValue(new Error('400 Bad Request'));
      await expect(updateRolePermissions('X', [])).rejects.toThrow('400 Bad Request');
    });
  });

  describe('deleteRole', () => {
    it('calls DELETE with encoded roleName', async () => {
      mockCustomInstance.mockResolvedValue(undefined);
      await deleteRole('Custom/Role');
      expect(mockCustomInstance).toHaveBeenCalledWith({
        url: '/api/UserManagement/roles/Custom%2FRole',
        method: 'DELETE',
      });
    });

    it('rejects when customInstance rejects (delete role fail)', async () => {
      mockCustomInstance.mockRejectedValue(new Error('409 Conflict'));
      await expect(deleteRole('R')).rejects.toThrow('409 Conflict');
    });
  });

  describe('getRolesWithPermissions', () => {
    it('returns array from API', async () => {
      const data = [{ roleName: 'Manager', permissions: [], isSystemRole: true, userCount: 1 }];
      mockCustomInstance.mockResolvedValue(data);
      const result = await getRolesWithPermissions();
      expect(result).toEqual(data);
    });

    it('returns empty array when API returns non-array', async () => {
      mockCustomInstance.mockResolvedValue(null);
      const result = await getRolesWithPermissions();
      expect(result).toEqual([]);
    });
  });

  describe('getPermissionsCatalog', () => {
    it('returns array from API', async () => {
      const data = [{ key: 'user.view', group: 'User', resource: 'user', action: 'view' }];
      mockCustomInstance.mockResolvedValue(data);
      const result = await getPermissionsCatalog();
      expect(result).toEqual(data);
    });
  });
});
