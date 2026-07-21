/**
 * FA dashboard Tagesabschluss reminder — only when closing is still due.
 */
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { TagesabschlussReminder } from '@/features/dashboard/components/TagesabschlussReminder';
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

const useTagesabschlussStatusMock = vi.fn();

vi.mock('@/hooks/useTagesabschlussStatus', () => ({
  useTagesabschlussStatus: () => useTagesabschlussStatusMock(),
}));

vi.mock('@/hooks/useCanAccessPath', () => ({
  useCanAccessPath: () => true,
}));

describe('TagesabschlussReminder', () => {
  it('renders nothing when closing is not required', () => {
    useTagesabschlussStatusMock.mockReturnValue({
      isClosingRequired: false,
      register: { id: 'r1', name: 'Kasse-1 — Hauptraum' },
      transactionCount: 0,
      isLoading: false,
    });

    const { container } = render(
      <I18nProvider>
        <TagesabschlussReminder />
      </I18nProvider>
    );

    expect(container).toBeEmptyDOMElement();
  });

  it('shows reminder with register name and CTA when closing is due', () => {
    useTagesabschlussStatusMock.mockReturnValue({
      isClosingRequired: true,
      register: { id: 'r1', name: 'Kasse-1 — Hauptraum' },
      transactionCount: 12,
      isLoading: false,
    });

    render(
      <I18nProvider>
        <TagesabschlussReminder />
      </I18nProvider>
    );

    expect(screen.getByText('Tagesabschluss steht aus')).toBeInTheDocument();
    expect(screen.getByText(/Kasse-1 — Hauptraum/)).toBeInTheDocument();
    expect(screen.getByText(/12 Transaktionen/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Jetzt durchführen' })).toHaveAttribute(
      'href',
      '/tagesabschluss'
    );
  });
});
