/**
 * updateUserRole – PUT body includes preservePreviousPermissions flag.
 */
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { updateUserRole } from '../users';

const mockCustomInstance = vi.fn();

vi.mock('@/lib/axios', () => ({
    customInstance: (config: { url: string; method: string; data?: unknown }) => mockCustomInstance(config),
}));

describe('updateUserRole', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('calls PUT with role and preservePreviousPermissions true', async () => {
        const result = {
            userId: 'u1',
            userName: 'jane',
            email: 'jane@test.com',
            name: 'Jane Doe',
            role: 'CustomRole',
            isOwner: false,
            joinedAtUtc: '2026-01-01T00:00:00Z',
        };
        mockCustomInstance.mockResolvedValue(result);

        const response = await updateUserRole('tenant-1', 'u1', {
            role: 'CustomRole',
            preservePreviousPermissions: true,
        });

        expect(mockCustomInstance).toHaveBeenCalledWith({
            url: '/api/admin/tenants/tenant-1/users/u1/role',
            method: 'PUT',
            data: {
                role: 'CustomRole',
                preservePreviousPermissions: true,
            },
        });
        expect(response).toEqual(result);
    });

    it('coerces preservePreviousPermissions to false when omitted', async () => {
        mockCustomInstance.mockResolvedValue({ userId: 'u1', role: 'Cashier' });

        await updateUserRole('tenant-1', 'u1', { role: 'Cashier' });

        expect(mockCustomInstance).toHaveBeenCalledWith({
            url: '/api/admin/tenants/tenant-1/users/u1/role',
            method: 'PUT',
            data: {
                role: 'Cashier',
                preservePreviousPermissions: false,
            },
        });
    });

    it('propagates API errors', async () => {
        mockCustomInstance.mockRejectedValue(new Error('404 Not Found'));
        await expect(
            updateUserRole('tenant-1', 'u1', { role: 'Manager', preservePreviousPermissions: true }),
        ).rejects.toThrow('404 Not Found');
    });
});
