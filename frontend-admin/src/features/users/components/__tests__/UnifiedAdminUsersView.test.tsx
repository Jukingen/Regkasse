/**
 * Unified admin user list action visibility for tenant rows.
 */
import React from 'react';
import '@testing-library/jest-dom';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { I18nProvider } from '@/i18n';
import { UnifiedAdminUsersView } from '@/features/users/components/UnifiedAdminUsersView';

const mockListTenantUsers = vi.fn();
const mockGenerateTemporaryPassword = vi.fn();

vi.mock('next/navigation', () => ({
    useRouter: () => ({
        replace: vi.fn(),
        push: vi.fn(),
        refresh: vi.fn(),
        back: vi.fn(),
        forward: vi.fn(),
        prefetch: vi.fn(),
    }),
    usePathname: () => '/admin/users',
    useSearchParams: () => new URLSearchParams('tenantId=tenant-1'),
}));

vi.mock('@/hooks/useDebounce', () => ({
    useDebounce: <T,>(value: T) => value,
}));

vi.mock('@/features/users/components/CreateUserModal', () => ({
    CreateUserModal: () => null,
}));

vi.mock('@/features/super-admin/components/QuickUserModal', () => ({
    QuickUserModal: () => null,
}));

vi.mock('@/features/super-admin/components/QuickUserSuccessModal', () => ({
    QuickUserSuccessModal: () => null,
}));

vi.mock('@/features/super-admin/components/ResetPasswordModal', () => ({
    ResetPasswordModal: () => null,
}));

vi.mock('@/features/users/components/TenantFilter', () => ({
    TenantFilter: () => <div data-testid="tenant-filter" />,
}));

vi.mock('@/features/users/components/UserRoleBadge', () => ({
    UserRoleBadge: ({ role }: { role: string }) => <span>{role}</span>,
}));

