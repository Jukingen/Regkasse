/**
 * Users page – shell, tenant list (Manager) and unified view wiring (SuperAdmin).
 */
import React from 'react';
import { describe, it, expect, vi, beforeEach, beforeAll } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen, within, waitFor, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { I18nProvider } from '@/i18n';
import UsersPage from '../page';
import type { UserInfo } from '@/features/users/api/usersGateway';
import type { UsersListResponse } from '@/features/users/api/usersApi';

const { mockMessageSuccess, mockMessageError } = vi.hoisted(() => ({
  mockMessageSuccess: vi.fn(() => ({})),
  mockMessageError: vi.fn(() => ({})),
}));

const testPageContext = vi.hoisted(() => ({
  authRole: 'SuperAdmin' as 'SuperAdmin' | 'Manager',
  pathname: '/users',
}));

const listHookState = vi.hoisted(() => ({
  data: undefined as UsersListResponse | undefined,
  isLoading: false,
  isFetching: false,
  isError: false,
  error: null as unknown,
  refetch: vi.fn(),
}));

const unifiedViewPropsRef = vi.hoisted(() => ({
  current: null as Record<string, unknown> | null,
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
  vi.stubGlobal('getComputedStyle', () => ({
    getPropertyValue: () => '',
  }));
});

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: {
      success: mockMessageSuccess,
      error: mockMessageError,
      warning: vi.fn(() => ({})),
      info: vi.fn(() => ({})),
      open: vi.fn(() => ({})),
      loading: vi.fn(() => ({})),
    },
    modal: { confirm: vi.fn() },
    notification: {},
  }),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => testPageContext.pathname,
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    refresh: vi.fn(),
    back: vi.fn(),
    forward: vi.fn(),
    prefetch: vi.fn(),
  }),
  useSearchParams: () => new URLSearchParams(),
}));

const mockCreateUser = vi.fn();
const mockUpdateUser = vi.fn();
const mockDeactivateUser = vi.fn();
const mockReactivateUser = vi.fn();
const mockResetPassword = vi.fn();
const mockCreateRole = vi.fn();
const mockGetUserById = vi.fn();
const mockCreatePlatformUser = vi.fn();

vi.mock('@/features/users/api/usersGateway', () => ({
  listQueryKey: ['/api/UserManagement'] as const,
  rolesQueryKey: ['/api/UserManagement/roles'] as const,
  rolesWithPermissionsQueryKey: ['/api/UserManagement/roles/with-permissions'] as const,
  permissionsCatalogQueryKey: ['/api/UserManagement/roles/permissions-catalog'] as const,
  getUserByIdQueryKey: (id: string) => ['/api/UserManagement', id] as const,
  getUsersList: vi.fn(),
  getUserById: (id: string) => mockGetUserById(id),
  getRolesWithPermissions: vi.fn().mockResolvedValue([]),
  getPermissionsCatalog: vi.fn().mockResolvedValue([]),
  updateRolePermissions: vi.fn(),
  deleteRole: vi.fn(),
  createUser: (data: unknown) => mockCreateUser(data),
  updateUser: (id: string, data: unknown) => mockUpdateUser(id, data),
  deactivateUser: (id: string, data: unknown) => mockDeactivateUser(id, data),
  reactivateUser: (id: string, data?: unknown) => mockReactivateUser(id, data),
  resetPassword: (id: string, data: unknown) => mockResetPassword(id, data),
  createRole: (data: unknown) => mockCreateRole(data),
  normalizeError: (err: unknown, fallback: string) =>
    (err as { response?: { data?: { message?: string }; message?: string } })?.response?.data?.message ??
    (err as Error)?.message ??
    fallback,
  useGenerateTemporaryPasswordMutation: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
  }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({ user: { id: 'current-user-id', role: testPageContext.authRole } }),
}));

const mockUseUsersPolicy = vi.fn(() => ({
  canView: true,
  canCreate: true,
  canEdit: true,
  canDeactivate: true,
  canReactivate: true,
  canCreateRole: false,
  canDeleteRole: false,
  canEditRolePermissions: false,
  canResetPassword: () => true,
  useGeneratedPasswordReset: false,
  canProvisionTenantCredentials: true,
  canManagePermissions: false,
}));

vi.mock('@/shared/auth/usersPolicy', () => ({
  useUsersPolicy: () => mockUseUsersPolicy(),
}));

