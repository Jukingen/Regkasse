/**
 * @vitest-environment jsdom
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, fireEvent, render, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ActivityLog } from '../ActivityLog';

const customInstance = vi.hoisted(() =>
  vi.fn(async () => ({ auditLogs: [], totalCount: 0 }))
);

vi.mock('@/lib/axios', () => ({
  customInstance,
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => key,
    formatLocale: 'de',
  }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({ message: { success: vi.fn(), error: vi.fn() } }),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({ user: { role: 'Manager' } }),
}));

vi.mock('@/features/users/hooks/useUsersList', () => ({
  useUsersList: () => ({ data: { items: [] }, isLoading: false }),
}));

function renderLog() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const wrap = (node: ReactNode) => (
    <QueryClientProvider client={client}>{node}</QueryClientProvider>
  );
  return render(wrap(<ActivityLog />));
}

function sentSearches(): Array<string | undefined> {
  return customInstance.mock.calls.map((call) => {
    const config = call[0] as { params?: { search?: string } };
    return config.params?.search;
  });
}

describe('ActivityLog search', () => {
  beforeEach(() => {
    customInstance.mockClear();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('waits 300ms before sending the search query', async () => {
    renderLog();
    await vi.advanceTimersByTimeAsync(0);

    const input = screen.getByRole('searchbox', { name: 'activity.filters.search' });
    fireEvent.change(input, { target: { value: 'kasse@shop.at' } });
    await vi.advanceTimersByTimeAsync(299);
    expect(sentSearches().some((value) => value === 'kasse@shop.at')).toBe(false);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(20);
    });
    expect(sentSearches(), JSON.stringify(sentSearches())).toContain('kasse@shop.at');
  });
});
