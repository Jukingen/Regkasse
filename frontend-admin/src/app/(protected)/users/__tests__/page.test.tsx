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

vi.mock('@/features/users/api/usersGateway', () => ({
  listQueryKey: ['/api/UserManagement'] as const,
  rolesQueryKey: ['/api/UserManagement/roles'] as const,
  getUsersList: (params: unknown) => mockGetUsersList(params),
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
  useAuth: () => ({ user: { id: 'current-user-id', role: 'Admin' } }),
}));

const mockUseUsersPolicy = vi.fn(() => ({
  canView: true,
  canCreate: true,
  canEdit: true,
  canDeactivate: true,
  canReactivate: true,
  canCreateRole: false,
  canResetPassword: () => true,
}));
vi.mock('@/shared/auth/usersPolicy', () => ({
  useUsersPolicy: () => mockUseUsersPolicy(),
}));

vi.mock('@/features/users/hooks/useRoles', () => ({
  useRoles: () => ({ data: ['Admin', 'SuperAdmin', 'BranchManager', 'Auditor'] }),
}));

vi.mock('@/features/users/components/UserDetailDrawer', () => ({
  UserDetailDrawer: () => null,
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
        <button type="button" onClick={() => onSubmit({ userName: 'newuser', password: 'secret12', firstName: 'New', lastName: 'User', email: 'new@test.com', employeeNumber: 'EMP001', role: 'Admin' })}>
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
  role: 'Admin',
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
      <UsersPage />
    </QueryClientProvider>
  );
}

describe('Users page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetUsersList.mockResolvedValue(listResponse([]));
    vi.spyOn(message, 'success').mockImplementation(() => {});
    vi.spyOn(message, 'error').mockImplementation(() => {});
  });

  describe('list loading', () => {
    it('renders title and filter controls when user can view', async () => {
      renderPage();
      await waitFor(() => {
        expect(mockGetUsersList).toHaveBeenCalled();
      });
      expect(screen.getByText('Benutzerverwaltung')).toBeInTheDocument();
      expect(screen.getByPlaceholderText(/Name, E-Mail, Mitarbeiternummer/)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Benutzer anlegen/ })).toBeInTheDocument();
    });

    it('displays user list when getUsersList succeeds', async () => {
      mockGetUsersList.mockResolvedValue(listResponse([sampleUser]));
      renderPage();
      await waitFor(() => {
        expect(screen.getByText('Jane Doe')).toBeInTheDocument();
      });
      expect(screen.getByRole('table')).toBeInTheDocument();
      expect(screen.getAllByText(/jane@example\.com/).length).toBeGreaterThanOrEqual(1);
      expect(screen.getByText('Admin')).toBeInTheDocument();
    });

    it('shows empty state when list returns no items', async () => {
      mockGetUsersList.mockResolvedValue(listResponse([], 0));
      renderPage();
      await waitFor(() => {
        expect(mockGetUsersList).toHaveBeenCalled();
      });
      expect(screen.getByText('Keine Benutzer gefunden.')).toBeInTheDocument();
    });

    it('shows error alert and retry when list load fails', async () => {
      mockGetUsersList.mockRejectedValue(new Error('Network error'));
      renderPage();
      await waitFor(() => {
        expect(screen.getByText(/Benutzerliste konnte nicht geladen werden/)).toBeInTheDocument();
      });
      expect(screen.getByRole('button', { name: /Erneut versuchen/ })).toBeInTheDocument();
    });
  });

  describe('filter combination', () => {
    it('calls getUsersList with default page and isActive on initial load', async () => {
      renderPage();
      await waitFor(() => {
        expect(mockGetUsersList).toHaveBeenCalled();
      });
      expect(mockGetUsersList).toHaveBeenCalledWith(
        expect.objectContaining({ page: 1, pageSize: 20, isActive: true })
      );
    });

    it('calls getUsersList with query when search is submitted', async () => {
      renderPage();
      await waitFor(() => {
        expect(mockGetUsersList).toHaveBeenCalled();
      });
      mockGetUsersList.mockClear();
      const search = screen.getByPlaceholderText(/Name, E-Mail, Mitarbeiternummer/);
      fireEvent.change(search, { target: { value: 'jane' } });
      const searchButton = search.closest('div')?.querySelector('button.ant-input-search-button');
      if (searchButton) fireEvent.click(searchButton);
      await waitFor(() => {
        const calls = mockGetUsersList.mock.calls;
        const withQuery = calls.some((c) => c[0]?.query === 'jane');
        expect(withQuery).toBe(true);
      });
    });
  });

  describe('create user', () => {
    it('calls createUser and shows success when drawer submit succeeds', async () => {
      mockCreateUser.mockResolvedValue({ id: 'new-1', userName: 'newuser' });
      mockGetUsersList.mockResolvedValue(listResponse([sampleUser]));
      renderPage();
      await waitFor(() => {
        expect(mockGetUsersList).toHaveBeenCalled();
      });
      fireEvent.click(screen.getByRole('button', { name: /Benutzer anlegen/ }));
      await waitFor(() => {
        expect(screen.getByTestId('user-form-drawer')).toBeInTheDocument();
      });
      fireEvent.click(screen.getByRole('button', { name: 'Submit' }));
      await waitFor(() => {
        expect(mockCreateUser).toHaveBeenCalledWith(
          expect.objectContaining({
            userName: 'newuser',
            firstName: 'New',
            lastName: 'User',
            email: 'new@test.com',
            employeeNumber: 'EMP001',
            role: 'Admin',
          })
        );
      });
      await waitFor(() => {
        expect(message.success).toHaveBeenCalledWith('Benutzer angelegt.');
      });
    });

    it('shows error message when createUser fails', async () => {
      mockCreateUser.mockRejectedValue({ response: { data: { message: 'Email already exists' } } });
      renderPage();
      await waitFor(() => {
        expect(mockGetUsersList).toHaveBeenCalled();
      });
      fireEvent.click(screen.getByRole('button', { name: /Benutzer anlegen/ }));
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
      mockGetUsersList.mockResolvedValue(listResponse([sampleUser]));
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
        expect(mockUpdateUser).toHaveBeenCalledWith('user-1', expect.objectContaining({ employeeNumber: 'EMP001', firstName: 'New', lastName: 'User', role: 'Admin' }));
      });
      expect(message.success).toHaveBeenCalledWith('Benutzer aktualisiert.');
    });
  });

  describe('deactivate flow', () => {
    it('opens deactivate modal and calls deactivateUser with reason on confirm', async () => {
      mockDeactivateUser.mockResolvedValue(undefined);
      mockGetUsersList.mockResolvedValue(listResponse([sampleUser]));
      renderPage();
      await waitFor(() => {
        expect(screen.getByText('Jane Doe')).toBeInTheDocument();
      });
      const deactivateBtns = screen.getAllByRole('button', { name: /Deaktivieren/ });
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
      mockGetUsersList.mockResolvedValue(listResponse([inactiveUser]));
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
      mockGetUsersList.mockResolvedValue(listResponse([sampleUser]));
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

    it('calls resetPassword with new password when valid', async () => {
      mockResetPassword.mockResolvedValue(undefined);
      mockGetUsersList.mockResolvedValue(listResponse([sampleUser]));
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
        canResetPassword: () => false,
      });
      renderPage();
      await waitFor(() => {
        expect(mockGetUsersList).toHaveBeenCalled();
      });
      expect(screen.queryByRole('button', { name: /Benutzer anlegen/ })).not.toBeInTheDocument();
    });
  });
});