vi.mock('@/features/users/hooks/useRoles', () => ({
  useRoles: () => ({
    data: ['SuperAdmin', 'Manager', 'Cashier', 'Waiter', 'Kitchen', 'ReportViewer', 'Accountant'],
    isLoading: false,
  }),
}));

vi.mock('@/features/users/hooks/useUsersList', () => ({
  useUsersList: (_params: unknown, options?: { enabled?: boolean }) => {
    if (options?.enabled === false) {
      return {
        data: undefined,
        isLoading: false,
        isFetching: false,
        isError: false,
        error: null,
        refetch: vi.fn(),
      };
    }
    return listHookState;
  },
}));

vi.mock('@/features/tenancy/providers/TenantProvider', () => ({
  useTenant: () => ({
    tenant: testPageContext.authRole === 'SuperAdmin' ? null : 'dev',
    setTenant: vi.fn(),
    clearTenant: vi.fn(),
  }),
}));

vi.mock('@/features/tenancy/hooks/useCurrentTenant', () => ({
  useCurrentTenant: () => ({
    hasAuthToken: true,
    isSuperAdminPlatformMode: testPageContext.authRole === 'SuperAdmin',
    isRealTenantSlug: testPageContext.authRole !== 'SuperAdmin',
    tenantName: 'Test Cafe',
    tenantSlug: 'dev',
    tenantId: 'tenant-1',
    isTenantSuspended: false,
    licenseValidUntilUtc: null,
    licenseKey: null,
    isTenantRecordLoading: false,
    isDevTenantOverride: false,
    isImpersonating: false,
  }),
}));

vi.mock('@/features/users/components/UserDetailDrawer', () => ({
  UserDetailDrawer: () => null,
}));

vi.mock('@/features/users/components/EditUsernameModal', () => ({
  EditUsernameModal: () => null,
}));

vi.mock('@/features/users/components/UserPermissionsModal', () => ({
  UserPermissionsModal: () => null,
}));

vi.mock('@/features/access/components/AccessSecondaryNav', () => ({
  AccessSecondaryNav: () => <nav data-testid="access-secondary-nav" />,
}));

vi.mock('@/features/users/components/UnifiedAdminUsersView', () => ({
  UnifiedAdminUsersView: (props: {
    onCreatePlatformUser: () => void;
    onEdit: (id: string) => void;
    onDeactivate: (user: UserInfo) => void;
    onReactivate: (user: UserInfo) => void;
    onResetPassword: (user: UserInfo) => void;
    policy: { canCreate: boolean };
    tenantScopeId?: string;
    isSuperAdminActor?: boolean;
  }) => {
    unifiedViewPropsRef.current = props as unknown as Record<string, unknown>;
    const showPlatformCreate =
      props.isSuperAdminActor !== false && props.policy.canCreate;
    return (
      <div data-testid="unified-admin-users-view">
        {props.tenantScopeId ? (
          <span data-testid="tenant-scope-id">{props.tenantScopeId}</span>
        ) : null}
        {showPlatformCreate ? (
          <button type="button" onClick={() => props.onCreatePlatformUser()}>
            Plattform-Admin anlegen
          </button>
        ) : null}
        <button type="button" onClick={() => props.onEdit('user-1')}>
          Bearbeiten
        </button>
        <button
          type="button"
          onClick={() =>
            props.onDeactivate({
              id: 'user-1',
              userName: 'jane',
              firstName: 'Jane',
              lastName: 'Doe',
              email: 'jane@example.com',
              role: 'SuperAdmin',
              isActive: true,
            } as UserInfo)
          }
        >
          Deaktivieren
        </button>
        <button
          type="button"
          onClick={() =>
            props.onReactivate({
              id: 'user-2',
              userName: 'inactive',
              firstName: 'In',
              lastName: 'Active',
              email: 'inactive@test.com',
              role: 'Manager',
              isActive: false,
            } as UserInfo)
          }
        >
          Reaktivieren
        </button>
        <button
          type="button"
          onClick={() =>
            props.onResetPassword({
              id: 'user-1',
              userName: 'jane',
              firstName: 'Jane',
              lastName: 'Doe',
              email: 'jane@example.com',
              role: 'SuperAdmin',
              isActive: true,
            } as UserInfo)
          }
        >
          Passwort zurücksetzen
        </button>
      </div>
    );
  },
}));

