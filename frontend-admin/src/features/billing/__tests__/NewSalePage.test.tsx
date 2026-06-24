import React from 'react';
import '@testing-library/jest-dom';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';

import { I18nProvider } from '@/i18n';
import BillingNewSalePage from '@/app/(protected)/admin/billing/sales/new/page';

const { mockPreviewMutateAsync } = vi.hoisted(() => ({
    mockPreviewMutateAsync: vi.fn(),
}));

vi.mock('next/navigation', () => ({
    useRouter: vi.fn(),
    useSearchParams: vi.fn(() => new URLSearchParams()),
}));

vi.mock('@/features/billing/hooks/useBillingAccess', () => ({
    useBillingAccess: () => true,
}));

vi.mock('@/hooks/useAntdApp', () => ({
    useAntdApp: () => ({
        message: {
            success: vi.fn(),
            open: vi.fn(),
        },
        notification: {},
        modal: {},
    }),
}));

vi.mock('@/features/super-admin/api/adminTenants', () => ({
    listAdminTenants: vi.fn().mockResolvedValue([
        {
            id: 'tenant-1',
            name: 'Cafe Central',
            slug: 'cafe-central',
            status: 'active',
            isActive: true,
            createdAt: '2026-01-01T00:00:00Z',
        },
    ]),
}));

vi.mock('@/features/billing/api/billingApi', () => ({
    billingApi: {
        usePreview: vi.fn((options?: { mutation?: { onSuccess?: (data: unknown) => void } }) => ({
            mutateAsync: async (vars: { data: unknown }) => {
                const data = await mockPreviewMutateAsync(vars);
                options?.mutation?.onSuccess?.(data);
                return data;
            },
            isPending: false,
        })),
        useCreate: vi.fn(() => ({
            mutateAsync: vi.fn(),
            isPending: false,
        })),
        useTenantLicense: vi.fn(() => ({
            data: null,
            isLoading: false,
        })),
    },
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

    class ResizeObserverMock {
        observe = vi.fn();
        unobserve = vi.fn();
        disconnect = vi.fn();
    }
    vi.stubGlobal('ResizeObserver', ResizeObserverMock);
});

function renderPage() {
    const queryClient = new QueryClient({
        defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
    });

    return render(
        <QueryClientProvider client={queryClient}>
            <I18nProvider>
                <BillingNewSalePage />
            </I18nProvider>
        </QueryClientProvider>,
    );
}

describe('NewBillingSalePage', () => {
    const mockRouter = { push: vi.fn(), back: vi.fn() };

    beforeEach(() => {
        vi.clearAllMocks();
        vi.mocked(useRouter).mockReturnValue(mockRouter as ReturnType<typeof useRouter>);
        mockPreviewMutateAsync.mockResolvedValue({
            licenseKey: 'REGK-20270101-cafe-A7F3K2D9',
            invoiceNumber: 'RE20260841',
            tenantName: 'Cafe Central',
            tenantSlug: 'cafe-central',
            priceNet: 299,
            vatRate: 20,
            vatAmount: 59.8,
            priceGross: 358.8,
            durationDisplay: '1 Jahr',
            durationDays: 365,
            validFromUtc: '2026-06-24T00:00:00Z',
            validUntilUtc: '2027-06-24T00:00:00Z',
        });
    });

    it('renders the form with all fields', async () => {
        renderPage();

        expect(screen.getByRole('heading', { name: 'Neuer Lizenzverkauf' })).toBeInTheDocument();
        expect(screen.getByText('Vorschau erstellen')).toBeInTheDocument();
        expect(screen.getByText('Verkauf abschließen')).toBeInTheDocument();

        await waitFor(() => {
            expect(screen.getByLabelText('Mandant')).toBeInTheDocument();
        });
        expect(screen.getByLabelText('Lizenzplan')).toBeInTheDocument();
        expect(screen.getByLabelText('Preis (Netto)')).toBeInTheDocument();
    });

    it('shows preview after clicking preview button', async () => {
        renderPage();

        await waitFor(() => {
            expect(screen.getByRole('combobox', { name: 'Mandant' })).toBeInTheDocument();
        });

        fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Mandant' }));
        const tenantOption = await screen.findByText('Cafe Central (cafe-central)');
        fireEvent.click(tenantOption);
        fireEvent.blur(screen.getByRole('combobox', { name: 'Mandant' }));

        const priceInput = document.querySelector<HTMLInputElement>('#priceNet input, input#priceNet');
        expect(priceInput).toBeTruthy();
        fireEvent.change(priceInput!, { target: { value: '299' } });
        fireEvent.blur(priceInput!);

        fireEvent.click(screen.getByText('Vorschau erstellen'));

        await waitFor(() => {
            expect(screen.getByText('REGK-20270101-cafe-A7F3K2D9')).toBeInTheDocument();
        });
        expect(screen.getByText('RE20260841')).toBeInTheDocument();
        expect(mockPreviewMutateAsync).toHaveBeenCalledWith(
            expect.objectContaining({
                data: expect.objectContaining({
                    tenantId: 'tenant-1',
                    licensePlan: '12_months',
                    priceNet: 299,
                    vatRate: 20,
                }),
            }),
        );
    });
});
