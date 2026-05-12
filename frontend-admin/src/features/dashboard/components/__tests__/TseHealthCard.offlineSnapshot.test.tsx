/**
 * Admin dashboard TSE card: offline snapshot maps to German operator-facing labels.
 */
import React from 'react';
import { describe, it, expect, vi, beforeAll } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { TseHealthCard } from '@/features/dashboard/components/TseHealthCard';

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

vi.mock('@/api/generated/tse/tse', () => ({
  useGetApiTseHealth: vi.fn(() => ({
    data: {
      status: 'Offline',
      consecutiveFailures: 4,
      lastCheckUtc: '2026-05-12T10:00:00Z',
      lastSuccessfulPingUtc: null,
      estimatedRecoveryTimeUtc: '2026-05-12T10:05:00Z',
      lastErrorMessageSafe: 'probe failed (test)',
    },
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
  })),
}));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('TseHealthCard', () => {
  it('shows offline badge and alert when API reports Offline', () => {
    renderWithClient(<TseHealthCard />);
    expect(screen.getByText('TSE-Status')).toBeInTheDocument();
    expect(screen.getByText('Offline')).toBeInTheDocument();
    expect(screen.getByText('TSE offline')).toBeInTheDocument();
  });
});