vi.mock('@/features/users/api/users', () => ({
  adminUsersQueryKeys: {
    all: (isActive?: boolean, role?: string, search?: string) =>
      ['admin', 'users', 'all', isActive ?? 'all', role ?? 'all', search ?? ''] as const,
    platform: (isActive?: boolean, search?: string) =>
      ['admin', 'users', 'platform', isActive ?? 'all', search ?? ''] as const,
    tenant: (tenantId?: string, role?: string, search?: string) =>
      ['admin', 'users', 'tenant', tenantId ?? 'all', role ?? 'all', search ?? ''] as const,
    userTenants: (userId: string) => ['admin', 'users', userId, 'tenants'] as const,
  },
  createPlatformUser: (data: unknown) => mockCreatePlatformUser(data),
  getAdminUserTenants: vi.fn().mockResolvedValue([]),
  listAllAdminUsers: vi.fn().mockResolvedValue({ items: [], totalCount: 0 }),
  listPlatformUsers: vi.fn().mockResolvedValue([]),
  listTenantUsers: vi.fn().mockResolvedValue([]),
  removeUserFromTenant: vi.fn(),
  updateUserRole: vi.fn().mockResolvedValue({ userId: 'user-1', role: 'Cashier' }),
  updateUserTenants: vi.fn(),
}));

vi.mock('@/features/users/components/UserFormDrawer', () => ({
  UserFormDrawer: ({ open, onClose, mode, onSubmit }: {
    open: boolean;
    onClose: () => void;
    mode: string;
    onSubmit: (v: unknown) => void;
  }) =>
    open ? (
      <div data-testid="user-form-drawer">
        <span>{mode === 'create' ? 'Create user' : 'Edit user'}</span>
        <button
          type="button"
          onClick={() =>
            onSubmit({
              userName: 'newuser',
              password: 'secret12',
              firstName: 'New',
              lastName: 'User',
              email: 'new@test.com',
              employeeNumber: 'EMP001',
              role: 'Manager',
            })
          }
        >
          Submit
        </button>
        <button type="button" onClick={onClose}>
          Close
        </button>
      </div>
    ) : null,
}));

function listResponse(items: UserInfo[], totalCount?: number): UsersListResponse {
  const count = totalCount ?? items.length;
  return {
    items,
    pagination: { page: 1, pageSize: 20, totalCount: count, totalPages: Math.ceil(count / 20) || 1 },
  };
}

const tenantSampleUser: UserInfo = {
  id: 'user-1',
  userName: 'jane',
  firstName: 'Jane',
  lastName: 'Doe',
  email: 'jane@example.com',
  role: 'Manager',
  isActive: true,
  employeeNumber: 'E001',
  lastLoginAt: '2025-01-15T10:00:00Z',
} as UserInfo;

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <UsersPage />
      </I18nProvider>
    </QueryClientProvider>,
  );
}

function useSuperAdminContext() {
  testPageContext.authRole = 'SuperAdmin';
  testPageContext.pathname = '/users';
}

function useManagerContext() {
  testPageContext.authRole = 'Manager';
  testPageContext.pathname = '/admin/users';
}

