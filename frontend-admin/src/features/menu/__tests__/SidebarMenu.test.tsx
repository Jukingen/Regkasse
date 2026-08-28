import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { SidebarMenu } from '@/features/menu/SidebarMenu';
import { useFavoritesStore } from '@/features/menu/favoritesStore';
import { sidebarFavoritesStorageKey } from '@/features/menu/menuConfig';
import { I18nProvider } from '@/i18n';
import { ADMIN_SIDEBAR_GROUP_KEYS } from '@/shared/adminSidebarNavigation';

const mockUseAuth = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    warning: vi.fn(),
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  }),
}));

vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

const VISIBLE = new Set([
  ADMIN_SIDEBAR_GROUP_KEYS.operations,
  '/receipts',
  '/tagesabschluss',
  '/products',
  ADMIN_SIDEBAR_GROUP_KEYS.rksv,
]);

function renderFavorites() {
  return render(
    <I18nProvider>
      <SidebarMenu visibleMenuKeys={VISIBLE} collapsed={false} />
    </I18nProvider>
  );
}

describe('SidebarMenu', () => {
  beforeEach(() => {
    const storage: Record<string, string> = {};
    vi.stubGlobal('localStorage', {
      getItem: (k: string) => storage[k] ?? null,
      setItem: (k: string, v: string) => {
        storage[k] = v;
      },
      removeItem: (k: string) => {
        delete storage[k];
      },
    });
    useFavoritesStore.getState().resetSession();
    mockUseAuth.mockReturnValue({
      user: { id: 'mgr-1', role: 'Manager' },
    });
  });

  it('shows the empty copy when the user has no favorites', async () => {
    window.localStorage.setItem(sidebarFavoritesStorageKey('mgr-1'), JSON.stringify([]));
    renderFavorites();
    expect(
      await screen.findByText(
        'Noch keine Favoriten. Klicken Sie auf den Stern neben einem Menüpunkt, um ihn hinzuzufügen.'
      )
    ).toBeInTheDocument();
    expect(screen.getByText('Favoriten')).toBeInTheDocument();
  });

  it('renders seeded Manager favorites and unpins on star click', async () => {
    const user = userEvent.setup();
    renderFavorites();
    expect(await screen.findByText('Verkauf')).toBeInTheDocument();
    expect(screen.getByText('Belege')).toBeInTheDocument();
    expect(screen.getByText('Tagesabschluss')).toBeInTheDocument();
    expect(screen.getByText('Produkte')).toBeInTheDocument();

    const unpinButtons = screen.getAllByLabelText('Aus Favoriten entfernen');
    await user.click(unpinButtons[0]!);
    expect(screen.queryByText('Verkauf')).not.toBeInTheDocument();
  });

  it('hides the section when the sider is collapsed', () => {
    window.localStorage.setItem(sidebarFavoritesStorageKey('mgr-1'), JSON.stringify([]));
    render(
      <I18nProvider>
        <SidebarMenu visibleMenuKeys={VISIBLE} collapsed />
      </I18nProvider>
    );
    expect(screen.queryByText('Favoriten')).not.toBeInTheDocument();
  });
});
