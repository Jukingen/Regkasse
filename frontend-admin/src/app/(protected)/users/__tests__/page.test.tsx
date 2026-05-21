/**
 * Users page – liste, filtreler, modallar ve yetki bazlı aksiyon görünürlüğü.
 * Gateway mock’ları gerçek endpoint şekilleriyle (UserInfo, UsersListResponse) kullanır.
 */
import React from 'react';
import { describe, it, expect, vi, beforeEach, beforeAll } from 'vitest';
import '@testing-library/jest-dom';

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
import { render, screen, within, waitFor, fireEvent } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { message } from 'antd';
import { I18nProvider } from '@/i18n';
import UsersPage from '../page';
import type { UserInfo } from '@/features/users/api/usersGateway';
import type { UsersListResponse } from '@/features/users/api/usersApi';

// --- Gateway: gerçek response şekilleri ---
const mockGetUsersList = vi.fn();
const mockCreateUser = vi.fn();
const mockUpdateUser = vi.fn();
const mockDeactivateUser = vi.fn();
const mockReactivateUser = vi.fn();
const mockResetPassword = vi.fn();
const mockCreateRole = vi.fn();
const mockGetRolesWithPermissions = vi.fn();
const mockGetPermissionsCatalog = vi.fn();
const mockUpdateRolePermissions = vi.fn();
const mockDeleteRole = vi.fn();
const mockGetUserById = vi.fn();

