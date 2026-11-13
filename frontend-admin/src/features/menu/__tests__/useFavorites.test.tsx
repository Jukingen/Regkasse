import { renderHook, waitFor, act } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { ReactNode } from 'react';

import { useFavoritesStore } from '@/features/menu/favoritesStore';
import { sidebarFavoritesStorageKey } from '@/features/menu/menuConfig';
import { useFavorites } from '@/features/menu/useFavorites';
import { I18nProvider } from '@/i18n';

const mockUseAuth = vi.fn();
const mockWarning = vi.fn();

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    warning: mockWarning,
  }),
}));

function wrapper({ children }: { children: ReactNode }) {
  return <I18nProvider>{children}</I18nProvider>;
}

describe('useFavorites', () => {
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
    mockWarning.mockReset();
  });

  it('hides favorited keys that are no longer in the visible menu', async () => {
    mockUseAuth.mockReturnValue({
      user: { id: 'u-fav', role: 'Cashier' },
    });
    window.localStorage.setItem(
      sidebarFavoritesStorageKey('u-fav'),
      JSON.stringify(['/a', '/b', '/gone'])
    );

    const { result } = renderHook(
      () => useFavorites({ visibleMenuKeys: new Set(['/a', '/b']) }),
      { wrapper }
    );

    await waitFor(() => {
      expect(result.current.hydrated).toBe(true);
    });
    expect(result.current.favorites).toEqual(['/a', '/b']);
    expect(result.current.isFavorite('/gone')).toBe(false);
  });

  it('warns when adding an 8th favorite', async () => {
    mockUseAuth.mockReturnValue({ user: { id: 'u-max', role: 'Cashier' } });
    window.localStorage.setItem(
      sidebarFavoritesStorageKey('u-max'),
      JSON.stringify(['/1', '/2', '/3', '/4', '/5', '/6', '/7'])
    );

    const { result } = renderHook(
      () =>
        useFavorites({
          visibleMenuKeys: new Set(['/1', '/2', '/3', '/4', '/5', '/6', '/7', '/8']),
        }),
      { wrapper }
    );
    await waitFor(() => {
      expect(result.current.hydrated).toBe(true);
      expect(result.current.favorites).toHaveLength(7);
    });

    act(() => {
      result.current.addFavorite('/8');
    });
    expect(mockWarning).toHaveBeenCalledWith('adminShell.sidebar.favorites.maxReached');
    expect(result.current.favorites).toHaveLength(7);
  });
});
