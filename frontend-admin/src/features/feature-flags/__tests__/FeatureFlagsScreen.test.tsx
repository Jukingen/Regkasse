/**
 * @vitest-environment jsdom
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { getRequiredPermissionForPath } from '@/shared/auth/routePermissions';

import { FeatureFlagsScreen } from '../FeatureFlagsScreen';

const tenantId = '22222222-2222-4222-8222-222222222222';
const setFeatureFlag = vi.hoisted(() => vi.fn(async (body: unknown) => body));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    successKey: vi.fn(),
    apiError: vi.fn(),
  }),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({
    isSuperAdmin: true,
    hasPermission: () => true,
  }),
}));

vi.mock('@/api/manual/featureFlags', () => ({
  fetchFeatureFlags: vi.fn(async () => [
    {
      name: 'EnableDepExportV2',
      enabled: false,
      configDefault: false,
      source: 'config',
      tenantOverrides: [{ tenantId, enabled: true }],
    },
    {
      name: 'EInvoicing.Peppol',
      enabled: false,
      configDefault: false,
      source: 'reserved',
      tenantOverrides: [],
    },
  ]),
  setFeatureFlag,
}));

vi.mock('@/features/super-admin/api/adminTenants', () => ({
  listAdminTenants: vi.fn(async () => [
    {
      id: tenantId,
      name: 'Wien Cafe',
      slug: 'wien',
      status: 'active',
      isActive: true,
      createdAt: '2026-01-01T00:00:00Z',
    },
  ]),
}));

function renderScreen() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => (
    <I18nProvider>
      <QueryClientProvider client={client}>
        <App>{children}</App>
      </QueryClientProvider>
    </I18nProvider>
  );
  return render(<FeatureFlagsScreen />, { wrapper });
}

describe('FeatureFlagsScreen', () => {
  beforeEach(() => {
    setFeatureFlag.mockClear();
  });

  it('is limited to system.critical', () => {
    expect(getRequiredPermissionForPath('/admin/feature-flags')).toBe(PERMISSIONS.SYSTEM_CRITICAL);
  });

  it('lists every returned flag, its description, and tenant overrides', async () => {
    renderScreen();

    expect(await screen.findByText('EnableDepExportV2')).toBeInTheDocument();
    expect(screen.getByText(/DEP-Export setzt den Header/)).toBeInTheDocument();
    expect(screen.getByText('EInvoicing.Peppol')).toBeInTheDocument();
    expect(screen.getByText(/Reserviert und aus/)).toBeInTheDocument();
    expect(screen.getByText(/Wien Cafe: An/)).toBeInTheDocument();
    expect(screen.getByRole('switch', { name: 'EInvoicing.Peppol global' })).toBeDisabled();
  });

  it('toggles the global value without a tenant id', async () => {
    renderScreen();
    const globalSwitch = await screen.findByRole('switch', { name: 'EnableDepExportV2 global' });
    fireEvent.click(globalSwitch);

    await waitFor(() => {
      expect(setFeatureFlag.mock.calls[0]?.[0]).toEqual({
        name: 'EnableDepExportV2',
        enabled: true,
        tenantId: null,
      });
    });
  });
});
