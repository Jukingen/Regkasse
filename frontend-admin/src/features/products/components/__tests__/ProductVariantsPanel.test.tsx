import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import ProductVariantsPanel from '@/features/products/components/ProductVariantsPanel';
import { I18nProvider } from '@/i18n';

const { customInstance, messageOpen, modalConfirm } = vi.hoisted(() => ({
  customInstance: vi.fn(),
  messageOpen: vi.fn(),
  modalConfirm: vi.fn(),
}));

vi.mock('@/lib/axios', () => ({
  customInstance,
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: {
      open: messageOpen,
      success: vi.fn(),
      error: vi.fn(),
      info: vi.fn(),
      warning: vi.fn(),
    },
    notification: {
      success: vi.fn(),
      error: vi.fn(),
      info: vi.fn(),
      warning: vi.fn(),
    },
    modal: { confirm: modalConfirm },
  }),
}));

vi.mock('@/shared/dev/technicalConsole', () => ({
  technicalConsole: { error: vi.fn(), warn: vi.fn(), info: vi.fn() },
}));

const existing = {
  id: 'var-1',
  productId: 'prod-1',
  sku: 'SKU-1',
  variantLabel: 'Rot',
  priceDelta: 0,
  stock: 2,
  isActive: true,
};

function renderPanel() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <ProductVariantsPanel productId="prod-1" />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('ProductVariantsPanel', () => {
  beforeEach(() => {
    customInstance.mockReset();
    messageOpen.mockReset();
    modalConfirm.mockReset();
    customInstance.mockImplementation((config: { method?: string }) => {
      if (config.method === 'GET') return Promise.resolve([existing]);
      return Promise.resolve(existing);
    });
  });

  it('creates, edits, and deactivates through the variant endpoints', async () => {
    modalConfirm.mockImplementation((options: { onOk?: () => Promise<unknown> }) => {
      void options.onOk?.();
    });
    renderPanel();

    expect(await screen.findByText('SKU-1')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Neue Variante' }));
    fireEvent.change(screen.getByLabelText('SKU'), { target: { value: 'SKU-2' } });
    fireEvent.change(screen.getByLabelText('Bezeichnung'), { target: { value: 'Blau' } });
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }));

    await waitFor(() => {
      expect(customInstance).toHaveBeenCalledWith(
        expect.objectContaining({
          url: '/api/admin/products/prod-1/variants',
          method: 'POST',
          data: expect.objectContaining({ sku: 'SKU-2', variantLabel: 'Blau' }),
        })
      );
    });

    fireEvent.click(screen.getByRole('button', { name: 'Bearbeiten' }));
    fireEvent.change(screen.getByLabelText('Bezeichnung'), { target: { value: 'Rot / L' } });
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }));

    await waitFor(() => {
      expect(customInstance).toHaveBeenCalledWith(
        expect.objectContaining({
          url: '/api/admin/products/prod-1/variants/var-1',
          method: 'PUT',
          data: expect.objectContaining({ sku: 'SKU-1', variantLabel: 'Rot / L' }),
        })
      );
    });

    fireEvent.click(screen.getByRole('button', { name: 'Deaktivieren' }));

    await waitFor(() => {
      expect(customInstance).toHaveBeenCalledWith(
        expect.objectContaining({
          url: '/api/admin/products/prod-1/variants/var-1',
          method: 'DELETE',
        })
      );
    });
  });

  it('shows the translated toast when the SKU is already used', async () => {
    customInstance.mockImplementation((config: { method?: string }) => {
      if (config.method === 'GET') return Promise.resolve([]);
      return Promise.reject({
        response: { status: 409, data: { code: 'SKU_DUPLICATE', message: 'raw' } },
      });
    });
    renderPanel();

    fireEvent.click(await screen.findByRole('button', { name: 'Neue Variante' }));
    fireEvent.change(screen.getByLabelText('SKU'), { target: { value: 'SKU-2' } });
    fireEvent.change(screen.getByLabelText('Bezeichnung'), { target: { value: 'Blau' } });
    fireEvent.click(screen.getByRole('button', { name: 'Speichern' }));

    await waitFor(() => {
      expect(messageOpen).toHaveBeenCalled();
    });
    const content = messageOpen.mock.calls.at(-1)?.[0]?.content as React.ReactElement;
    render(<I18nProvider>{content}</I18nProvider>);
    expect(screen.getByText('Diese SKU ist in diesem Mandanten bereits vergeben.')).toBeTruthy();
  });
});