vi.mock('@/features/users/hooks/useCreateUser', () => ({
    useCreateUser: () => ({
        mutateAsync: vi.fn(),
        isPending: false,
    }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
    useAntdApp: () => ({
        message: {
            success: vi.fn(),
            error: vi.fn(),
            info: vi.fn(),
            warning: vi.fn(),
        },
        notification: {
            success: vi.fn(),
            error: vi.fn(),
        },
        modal: {
            confirm: vi.fn(),
        },
    }),
}));

vi.mock('@/features/users/api/usersGateway', () => ({
    normalizeError: (error: unknown, fallbackMessage: string) => ({
        message: fallbackMessage,
        details: error,
    }),
    useGenerateTemporaryPasswordMutation: () => ({
        mutateAsync: (...args: unknown[]) => mockGenerateTemporaryPassword(...args),
        isPending: false,
    }),
}));

vi.mock('@/features/super-admin/api/quickUser', () => ({
    createQuickUser: vi.fn(),
}));

vi.mock('@/features/tenancy/hooks/useTenantList', () => ({
    useTenantList: () => ({
        tenants: [{ id: 'tenant-1', name: 'Cafe Central', slug: 'cafe-central', isActive: true }],
        isLoading: false,
    }),
}));

vi.mock('@/features/users/api/users', () => ({
    adminUsersQueryKeys: {
        all: (isActive?: boolean, role?: string, search?: string) =>
            ['admin', 'users', 'all', isActive ?? 'all', role ?? 'all', search ?? ''] as const,
        platform: (isActive?: boolean, search?: string) =>
            ['admin', 'users', 'platform', isActive ?? 'all', search ?? ''] as const,
        tenant: (tenantId?: string, role?: string, search?: string) =>
            ['admin', 'users', 'tenant', tenantId ?? 'all', role ?? 'all', search ?? ''] as const,
    },
    listAllAdminUsers: vi.fn().mockResolvedValue([]),
    listPlatformUsers: vi.fn().mockResolvedValue([]),
    listTenantUsers: (...args: unknown[]) => mockListTenantUsers(...args),
    removeUserFromTenant: vi.fn(),
    adminUserToUserInfo: vi.fn(),
    tenantRowToTenantUser: (row: {
        userId: string;
        userName: string;
        email: string;
        name: string;
        role: string;
        isOwner: boolean;
        joinedAtUtc: string;
        tenantId: string;
        tenantSlug: string;
        tenantName: string;
        isActive: boolean;
        lastLoginAt?: string | null;
    }) => ({
        userId: row.userId,
        userName: row.userName,
        email: row.email,
        name: row.name,
        role: row.role,
        isOwner: row.isOwner,
        joinedAtUtc: row.joinedAtUtc,
        tenantId: row.tenantId,
        tenantSlug: row.tenantSlug,
        tenantName: row.tenantName,
        isActive: row.isActive,
        lastLoginAt: row.lastLoginAt ?? undefined,
    }),
}));

describe('UnifiedAdminUsersView', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('shows reactivate action for inactive tenant users', async () => {
        const onReactivate = vi.fn();
        mockListTenantUsers.mockResolvedValue([
            {
                userId: 'tenant-user-1',
                email: 'inactive@tenant.test',
                name: 'Inactive Tenant',
                role: 'Manager',
                isOwner: false,
                joinedAtUtc: '2025-01-01T00:00:00Z',
                tenantId: 'tenant-1',
                tenantSlug: 'cafe-central',
                tenantName: 'Cafe Central',
                isActive: false,
                lastLoginAt: null,
            },
        ]);

        const queryClient = new QueryClient({
            defaultOptions: { queries: { retry: false } },
        });

        render(
            <QueryClientProvider client={queryClient}>
                <I18nProvider>
                    <UnifiedAdminUsersView
                        policy={{
                            canView: true,
                            canCreate: true,
                            canEdit: true,
                            canDeactivate: true,
                            canReactivate: true,
                            canCreateRole: false,
                            canDeleteRole: false,
                            canEditRolePermissions: false,
                            canResetPassword: () => true,
                            canProvisionTenantCredentials: true,
                        }}
                        roleDisplayLabel={(role) => role}
                        currentUserId="current-user-id"
                        onView={vi.fn()}
                        onEdit={vi.fn()}
                        onDeactivate={vi.fn()}
                        onReactivate={onReactivate}
                        onResetPassword={vi.fn()}
                        onCreatePlatformUser={vi.fn()}
                    />
                </I18nProvider>
            </QueryClientProvider>,
        );

        await waitFor(() => {
            expect(screen.getByText('Inactive Tenant')).toBeInTheDocument();
        });

        fireEvent.click(screen.getByRole('button', { name: /Reaktivieren/i }));
        expect(onReactivate).toHaveBeenCalledWith(
            expect.objectContaining({
                id: 'tenant-user-1',
                isActive: false,
            }),
        );
    });

    it('shows deactivate action for active tenant users', async () => {
        const onDeactivate = vi.fn();
        mockListTenantUsers.mockResolvedValue([
            {
                userId: 'tenant-user-2',
                email: 'active@tenant.test',
                name: 'Active Tenant',
                role: 'Manager',
                isOwner: false,
                joinedAtUtc: '2025-01-01T00:00:00Z',
                tenantId: 'tenant-1',
                tenantSlug: 'cafe-central',
                tenantName: 'Cafe Central',
                isActive: true,
                lastLoginAt: null,
            },
        ]);

        const queryClient = new QueryClient({
            defaultOptions: { queries: { retry: false } },
        });

        render(
            <QueryClientProvider client={queryClient}>
                <I18nProvider>
                    <UnifiedAdminUsersView
                        policy={{
                            canView: true,
                            canCreate: true,
                            canEdit: true,
                            canDeactivate: true,
                            canReactivate: true,
                            canCreateRole: false,
                            canDeleteRole: false,
                            canEditRolePermissions: false,
                            canResetPassword: () => true,
                            canProvisionTenantCredentials: true,
                        }}
                        roleDisplayLabel={(role) => role}
                        currentUserId="current-user-id"
                        onView={vi.fn()}
                        onEdit={vi.fn()}
                        onDeactivate={onDeactivate}
                        onReactivate={vi.fn()}
                        onResetPassword={vi.fn()}
                        onCreatePlatformUser={vi.fn()}
                    />
                </I18nProvider>
            </QueryClientProvider>,
        );

        await waitFor(() => {
            expect(screen.getByText('Active Tenant')).toBeInTheDocument();
        });

        fireEvent.click(screen.getByRole('button', { name: /Konto deaktivieren/i }));
        expect(onDeactivate).toHaveBeenCalledWith(
            expect.objectContaining({
                id: 'tenant-user-2',
                isActive: true,
            }),
        );
    });

    it('opens password modal and generates a temporary password for tenant users', async () => {
        mockListTenantUsers.mockResolvedValue([
            {
                userId: 'tenant-user-3',
                email: 'password@tenant.test',
                name: 'Password Tenant',
                role: 'Manager',
                isOwner: false,
                joinedAtUtc: '2025-01-01T00:00:00Z',
                tenantId: 'tenant-1',
                tenantSlug: 'cafe-central',
                tenantName: 'Cafe Central',
                isActive: true,
                lastLoginAt: null,
            },
        ]);
        mockGenerateTemporaryPassword.mockResolvedValue({
            generatedPassword: 'Temp#Pass123',
            forcePasswordChangeOnNextLogin: true,
        });

        const queryClient = new QueryClient({
            defaultOptions: { queries: { retry: false } },
        });

        render(
            <QueryClientProvider client={queryClient}>
                <I18nProvider>
                    <UnifiedAdminUsersView
                        policy={{
                            canView: true,
                            canCreate: true,
                            canEdit: true,
                            canDeactivate: true,
                            canReactivate: true,
                            canCreateRole: false,
                            canDeleteRole: false,
                            canEditRolePermissions: false,
                            canResetPassword: () => true,
                            canProvisionTenantCredentials: true,
                        }}
                        roleDisplayLabel={(role) => role}
                        currentUserId="current-user-id"
                        onView={vi.fn()}
                        onEdit={vi.fn()}
                        onDeactivate={vi.fn()}
                        onReactivate={vi.fn()}
                        onResetPassword={vi.fn()}
                        onCreatePlatformUser={vi.fn()}
                    />
                </I18nProvider>
            </QueryClientProvider>,
        );

        await waitFor(() => {
            expect(screen.getByText('Password Tenant')).toBeInTheDocument();
        });

        expect(screen.getByText('***')).toBeInTheDocument();
        fireEvent.click(screen.getByRole('button', { name: /Temporäres Passwort erzeugen und anzeigen/i }));
        expect(await screen.findByText('Temporäres Passwort generieren')).toBeInTheDocument();
        fireEvent.click(screen.getByRole('button', { name: 'Temporäres Passwort generieren' }));

        await waitFor(() => {
            expect(mockGenerateTemporaryPassword).toHaveBeenCalledWith('tenant-user-3');
        });
        expect(await screen.findByDisplayValue('Temp#Pass123')).toBeInTheDocument();
    });

    it('scopes tenant managers to JWT mandant and hides platform filter', async () => {
        mockListTenantUsers.mockResolvedValue([
            {
                userId: 'tenant-user-4',
                userName: 'manager1',
                email: 'manager@tenant.test',
                name: 'Scoped Manager',
                role: 'Cashier',
                isOwner: false,
                joinedAtUtc: '2025-01-01T00:00:00Z',
                tenantId: 'tenant-1',
                tenantSlug: 'cafe-central',
                tenantName: 'Cafe Central',
                isActive: true,
                lastLoginAt: null,
            },
        ]);

        const queryClient = new QueryClient({
            defaultOptions: { queries: { retry: false } },
        });

        render(
            <QueryClientProvider client={queryClient}>
                <I18nProvider>
                    <UnifiedAdminUsersView
                        policy={{
                            canView: true,
                            canCreate: true,
                            canEdit: true,
                            canDeactivate: true,
                            canReactivate: true,
                            canCreateRole: false,
                            canDeleteRole: false,
                            canEditRolePermissions: false,
                            canResetPassword: () => true,
                            canProvisionTenantCredentials: false,
                        }}
                        roleDisplayLabel={(role) => role}
                        tenantScopeId="tenant-1"
                        isSuperAdminActor={false}
                        onView={vi.fn()}
                        onEdit={vi.fn()}
                        onDeactivate={vi.fn()}
                        onReactivate={vi.fn()}
                        onResetPassword={vi.fn()}
                        onCreatePlatformUser={vi.fn()}
                    />
                </I18nProvider>
            </QueryClientProvider>,
        );

        await waitFor(() => {
            expect(screen.getByText('Scoped Manager')).toBeInTheDocument();
        });

        expect(screen.queryByTestId('tenant-filter')).not.toBeInTheDocument();
        expect(screen.queryByRole('button', { name: /Plattform-Admin anlegen/i })).not.toBeInTheDocument();
        expect(screen.queryByRole('columnheader', { name: /^Passwort$/i })).not.toBeInTheDocument();
        expect(
            screen.queryByRole('button', { name: /Temporäres Passwort erzeugen und anzeigen/i }),
        ).not.toBeInTheDocument();
        expect(screen.getByRole('button', { name: /Passwort zurücksetzen/i })).toBeInTheDocument();
        expect(mockListTenantUsers).toHaveBeenCalledWith(
            expect.objectContaining({ tenantId: 'tenant-1' }),
        );
    });
});
