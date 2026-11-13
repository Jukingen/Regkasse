/**
 * @vitest-environment jsdom
 */
import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { DigitalServiceAccess } from '@/features/digital/components/DigitalServiceAccess';
import { PERMISSIONS } from '@/shared/auth/permissions';

const mockUsePermissions = vi.fn();
const mockUseTenantDigitalService = vi.fn();
const mockUseCurrentTenant = vi.fn();

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => mockUsePermissions(),
}));

vi.mock('@/features/digital-services/hooks/useTenantDigitalServices', () => ({
  useTenantDigitalService: (...args: unknown[]) => mockUseTenantDigitalService(...args),
}));

vi.mock('@/features/tenancy/hooks/useCurrentTenant', () => ({
  useCurrentTenant: () => mockUseCurrentTenant(),
}));

vi.mock('@/features/digital/components/DigitalServices', () => ({
  DigitalServices: () => <div>Dijital Hizmetler</div>,
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => {
      const labels: Record<string, string> = {
        'tenants.digitalServices.accessDeniedTitle': 'Access Denied',
        'tenants.digitalServices.accessDenied':
          'You do not have permission to access digital services.',
        'tenants.digitalServices.statusLoading': 'Loading…',
        'tenants.digitalServices.statusLoadFailed': 'Failed to load service status.',
        'tenants.digitalServices.servicesDisabled': 'Digital services are disabled.',
      };
      return labels[key] ?? key;
    },
    formatLocale: 'de-DE',
  }),
}));

describe('DigitalServiceAccess', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseCurrentTenant.mockReturnValue({ tenantId: 'test-id' });
    mockUseTenantDigitalService.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
      isFetched: false,
    });
  });

  it('renders DigitalServices when user has permission', () => {
    mockUsePermissions.mockReturnValue({
      hasPermission: vi.fn((permission: string) => permission === PERMISSIONS.DIGITAL_VIEW),
      user: { role: 'Manager', permissions: [PERMISSIONS.DIGITAL_VIEW] },
      isSuperAdmin: false,
    });

    // Skip status gate so the portal content can render.
    render(<DigitalServiceAccess tenantId="test-id" blockWhenDisabled={false} />);

    expect(screen.getByText('Dijital Hizmetler')).toBeInTheDocument();
    expect(mockUseTenantDigitalService).toHaveBeenCalledWith(undefined);
  });

  it('shows access denied when user has no permission', () => {
    mockUsePermissions.mockReturnValue({
      hasPermission: vi.fn().mockReturnValue(false),
      user: { role: 'Cashier', permissions: [] },
      isSuperAdmin: false,
    });

    render(<DigitalServiceAccess tenantId="test-id" />);

    expect(screen.getByText('Access Denied')).toBeInTheDocument();
    expect(
      screen.getByText('You do not have permission to access digital services.')
    ).toBeInTheDocument();
  });

  it('blocks Mandanten when digital services are disabled', () => {
    mockUsePermissions.mockReturnValue({
      hasPermission: vi.fn((permission: string) => permission === PERMISSIONS.DIGITAL_VIEW),
      user: { role: 'Manager', permissions: [PERMISSIONS.DIGITAL_VIEW] },
      isSuperAdmin: false,
    });
    mockUseTenantDigitalService.mockReturnValue({
      data: {
        website: { isAvailable: false },
        app: { isAvailable: false },
      },
      isLoading: false,
      isError: false,
      isFetched: true,
    });

    render(<DigitalServiceAccess tenantId="test-id" blockWhenDisabled />);

    expect(screen.getByText('Digital services are disabled.')).toBeInTheDocument();
    expect(screen.queryByText('Dijital Hizmetler')).not.toBeInTheDocument();
  });
});
