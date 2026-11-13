import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { I18nProvider } from '@/i18n/I18nProvider';
import { FiskalyStatusWidget } from '@/features/dashboard/widgets/FiskalyStatusWidget';

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
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

const queryMock = vi.hoisted(() => ({
  current: {
    data: {
      isEnabled: true,
      isConfigured: true,
      isAuthenticated: true,
      environment: 'TEST',
      lastCheck: '2026-08-16T12:00:00Z',
      error: null,
    },
    isLoading: false,
    isFetching: false,
    refetch: vi.fn(),
  },
}));

vi.mock('@tanstack/react-query', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@tanstack/react-query')>();
  return {
    ...actual,
    useQuery: () => queryMock.current,
  };
});

vi.mock('@/hooks/useAuthorizedQuery', () => ({
  useAuthorizationGate: () => ({ isAuthorized: true }),
}));

describe('FiskalyStatusWidget', () => {
  it('shows active status when enabled and authenticated', () => {
    render(
      <I18nProvider>
        <FiskalyStatusWidget title="Fiskaly TSE" />
      </I18nProvider>
    );

    expect(screen.getByText('Aktiv')).toBeInTheDocument();
    expect(screen.getByText(/TEST/)).toBeInTheDocument();
  });
});
