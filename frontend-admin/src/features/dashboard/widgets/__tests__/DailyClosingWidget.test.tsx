import '@testing-library/jest-dom';
import { fireEvent, render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import { DailyClosingWidget } from '@/features/dashboard/widgets/DailyClosingWidget';
import { I18nProvider } from '@/i18n/I18nProvider';

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

const summaryMock = vi.hoisted(() => ({
  current: {
    data: null as Record<string, unknown> | null,
    isLoading: false,
    isError: false,
    isFetching: false,
    error: null,
    refetch: vi.fn(),
  },
}));

const modalConfirm = vi.fn();

vi.mock('@/features/dashboard/api/dailyClosingDashboard', () => ({
  useDailyClosingDashboardSummary: () => summaryMock.current,
}));

vi.mock('@/hooks/useCashRegisterSelection', () => ({
  useCashRegisterSelection: () => ({ selectedRegisterId: 'reg-1' }),
}));

vi.mock('@/hooks/useAuthorizedQuery', () => ({
  useAuthorizationGate: () => ({ isAuthorized: true }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    modal: { confirm: modalConfirm },
    message: { success: vi.fn(), error: vi.fn() },
  }),
}));

vi.mock('@tanstack/react-query', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@tanstack/react-query')>();
  return {
    ...actual,
    useQueryClient: () => ({ invalidateQueries: vi.fn() }),
  };
});

vi.mock('@/api/generated/tagesabschluss/tagesabschluss', () => ({
  postApiTagesabschlussDaily: vi.fn(),
}));

function renderWidget() {
  return render(
    <I18nProvider>
      <DailyClosingWidget title="Tagesabschluss" />
    </I18nProvider>
  );
}

describe('DailyClosingWidget', () => {
  beforeEach(() => {
    modalConfirm.mockReset();
    summaryMock.current = {
      data: null,
      isLoading: false,
      isError: false,
      isFetching: false,
      error: null,
      refetch: vi.fn(),
    };
  });

  it('shows a green tag when today is closed', () => {
    summaryMock.current.data = {
      today: { isClosed: true, transactionCount: 4, canClose: false },
      week: { closedDays: 3, totalDays: 7 },
      requiresAttention: false,
      lastClosing: { closedAt: '2026-08-16T10:00:00Z' },
    };
    renderWidget();
    expect(screen.getByText('Heute geschlossen')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Heute schließen' })).not.toBeInTheDocument();
  });

  it('shows a red tag and close button when today is open with transactions', () => {
    summaryMock.current.data = {
      today: { isClosed: false, transactionCount: 12, canClose: true },
      week: { closedDays: 2, totalDays: 7 },
      requiresAttention: true,
      lastClosing: null,
    };
    renderWidget();
    expect(screen.getByText('Heute offen (12 Transaktionen)')).toBeInTheDocument();
    expect(screen.getByText('2 / 7 geschlossen')).toBeInTheDocument();
    expect(screen.getByText('Noch kein Abschluss')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Tagesabschluss öffnen' })).toHaveAttribute(
      'href',
      '/tagesabschluss'
    );
    fireEvent.click(screen.getByRole('button', { name: 'Heute schließen' }));
    expect(modalConfirm).toHaveBeenCalled();
  });

  it('shows a gray empty-day tag when today has no transactions', () => {
    summaryMock.current.data = {
      today: { isClosed: false, transactionCount: 0, canClose: true },
      week: { closedDays: 1, totalDays: 7 },
      requiresAttention: false,
      lastClosing: null,
    };
    renderWidget();
    expect(screen.getByText('Keine Transaktionen — leerer Abschluss möglich')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Heute schließen' })).not.toBeInTheDocument();
  });
});
