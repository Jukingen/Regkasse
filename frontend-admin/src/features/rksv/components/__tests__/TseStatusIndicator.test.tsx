import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { TseStatusIndicator } from '@/features/rksv/components/TseStatusIndicator';
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
      scuInitialized: true,
      scuId: 'scu-1',
      environment: 'TEST',
    },
    isLoading: false,
  },
}));

vi.mock('@tanstack/react-query', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@tanstack/react-query')>();
  return {
    ...actual,
    useQuery: () => queryMock.current,
  };
});

vi.mock('@/shared/auth/usePermissions', () => ({
  usePermissions: () => ({ hasPermission: () => true }),
}));

describe('TseStatusIndicator', () => {
  it('shows active TSE when Fiskaly SCU is initialized', () => {
    render(
      <I18nProvider>
        <TseStatusIndicator />
      </I18nProvider>
    );

    expect(screen.getByText('TSE aktiv')).toBeInTheDocument();
    expect(screen.getByText(/SCU: scu-1/)).toBeInTheDocument();
  });

  it('shows disabled state when Fiskaly is off', () => {
    queryMock.current = {
      data: {
        isEnabled: false,
        isConfigured: true,
        isAuthenticated: false,
        scuInitialized: false,
        scuId: null,
        environment: 'TEST',
      },
      isLoading: false,
    };

    render(
      <I18nProvider>
        <TseStatusIndicator />
      </I18nProvider>
    );

    expect(screen.getByText('TSE deaktiviert')).toBeInTheDocument();
  });
});