vi.mock('@/features/users/api/usersGateway', () => ({
  listQueryKey: ['/api/UserManagement'] as const,
  rolesQueryKey: ['/api/UserManagement/roles'] as const,
  rolesWithPermissionsQueryKey: ['/api/UserManagement/roles/with-permissions'] as const,
  permissionsCatalogQueryKey: ['/api/UserManagement/roles/permissions-catalog'] as const,
  getUserByIdQueryKey: (id: string) => ['/api/UserManagement', id] as const,
  getUsersList: (params: unknown) => mockGetUsersList(params),
  getUserById: (id: string) => mockGetUserById(id),
  getRolesWithPermissions: () => mockGetRolesWithPermissions(),
  getPermissionsCatalog: () => mockGetPermissionsCatalog(),
  updateRolePermissions: (roleName: string, permissions: string[]) => mockUpdateRolePermissions(roleName, permissions),
  deleteRole: (roleName: string) => mockDeleteRole(roleName),
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
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({ user: { id: 'current-user-id', role: 'SuperAdmin' } }),
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

vi.mock('@/features/users/components/UserDetailDrawer', () => ({
  UserDetailDrawer: () => null,
}));

vi.mock('@/features/users/components/RoleManagementDrawer', () => ({
  RoleManagementDrawer: () => null,
}));

const mockPlatformUserItems = vi.fn<UserInfo[], []>(() => []);
const platformHookState = { isError: false };
vi.mock('@/features/users/hooks/usePlatformUsersList', () => ({
  usePlatformUsersList: () => ({
    items: platformHookState.isError ? [] : mockPlatformUserItems(),
    isLoading: false,
    isFetching: false,
    isError: platformHookState.isError,
    refetch: vi.fn(),
  }),
  platformUsersQueryKey: ['admin', 'users', 'platform'],
}));

const mockCreatePlatformUser = vi.fn();
vi.mock('@/features/users/api/users', () => ({
  adminUsersQueryKeys: {
    platform: (isActive?: boolean) => ['admin', 'users', 'platform', isActive ?? 'all'] as const,
    tenant: (tenantId?: string, role?: string) =>
      ['admin', 'users', 'tenant', tenantId ?? 'all', role ?? 'all'] as const,
  },
  createPlatformUser: (data: unknown) => mockCreatePlatformUser(data),
  listPlatformUsers: vi.fn().mockResolvedValue([]),
  listTenantUsers: vi.fn().mockResolvedValue([]),
  inviteAdminUser: vi.fn(),
  removeUserFromTenant: vi.fn(),
  adminUserToUserInfo: (dto: { id: string; isActive: boolean; firstName?: string; lastName?: string; email?: string; userName?: string; role?: string }) => ({
    id: dto.id,
    isActive: dto.isActive,
    firstName: dto.firstName ?? '',
    lastName: dto.lastName ?? '',
    email: dto.email,
    userName: dto.userName,
    role: dto.role,
  }),
}));

vi.mock('@/features/users/components/TenantUsersTab', () => ({
  TenantUsersTab: () => null,
}));

vi.mock('@/features/users/components/UserInvitationsPanel', () => ({
  UserInvitationsPanel: () => null,
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
        <button type="button" onClick={() => onSubmit({ userName: 'newuser', password: 'secret12', firstName: 'New', lastName: 'User', email: 'new@test.com', employeeNumber: 'EMP001', role: 'Manager' })}>
          Submit
        </button>
        <button type="button" onClick={onClose}>Close</button>
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

const sampleUser: UserInfo = {
  id: 'user-1',
  userName: 'jane',
  firstName: 'Jane',
  lastName: 'Doe',
  email: 'jane@example.com',
  role: 'SuperAdmin',
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
    </QueryClientProvider>
  );
}

describe('Users page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    platformHookState.isError = false;
    mockPlatformUserItems.mockReturnValue([]);
    mockCreatePlatformUser.mockResolvedValue({ id: 'new-platform' });
    mockGetUsersList.mockResolvedValue(listResponse([]));
    mockGetRolesWithPermissions.mockResolvedValue([]);
    mockGetPermissionsCatalog.mockResolvedValue([]);
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
    });
    vi.spyOn(message, 'success').mockImplementation((() => ({}) as any) as any);
    vi.spyOn(message, 'error').mockImplementation((() => ({}) as any) as any);
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
      } as any);
      renderPage();
      expect(screen.getByText(/Nur mit Berechtigung/)).toBeInTheDocument();
      expect(screen.getByText(/Benutzer anzeigen/)).toBeInTheDocument();
    });
  });

  describe('list loading', () => {
    it('renders title and filter controls when user can view', async () => {
      renderPage();
      await waitFor(() => {
        expect(screen.getAllByText('Benutzerverwaltung').length).toBeGreaterThanOrEqual(1);
      });
      expect(screen.getAllByText('Benutzerverwaltung').length).toBeGreaterThanOrEqual(1);
      expect(screen.getByPlaceholderText(/Name, E-Mail, Mitarbeiternummer/)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Plattform-Admin anlegen/ })).toBeInTheDocument();
    });

    it('displays user list when getUsersList succeeds', async () => {
      mockPlatformUserItems.mockReturnValue([sampleUser]);
      renderPage();
      await waitFor(() => {
        expect(screen.getByText('Jane Doe')).toBeInTheDocument();
      });
      expect(screen.getByRole('table')).toBeInTheDocument();
      expect(screen.getAllByText(/jane@example\.com/).length).toBeGreaterThanOrEqual(1);
      expect(screen.getAllByText(/Super-Administrator|SuperAdmin/).length).toBeGreaterThanOrEqual(1);
    });

    it('shows empty state when list returns no items', async () => {
      mockPlatformUserItems.mockReturnValue([]);
      renderPage();
      await waitFor(() => {
        expect(screen.getByText(/Keine Plattform-Benutzer/)).toBeInTheDocument();
      });
    });

    it('shows error alert and retry when list load fails', async () => {
      platformHookState.isError = true;
      renderPage();
      await waitFor(() => {
        expect(screen.getByText(/Benutzerliste konnte nicht geladen werden/)).toBeInTheDocument();
      });
      expect(screen.getByRole('button', { name: /Erneut versuchen/ })).toBeInTheDocument();
    });
  });

  describe('filter combination', () => {
    it('shows platform tab filters without calling tenant user list API', async () => {
      renderPage();
      await waitFor(() => {
        expect(screen.getByPlaceholderText(/Name, E-Mail, Mitarbeiternummer/)).toBeInTheDocument();
      });
      expect(mockGetUsersList).not.toHaveBeenCalled();
    });
  });

  describe('create user', () => {
    it('calls createUser and shows success when drawer submit succeeds', async () => {
      mockCreateUser.mockResolvedValue({ id: 'new-1', userName: 'newuser' });
      mockPlatformUserItems.mockReturnValue([sampleUser]);
      renderPage();
      await waitFor(() => {
        expect(screen.getByText('Jane Doe')).toBeInTheDocument();
      });
      fireEvent.click(screen.getByRole('button', { name: /Plattform-Admin anlegen/ }));
      await waitFor(() => {
        expect(screen.getByTestId('user-form-drawer')).toBeInTheDocument();
      });
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));
      await waitFor(() => {
        expect(mockCreatePlatformUser).toHaveBeenCalledWith(
          expect.objectContaining({
            userName: 'newuser',
            firstName: 'New',
            lastName: 'User',
            email: 'new@test.com',
            employeeNumber: 'EMP001',
          })
        );
      });
      await waitFor(() => {
        expect(message.success).toHaveBeenCalledWith('Benutzer angelegt.');
      });
    });

    it('shows error message when createUser fails', async () => {
      mockCreatePlatformUser.mockRejectedValue({ response: { data: { message: 'Email already exists' } } });
      renderPage();
      fireEvent.click(screen.getByRole('button', { name: /Plattform-Admin anlegen/ }));
      await waitFor(() => {
        expect(screen.getByTestId('user-form-drawer')).toBeInTheDocument();
      });
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));
      await waitFor(() => {
        expect(message.error).toHaveBeenCalled();
      });
    });
  });

  describe('edit user', () => {
    it('calls updateUser when edit drawer is submitted', async () => {
      mockUpdateUser.mockResolvedValue(undefined);
      mockGetUserById.mockResolvedValue(sampleUser);
      mockPlatformUserItems.mockReturnValue([sampleUser]);
      renderPage();
      await waitFor(() => {
        expect(screen.getByText('Jane Doe')).toBeInTheDocument();
      });
      const editBtns = screen.getAllByRole('button', { name: /Bearbeiten/ });
      fireEvent.click(editBtns[0]);
      await waitFor(() => {
        expect(screen.getByText('Edit user')).toBeInTheDocument();
      });
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));
      await waitFor(() => {
        expect(mockUpdateUser).toHaveBeenCalledWith('user-1', expect.objectContaining({ employeeNumber: 'EMP001', firstName: 'New', lastName: 'User', role: 'Manager' }));
      });
      expect(message.success).toHaveBeenCalledWith('Benutzer aktualisiert.');
    });

    it('invalidates user detail query on update success so UI rehydrates from backend', async () => {
      mockUpdateUser.mockResolvedValue(undefined);
      mockGetUserById.mockResolvedValue(sampleUser);
      mockPlatformUserItems.mockReturnValue([sampleUser]);
      const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      const invalidateSpy = vi.spyOn(queryClient, 'invalidateQueries');
      render(
        <QueryClientProvider client={queryClient}>
          <I18nProvider>
            <UsersPage />
          </I18nProvider>
        </QueryClientProvider>
      );
      await waitFor(() => expect(screen.getByText('Jane Doe')).toBeInTheDocument());
      fireEvent.click(screen.getAllByRole('button', { name: /Bearbeiten/ })[0]);
      await waitFor(() => expect(screen.getByText('Edit user')).toBeInTheDocument());
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));
      await waitFor(() => expect(mockUpdateUser).toHaveBeenCalled());
      expect(invalidateSpy).toHaveBeenCalledWith(expect.objectContaining({ queryKey: ['/api/UserManagement', 'user-1'] }));
    });
  });

  describe('deactivate flow', () => {
    it('opens deactivate modal and calls deactivateUser with reason on confirm', async () => {
      mockDeactivateUser.mockResolvedValue(undefined);
      mockPlatformUserItems.mockReturnValue([sampleUser]);
      renderPage();
      await waitFor(() => {
        expect(screen.getByText('Jane Doe')).toBeInTheDocument();
      });
      const deactivateBtns = screen.getAllByRole('button', { name: /Konto deaktivieren|Deaktivieren/ });
      fireEvent.click(deactivateBtns[0]);
      await waitFor(() => {
        expect(screen.getByText(/wird deaktiviert/)).toBeInTheDocument();
      });
      const textarea = screen.getByPlaceholderText(/z. B. Ausscheiden/);
      fireEvent.change(textarea, { target: { value: 'Ausscheiden' } });
      const dialog = screen.getByRole('dialog');
      const okBtn = within(dialog).getByRole('button', { name: /Deaktivieren/ });
      fireEvent.click(okBtn);
      await waitFor(() => {
        expect(mockDeactivateUser).toHaveBeenCalledWith('user-1', { reason: 'Ausscheiden' });
      });
      expect(message.success).toHaveBeenCalledWith('Benutzer deaktiviert.');
    });
  });

  describe('reactivate flow', () => {
    it('calls reactivateUser when reactivate modal is confirmed', async () => {
      mockReactivateUser.mockResolvedValue(undefined);
      const inactiveUser = { ...sampleUser, id: 'user-2', isActive: false, userName: 'inactive', firstName: 'In', lastName: 'Active' };
      mockPlatformUserItems.mockReturnValue([inactiveUser]);
      renderPage();
      await waitFor(() => {
        expect(screen.getByText('In Active')).toBeInTheDocument();
      });
      const reactivateBtns = screen.getAllByRole('button', { name: /Reaktivieren/ });
      fireEvent.click(reactivateBtns[0]);
      await waitFor(() => {
        expect(screen.getByText(/wieder aktivieren/)).toBeInTheDocument();
      });
      const dialog = screen.getByRole('dialog');
      const okBtn = within(dialog).getByRole('button', { name: /Reaktivieren/ });
      fireEvent.click(okBtn);
      await waitFor(() => {
        expect(mockReactivateUser).toHaveBeenCalledWith('user-2', undefined);
      });
      expect(message.success).toHaveBeenCalledWith('Benutzer reaktiviert.');
    });
  });

  describe('reset password', () => {
    it('shows validation when new password is too short', async () => {
      mockPlatformUserItems.mockReturnValue([sampleUser]);
      renderPage();
      await waitFor(() => {
        expect(screen.getByText('Jane Doe')).toBeInTheDocument();
      });
      const resetBtns = screen.getAllByRole('button', { name: /Passwort zurücksetzen/ });
      fireEvent.click(resetBtns[0]);
      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });
      const passwordInput = screen.getByPlaceholderText('••••••••');
      fireEvent.change(passwordInput, { target: { value: '12345' } });
      const okBtn = within(screen.getByRole('dialog')).getByRole('button', { name: 'Speichern' });
      fireEvent.click(okBtn);
      await waitFor(() => {
        expect(mockResetPassword).not.toHaveBeenCalled();
      });
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it.skip('calls resetPassword with new password when valid', async () => {
      mockResetPassword.mockResolvedValue(undefined);
      mockPlatformUserItems.mockReturnValue([sampleUser]);
      renderPage();
      await waitFor(() => {
        expect(screen.getByText('Jane Doe')).toBeInTheDocument();
      });
      const resetBtns = screen.getAllByRole('button', { name: /Passwort zurücksetzen/ });
      fireEvent.click(resetBtns[0]);
      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
      });
      const passwordInput = screen.getByPlaceholderText('••••••••');
      fireEvent.change(passwordInput, { target: { value: 'newPass123' } });
      const okBtn = within(screen.getByRole('dialog')).getByRole('button', { name: 'Speichern' });
      fireEvent.click(okBtn);
      await waitFor(() => {
        expect(mockResetPassword).toHaveBeenCalledWith('user-1', { newPassword: 'newPass123' });
      });
      expect(message.success).toHaveBeenCalledWith(
        'Passwort wurde zurückgesetzt. Sitzungen des Benutzers wurden ungültig.'
      );
    });
  });

  describe('permission-based action visibility', () => {
    it('hides create button when user cannot create', async () => {
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
      } as any);
      renderPage();
      await waitFor(() => {
        expect(screen.getAllByText('Benutzerverwaltung').length).toBeGreaterThanOrEqual(1);
      });
      expect(screen.queryByRole('button', { name: /Plattform-Admin anlegen|Benutzer anlegen/ })).not.toBeInTheDocument();
    });
  });
});
