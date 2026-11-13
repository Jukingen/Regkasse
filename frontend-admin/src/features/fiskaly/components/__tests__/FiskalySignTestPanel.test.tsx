import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { FiskalyReceiptChecksList } from '@/features/fiskaly/components/FiskalySignTestPanel';
import { I18nProvider } from '@/i18n';

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    successKey: vi.fn(),
    apiError: vi.fn(),
  }),
}));

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

describe('FiskalyReceiptChecksList', () => {
  it('renders German labels for passing and failing checks', () => {
    render(
      <I18nProvider>
        <FiskalyReceiptChecksList
          checks={{
            qrFormatValid: true,
            hasReceiptNumber: true,
            receiptNumberLooksSequential: true,
            hasTimeSignature: false,
            hasCashRegisterSerial: true,
            signed: true,
          }}
        />
      </I18nProvider>
    );

    expect(screen.getByText('QR-Format')).toBeInTheDocument();
    expect(screen.getByText('Signaturzeit')).toBeInTheDocument();
    expect(screen.getByText('Signiert')).toBeInTheDocument();
  });
});
