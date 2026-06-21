import React from 'react';
import '@testing-library/jest-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';

import { I18nProvider } from '@/i18n';
import { UserFormDrawer } from '@/features/users/components/UserFormDrawer';

const mockUseAdminUserTenants = vi.fn();
const mockUseTenantList = vi.fn();

vi.mock('@/features/users/hooks/useAdminUserTenants', () => ({
    useAdminUserTenants: (...args: unknown[]) => mockUseAdminUserTenants(...args),
}));

vi.mock('@/features/tenancy/hooks/useTenantList', () => ({
    useTenantList: (...args: unknown[]) => mockUseTenantList(...args),
}));

describe('UserFormDrawer', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mockUseAdminUserTenants.mockReturnValue({
            data: [
                {
                    tenantId: 'tenant-1',
                    tenantName: 'Cafe Central',
                    tenantSlug: 'cafe-central',
                    role: 'Manager',
                    isOwner: false,
                },
            ],
            isLoading: false,
            refetch: vi.fn(),
        });
        mockUseTenantList.mockReturnValue({
            tenants: [
                { id: 'tenant-1', name: 'Cafe Central', slug: 'cafe-central', status: 'active', isActive: true },
                { id: 'tenant-2', name: 'Bistro Uno', slug: 'bistro-uno', status: 'active', isActive: true },
            ],
            isLoading: false,
        });
    });

    it('submits current tenant assignments directly in edit mode', async () => {
        const onSubmit = vi.fn();

        render(
            <I18nProvider>
                <UserFormDrawer
                    open
                    mode="edit"
                    user={{
                        id: 'user-1',
                        email: 'alice@example.com',
                        firstName: 'Alice',
                        lastName: 'Example',
                        employeeNumber: 'EMP-1',
                        role: 'Manager',
                        isActive: true,
                    }}
                    roleOptions={[{ value: 'Manager', label: 'Manager' }]}
                    onClose={vi.fn()}
                    onSubmit={onSubmit}
                    canManageTenants
                />
            </I18nProvider>,
        );

        await waitFor(() => expect(screen.getByDisplayValue('alice@example.com')).toBeInTheDocument());
        await waitFor(() => expect(screen.getByText('Cafe Central')).toBeInTheDocument());

        fireEvent.click(screen.getByRole('button', { name: /Speichern/i }));

        await waitFor(() =>
            expect(onSubmit).toHaveBeenCalledWith(
                expect.objectContaining({
                    email: 'alice@example.com',
                    tenantIds: ['tenant-1'],
                }),
            ),
        );
    });
});