describe('Users page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useSuperAdminContext();
    unifiedViewPropsRef.current = null;
    listHookState.data = listResponse([]);
    listHookState.isLoading = false;
    listHookState.isFetching = false;
    listHookState.isError = false;
    listHookState.error = null;
    listHookState.refetch = vi.fn();
    mockCreatePlatformUser.mockResolvedValue({ id: 'new-platform' });
    mockGetUserById.mockResolvedValue(tenantSampleUser);
    mockUseUsersPolicy.mockReturnValue({
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
      canManagePermissions: false,
    });
  });

  describe('authorization', () => {
    it('shows no-permission alert when user cannot view (e.g. Cashier)', () => {
      mockUseUsersPolicy.mockReturnValue({
        canView: false,
        canCreate: false,
        canEdit: false,
        canDeactivate: false,
        canReactivate: false,
        canCreateRole: false,
        canDeleteRole: false,
        canEditRolePermissions: false,
        canResetPassword: () => false,
        canManagePermissions: false,
      } as ReturnType<typeof mockUseUsersPolicy>);
      renderPage();
      expect(screen.getByText(/Nur mit Berechtigung/)).toBeInTheDocument();
      expect(screen.getByText(/Benutzer anzeigen/)).toBeInTheDocument();
    });
  });

  describe('SuperAdmin unified view', () => {
    it('renders page shell with unified intro and delegates to UnifiedAdminUsersView', async () => {
      renderPage();
      await waitFor(() => {
        expect(screen.getAllByText('Benutzerverwaltung').length).toBeGreaterThanOrEqual(1);
      });
      expect(
        screen.getByText(/Alle Plattform- und Mandanten-Benutzer an einem Ort/),
      ).toBeInTheDocument();
      expect(screen.getByTestId('unified-admin-users-view')).toBeInTheDocument();
      expect(screen.queryByTestId('access-secondary-nav')).not.toBeInTheDocument();
      expect(unifiedViewPropsRef.current).not.toBeNull();
    });

    it('creates platform user via unified view callback and drawer submit', async () => {
      mockCreatePlatformUser.mockResolvedValue({ id: 'new-platform' });
      renderPage();
      fireEvent.click(screen.getByRole('button', { name: /Plattform-Admin anlegen/ }));
      await waitFor(() => {
        expect(screen.getByTestId('user-form-drawer')).toBeInTheDocument();
      });
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));
      await waitFor(() => {
        expect(mockCreatePlatformUser).toHaveBeenCalledWith(
          expect.objectContaining({
            firstName: 'New',
            lastName: 'User',
            email: 'new@test.com',
            role: 'SuperAdmin',
          }),
        );
      });
      expect(mockMessageSuccess).toHaveBeenCalledWith('Benutzer angelegt.');
    });

    it('shows error when platform user creation fails', async () => {
      mockCreatePlatformUser.mockRejectedValue({ response: { data: { message: 'Email already exists' } } });
      renderPage();
      fireEvent.click(screen.getByRole('button', { name: /Plattform-Admin anlegen/ }));
      await waitFor(() => {
        expect(screen.getByTestId('user-form-drawer')).toBeInTheDocument();
      });
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));
      await waitFor(() => {
        expect(mockMessageError).toHaveBeenCalled();
      });
    });

    it('opens edit drawer from unified view callback', async () => {
      renderPage();
      fireEvent.click(screen.getAllByRole('button', { name: /Bearbeiten/ })[0]);
      await waitFor(() => {
        expect(screen.getByText('Edit user')).toBeInTheDocument();
      });
    });

    it('hides unified create action when user cannot create', async () => {
      mockUseUsersPolicy.mockReturnValue({
        canView: true,
        canCreate: false,
        canEdit: false,
        canDeactivate: false,
        canReactivate: false,
        canCreateRole: false,
        canDeleteRole: false,
        canEditRolePermissions: false,
        canResetPassword: () => false,
        canManagePermissions: false,
      } as ReturnType<typeof mockUseUsersPolicy>);
      renderPage();
      await waitFor(() => {
        expect(screen.getByTestId('unified-admin-users-view')).toBeInTheDocument();
      });
      expect(screen.queryByRole('button', { name: /Plattform-Admin anlegen/ })).not.toBeInTheDocument();
    });
  });

  describe('Manager tenant list', () => {
    beforeEach(() => {
      useManagerContext();
      listHookState.data = listResponse([tenantSampleUser]);
      mockUseUsersPolicy.mockReturnValue({
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
        canManagePermissions: false,
      });
    });

    it('renders access hub nav and scoped unified view for the JWT tenant', async () => {
      renderPage();
      await waitFor(() => {
        expect(screen.getByTestId('access-secondary-nav')).toBeInTheDocument();
      });
      expect(screen.getByText('Zugriff & Rollen')).toBeInTheDocument();
      expect(screen.getByTestId('unified-admin-users-view')).toBeInTheDocument();
      expect(screen.getByTestId('tenant-scope-id')).toHaveTextContent('tenant-1');
      expect(unifiedViewPropsRef.current).toMatchObject({
        tenantScopeId: 'tenant-1',
        isSuperAdminActor: false,
      });
      expect(screen.queryByRole('button', { name: /Plattform-Admin anlegen/ })).not.toBeInTheDocument();
    });

    it('opens edit drawer from unified view callback', async () => {
      renderPage();
      fireEvent.click(screen.getByRole('button', { name: /Bearbeiten/ }));
      await waitFor(() => {
        expect(screen.getByText('Edit user')).toBeInTheDocument();
      });
    });

    it('opens deactivate modal from unified view callback', async () => {
      mockDeactivateUser.mockResolvedValue(undefined);
      renderPage();
      fireEvent.click(screen.getByRole('button', { name: /Deaktivieren/ }));
      await waitFor(() => {
        expect(screen.getByText(/wird deaktiviert/)).toBeInTheDocument();
      });
    });

    it('hides platform create when scoped as Manager', async () => {
      renderPage();
      expect(screen.queryByRole('button', { name: /Plattform-Admin anlegen/ })).not.toBeInTheDocument();
    });
  });
});
