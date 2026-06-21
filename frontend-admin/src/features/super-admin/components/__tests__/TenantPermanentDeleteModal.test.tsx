/**
 * TenantPermanentDeleteModal — hard-delete submit, 400 blockers, 204 success.
 */
import React from 'react';
import { describe, it, expect, vi, beforeEach, beforeAll } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import axios from 'axios';
import { I18nProvider } from '@/i18n';
import {
    TenantPermanentDeleteModal,
    TENANT_PERMANENT_DELETE_CONFIRM_PHRASE,
} from '@/features/super-admin/components/TenantPermanentDeleteModal';

const mockDeletePermanent = vi.fn();
const mockGetDeleteDependencies = vi.fn();
const mockMessageSuccess = vi.fn();
const mockMessageError = vi.fn();

vi.mock('@/api/generated/admin/admin', async (importOriginal) => {
    const actual = await importOriginal<typeof import('@/api/generated/admin/admin')>();
    return {
        ...actual,
        deleteApiAdminTenantsTenantIdPermanent: (...args: unknown[]) => mockDeletePermanent(...args),
        getApiAdminTenantsTenantIdDeleteDependencies: (...args: unknown[]) =>
            mockGetDeleteDependencies(...args),
    };
});

vi.mock('@/hooks/useAntdApp', () => ({
    useAntdApp: () => ({
        message: {
            success: mockMessageSuccess,
            error: mockMessageError,
            warning: vi.fn(),
            info: vi.fn(),
        },
    }),
}));

const tenantId = '22222222-2222-2222-2222-222222222222';
const tenantSlug = 'closed-shop';

function renderModal(overrides: Partial<React.ComponentProps<typeof TenantPermanentDeleteModal>> = {}) {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });
    const onClose = vi.fn();
    const onSuccess = vi.fn();

    render(
        <QueryClientProvider client={queryClient}>
            <I18nProvider>
                <TenantPermanentDeleteModal
                    open
                    tenantId={tenantId}
                    tenantName="Closed Shop"
                    tenantSlug={tenantSlug}
                    onClose={onClose}
                    onSuccess={onSuccess}
                    {...overrides}
                />
            </I18nProvider>
        </QueryClientProvider>,
    );

    return { onClose, onSuccess };
}

async function fillConfirmation(modal: HTMLElement) {
    await waitFor(() => expect(within(modal).getByText('Bestätigung')).toBeInTheDocument());
    const inputs = within(modal).getAllByRole('textbox');
    fireEvent.change(inputs[0], { target: { value: tenantSlug } });
    fireEvent.change(inputs[1], { target: { value: TENANT_PERMANENT_DELETE_CONFIRM_PHRASE } });
    fireEvent.click(within(modal).getByRole('checkbox'));
}

beforeAll(() => {
    class ResizeObserverMock {
        observe() {}
        unobserve() {}
        disconnect() {}
    }
    vi.stubGlobal('ResizeObserver', ResizeObserverMock);

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

describe('TenantPermanentDeleteModal', () => {
    beforeEach(() => {
        vi.clearAllMocks();
        mockDeletePermanent.mockResolvedValue(undefined);
        mockGetDeleteDependencies.mockResolvedValue({
            tenantId,
            tenantSlug,
            canHardDelete: true,
            hasFiscalFootprint: false,
            dependencies: { cashRegisters: 0 },
        });
    });

    it('Flow B: 204 success closes modal, toasts, and calls onSuccess', async () => {
        const { onClose, onSuccess } = renderModal();

        const modal = await waitFor(() => document.querySelector('.ant-modal') as HTMLElement);
        await waitFor(() => expect(mockGetDeleteDependencies).toHaveBeenCalled());
        await fillConfirmation(modal);

        const submit = within(modal).getAllByRole('button', { name: /Endgültig löschen/i }).at(-1)!;
        await waitFor(() => expect(submit).not.toBeDisabled());

        fireEvent.click(submit);

        await waitFor(() =>
            expect(mockDeletePermanent).toHaveBeenCalledWith(tenantId, {
                confirmSlug: tenantSlug,
            }),
        );
        await waitFor(() => expect(mockMessageSuccess).toHaveBeenCalled());
        await waitFor(() => expect(onClose).toHaveBeenCalled());
        await waitFor(() => expect(onSuccess).toHaveBeenCalled());
        expect(mockMessageError).not.toHaveBeenCalled();
    });

    it('Flow B: 400 with dependencies keeps modal open and shows updated blockers', async () => {
        const axiosError = new axios.AxiosError(
            'Request failed',
            'ERR_BAD_REQUEST',
            undefined,
            undefined,
            {
                status: 400,
                statusText: 'Bad Request',
                headers: {},
                config: {} as never,
                data: {
                    code: 'cash_registers_present',
                    message: 'Cannot permanently delete tenant with cash registers.',
                    dependencies: {
                        tenantId,
                        tenantSlug,
                        canHardDelete: false,
                        failureCode: 'cash_registers_present',
                        hasFiscalFootprint: false,
                        dependencies: { cashRegisters: 2 },
                        blockingDependencies: [
                            {
                                code: 'cash_registers_present',
                                severity: 'blocking',
                                count: 2,
                            },
                        ],
                    },
                },
            },
        );
        mockDeletePermanent.mockRejectedValue(axiosError);

        renderModal();

        const modal = await waitFor(() => document.querySelector('.ant-modal') as HTMLElement);
        await waitFor(() => expect(mockGetDeleteDependencies).toHaveBeenCalled());
        await fillConfirmation(modal);

        const submit = within(modal).getAllByRole('button', { name: /Endgültig löschen/i }).at(-1)!;
        await waitFor(() => expect(submit).not.toBeDisabled());

        fireEvent.click(submit);

        await waitFor(() => expect(mockDeletePermanent).toHaveBeenCalled());
        await waitFor(() =>
            expect(within(modal).getAllByText(/Es sind noch Kassen vorhanden/i).length).toBeGreaterThan(0),
        );
        expect(document.querySelector('.ant-modal')).toBeTruthy();
        expect(mockMessageError).not.toHaveBeenCalled();
    });
});
