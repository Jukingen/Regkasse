import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { FiskalyOperationStatusBadge } from '@/features/fiskaly/components/FiskalyOperationStatusBadge';
import { I18nProvider } from '@/i18n';

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeEventListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('FiskalyOperationStatusBadge', () => {
  it('renders completed label for Success', () => {
    render(
      <I18nProvider>
        <FiskalyOperationStatusBadge status="Success" />
      </I18nProvider>
    );
    expect(screen.getByText('Abgeschlossen')).toBeInTheDocument();
  });

  it('renders processing label', () => {
    render(
      <I18nProvider>
        <FiskalyOperationStatusBadge status="Processing" />
      </I18nProvider>
    );
    expect(screen.getByText('In Bearbeitung')).toBeInTheDocument();
  });
});
