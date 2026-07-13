/**
 * Mandant üyelik yöneticisi yalnızca anlamlı durumlarda açılmalı ve mevcut atamaları görünür tutmalı.
 */
import React from 'react';
import '@testing-library/jest-dom';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { I18nProvider } from '@/i18n';
import { TenantMembershipManager } from '@/features/users/components/TenantMembershipManager';

type MockTenant = {
    id: string;
    name: string;
    slug: string;
    status: string;
    isActive: boolean;
};

let mockTenantListResult: { tenants: MockTenant[]; isLoading: boolean } = {
    tenants: [],
    isLoading: false,
};

vi.mock('@/features/tenancy/hooks/useTenantList', () => ({
    useTenantList: () => mockTenantListResult,
}));

vi.mock('@/features/users/api/users', () => ({
    adminUsersQueryKeys: {
        userTenants: (userId: string) => ['admin', 'users', userId, 'tenants'] as const,
    },
    updateUserTenants: vi.fn(),
}));

beforeAll(() => {
    Object.defineProperty(window, 'matchMedia', {
        writable: true,
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

function renderManager(currentTenants: React.ComponentProps<typeof TenantMembershipManager>['currentTenants']) {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });

    return render(
        <QueryClientProvider client={queryClient}>
            <I18nProvider>
                <TenantMembershipManager
                    userId="user-1"
                    currentTenants={currentTenants}
                    onSuccess={vi.fn()}
                />
            </I18nProvider>
        </QueryClientProvider>,
    );
}

describe('TenantMembershipManager', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mockTenantListResult = {
            tenants: [],
            isLoading: false,
        };
    });

    it('shows current tenant assignments and preselects active memberships', async () => {
        mockTenantListResult = {
            isLoading: false,
            tenants: [
                { id: 'tenant-1', name: 'Cafe Central', slug: 'cafe-central', status: 'active', isActive: true },
                { id: 'tenant-2', name: 'Bistro Uno', slug: 'bistro-uno', status: 'active', isActive: true },
                { id: 'tenant-3', name: 'Suspended Shop', slug: 'suspended-shop', status: 'suspended', isActive: true },
            ],
        };

        renderManager([
            {
                id: 'tenant-1',
                name: 'Cafe Central',
                slug: 'cafe-central',
                role: 'Manager',
                isOwner: true,
            },
        ]);

        fireEvent.click(screen.getByRole('button', { name: /Mandanten verwalten/i }));

        expect(await screen.findByRole('dialog')).toBeInTheDocument();
        expect(screen.getAllByText('Cafe Central').length).toBeGreaterThan(0);
        expect(screen.getAllByText('(cafe-central)').length).toBeGreaterThan(0);
        expect(screen.getAllByText('Hauptadministrator').length).toBeGreaterThan(0);
        expect(screen.queryByText('Suspended Shop')).not.toBeInTheDocument();
        expect(screen.getByRole('checkbox', { name: /Cafe Central/ })).toBeChecked();
    });

    it('does not open the modal when there are no active tenants to assign', () => {
        mockTenantListResult = {
            isLoading: false,
            tenants: [
                { id: 'tenant-3', name: 'Deleted Shop', slug: 'deleted-shop', status: 'deleted', isActive: false },
                { id: 'tenant-4', name: 'Suspended Shop', slug: 'suspended-shop', status: 'suspended', isActive: true },
            ],
        };

        renderManager([]);

        const manageButton = screen.getByRole('button', { name: /Mandanten verwalten/i });
        expect(manageButton).toBeDisabled();

        fireEvent.click(manageButton);
        expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });
});
