import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { BulkEmailForm } from '@/features/communication/components/BulkEmailForm';

const {
  mockPreviewMutate,
  mockPreviewMutateAsync,
  mockSendMutateAsync,
  mockNotifyErrorKey,
  mockNotifySuccessKey,
  mockModalConfirm,
} = vi.hoisted(() => ({
  mockPreviewMutate: vi.fn(),
  mockPreviewMutateAsync: vi.fn(),
  mockSendMutateAsync: vi.fn(),
  mockNotifyErrorKey: vi.fn(),
  mockNotifySuccessKey: vi.fn(),
  mockModalConfirm: vi.fn(),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string, options?: Record<string, string | number>) =>
      options ? `${key}:${JSON.stringify(options)}` : key,
    textLocale: 'en',
    formatLocale: 'en',
    setTextLocale: vi.fn(),
    setFormatLocale: vi.fn(),
    isLocaleReady: true,
  }),
  I18nProvider: ({ children }: { children: React.ReactNode }) => children,
}));

vi.mock('@/api/generated/admin/admin', () => ({
  usePostApiAdminCommunicationBulkEmailPreview: (options?: {
    mutation?: {
      onSuccess?: (data: unknown) => void;
      onError?: () => void;
    };
  }) => ({
    mutate: (vars: unknown) => {
      mockPreviewMutate(vars);
      const data = { recipientCount: 3, tenantCount: 2 };
      options?.mutation?.onSuccess?.(data);
      return data;
    },
    mutateAsync: async (vars: unknown) => {
      const data = await mockPreviewMutateAsync(vars);
      options?.mutation?.onSuccess?.(data);
      return data;
    },
    isPending: false,
  }),
  usePostApiAdminCommunicationBulkEmail: (options?: {
    mutation?: {
      onSuccess?: (data: unknown) => void;
      onError?: (err: unknown) => void;
    };
  }) => ({
    mutateAsync: async (vars: unknown) => {
      const data = await mockSendMutateAsync(vars);
      options?.mutation?.onSuccess?.(data);
      return data;
    },
    isPending: false,
  }),
}));

vi.mock('@/features/tenancy/api/getApiAdminTenants', () => ({
  useGetApiAdminTenants: () => ({
    data: [
      {
        id: '11111111-1111-1111-1111-111111111111',
        name: 'Cafe Central',
        slug: 'cafe-central',
        isActive: true,
        status: 'Active',
        createdAt: '2026-01-01T00:00:00Z',
      },
    ],
    isLoading: false,
  }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    errorKey: mockNotifyErrorKey,
    successKey: mockNotifySuccessKey,
    success: vi.fn(),
    error: vi.fn(),
    apiError: vi.fn(),
  }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    modal: {
      confirm: (opts: { onOk?: () => unknown }) => {
        mockModalConfirm(opts);
        return opts.onOk?.();
      },
    },
    message: {},
    notification: {},
  }),
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

function renderForm() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <BulkEmailForm />
    </QueryClientProvider>
  );
}

describe('BulkEmailForm', () => {
  beforeEach(() => {
    mockPreviewMutate.mockReset();
    mockPreviewMutateAsync.mockReset();
    mockSendMutateAsync.mockReset();
    mockNotifyErrorKey.mockReset();
    mockNotifySuccessKey.mockReset();
    mockModalConfirm.mockReset();

    mockPreviewMutateAsync.mockResolvedValue({ recipientCount: 3, tenantCount: 2 });
    mockSendMutateAsync.mockResolvedValue({
      totalAttempted: 3,
      totalSent: 3,
      totalFailed: 0,
      failedEmails: [],
    });
  });

  it('blocks send when subject/body invalid', async () => {
    renderForm();

    fireEvent.click(screen.getByTestId('bulk-email-send'));

    await waitFor(() => {
      expect(screen.getByText('communication.bulkEmail.subjectRequired')).toBeInTheDocument();
    });
    expect(mockSendMutateAsync).not.toHaveBeenCalled();
    expect(mockModalConfirm).not.toHaveBeenCalled();
  });

  it('previews recipients and sends after confirmation', async () => {
    renderForm();

    const subject = screen.getByLabelText('communication.bulkEmail.subject');
    const body = screen.getByLabelText('communication.bulkEmail.body');
    fireEvent.change(subject, { target: { value: 'Wartung' } });
    fireEvent.change(body, { target: { value: '<p>Hallo</p>' } });

    fireEvent.click(screen.getByTestId('bulk-email-preview'));

    await waitFor(() => {
      expect(mockPreviewMutate).toHaveBeenCalled();
      expect(
        screen.getByText('communication.bulkEmail.recipientCount:{"count":3}')
      ).toBeInTheDocument();
    });

    fireEvent.click(screen.getByTestId('bulk-email-send'));

    await waitFor(() => {
      expect(mockModalConfirm).toHaveBeenCalled();
      expect(mockSendMutateAsync).toHaveBeenCalledWith({
        data: expect.objectContaining({
          subject: 'Wartung',
          body: '<p>Hallo</p>',
          tenantIds: null,
        }),
      });
      expect(mockNotifySuccessKey).toHaveBeenCalledWith('communication.bulkEmail.success');
    });

    expect(await screen.findByText('communication.bulkEmail.resultTitle')).toBeInTheDocument();
  });

  it('shows rate limit warning', () => {
    renderForm();
    expect(screen.getByText('communication.bulkEmail.rateLimitWarning')).toBeInTheDocument();
  });
});
