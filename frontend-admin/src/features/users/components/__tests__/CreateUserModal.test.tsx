import React from 'react';
import '@testing-library/jest-dom';
import { beforeAll, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { I18nProvider } from '@/i18n';
import { CreateUserModal } from '@/features/users/components/CreateUserModal';

vi.mock('@/features/super-admin/components/QuickUserSuccessModal', () => ({
    QuickUserSuccessModal: () => null,
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

function renderModal(node: React.ReactNode) {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });

    return render(
        <QueryClientProvider client={queryClient}>
            <I18nProvider>{node}</I18nProvider>
        </QueryClientProvider>,
    );
}

describe('CreateUserModal', () => {
    it('assigns the selected tenant directly without opening the assignment modal', async () => {
        const onSubmit = vi.fn().mockResolvedValue({
            userId: 'user-1',
            email: 'alice@example.com',
            generatedPassword: 'Temp#Pass123',
            forcePasswordChangeOnNextLogin: true,
            success: true,
        });
        const onAssignTenants = vi.fn().mockResolvedValue(undefined);

        renderModal(
            <CreateUserModal
                open
                isSuperAdmin
                allowDeferredTenantAssignment
                tenantRows={[
                    { id: 'tenant-1', name: 'Cafe Central', slug: 'cafe-central', status: 'active', isActive: true, createdAt: '' },
                ]}
                initialValues={{ tenantId: 'tenant-1' }}
                onClose={vi.fn()}
                onSubmit={onSubmit}
                onAssignTenants={onAssignTenants}
            />,
        );

        fireEvent.change(screen.getByLabelText('E-Mail'), { target: { value: 'alice@example.com' } });
        fireEvent.click(screen.getByRole('button', { name: /Benutzer erstellen/i }));

        await waitFor(() => {
            expect(onSubmit).toHaveBeenCalledWith(
                expect.objectContaining({
                    email: 'alice@example.com',
                    role: 'Manager',
                }),
            );
        });
        expect(onSubmit.mock.calls[0]?.[0]).not.toHaveProperty('tenantId');
        await waitFor(() => expect(onAssignTenants).toHaveBeenCalledWith('user-1', ['tenant-1']));
        expect(screen.queryByText('Optional können Sie jetzt Mandanten für alice@example.com auswählen.')).not.toBeInTheDocument();
    });

    it('opens the assignment modal when no tenant was selected during creation', async () => {
        const onSubmit = vi.fn().mockResolvedValue({
            userId: 'user-2',
            email: 'bob@example.com',
            generatedPassword: 'Temp#Pass123',
            forcePasswordChangeOnNextLogin: true,
            success: true,
        });

        renderModal(
            <CreateUserModal
                open
                isSuperAdmin
                allowDeferredTenantAssignment
                tenantRows={[
                    { id: 'tenant-1', name: 'Cafe Central', slug: 'cafe-central', status: 'active', isActive: true, createdAt: '' },
                ]}
                onClose={vi.fn()}
                onSubmit={onSubmit}
                onAssignTenants={vi.fn().mockResolvedValue(undefined)}
            />,
        );

        fireEvent.change(screen.getByLabelText('E-Mail'), { target: { value: 'bob@example.com' } });
        fireEvent.click(screen.getByRole('button', { name: /Benutzer erstellen/i }));

        await waitFor(() => expect(onSubmit).toHaveBeenCalled());
        expect(await screen.findByText('Optional können Sie jetzt Mandanten für bob@example.com auswählen.')).toBeInTheDocument();
    });

    it('uses deferred assignment in quick mode when no tenant is selected', async () => {
        const onQuickSubmitWithoutTenant = vi.fn().mockResolvedValue({
            userId: 'user-quick-1',
            email: 'manager_ab12cd@platform.regkasse.at',
            generatedPassword: 'Temp#Pass123',
            forcePasswordChangeOnNextLogin: true,
            success: true,
        });

        renderModal(
            <CreateUserModal
                open
                isSuperAdmin
                allowDeferredTenantAssignment
                tenantRows={[
                    { id: 'tenant-1', name: 'Cafe Central', slug: 'cafe-central', status: 'active', isActive: true, createdAt: '' },
                ]}
                onClose={vi.fn()}
                onSubmit={vi.fn()}
                onAssignTenants={vi.fn().mockResolvedValue(undefined)}
                quickMode={{
                    onSubmit: vi.fn(),
                    onSubmitWithoutTenant: onQuickSubmitWithoutTenant,
                }}
            />,
        );

        fireEvent.click(screen.getByRole('tab', { name: /Schnell anlegen/i }));
        expect(screen.getByText('Nach der Erstellung müssen Sie den Benutzer einem Mandanten zuordnen.')).toBeInTheDocument();

        fireEvent.click(screen.getByRole('button', { name: /Generieren/i }));

        await waitFor(() =>
            expect(onQuickSubmitWithoutTenant).toHaveBeenCalledWith(
                expect.objectContaining({ role: 'Manager' }),
            ),
        );
        expect(
            await screen.findByText('Optional können Sie jetzt Mandanten für manager_ab12cd@platform.regkasse.at auswählen.'),
        ).toBeInTheDocument();
    });
});
