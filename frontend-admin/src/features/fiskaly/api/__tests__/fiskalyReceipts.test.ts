import { describe, expect, it, vi } from 'vitest';

import { parseFiskalyReceiptError, postFiskalyReceiptOperation } from '../fiskalyReceipts';

vi.mock('@/lib/axios', () => ({
  customInstance: vi.fn(),
}));

import { customInstance } from '@/lib/axios';

describe('parseFiskalyReceiptError', () => {
  it('reads nested error envelope from axios response', () => {
    const parsed = parseFiskalyReceiptError({
      response: {
        status: 400,
        data: {
          success: false,
          error: {
            code: 'FISKALY_AUTH_FAILED',
            message: 'Fiskaly authentication failed',
            details: 'Invalid API key or secret',
          },
        },
      },
    });
    expect(parsed).toEqual({
      code: 'FISKALY_AUTH_FAILED',
      message: 'Fiskaly authentication failed',
      details: 'Invalid API key or secret',
    });
  });

  it('falls back to top-level message when nested error message is empty', () => {
    const parsed = parseFiskalyReceiptError({
      response: {
        status: 400,
        data: {
          success: false,
          message: 'Startbeleg bereits vorhanden',
          error: { code: 'DUPLICATE_STARTBELEG', message: '', details: 'Für diese Kasse wurde bereits ein Startbeleg erstellt.' },
        },
      },
    });
    expect(parsed).toEqual({
      code: 'DUPLICATE_STARTBELEG',
      message: 'Startbeleg bereits vorhanden',
      details: 'Für diese Kasse wurde bereits ein Startbeleg erstellt.',
    });
  });

  it('posts tagesabschluss by register when closing id is omitted', async () => {
    const mock = vi.mocked(customInstance);
    mock.mockResolvedValueOnce({
      success: true,
      data: { receiptId: 'rx-reg', receiptNumber: 'TA-20260829' },
    });

    const result = await postFiskalyReceiptOperation('tagesabschluss', {
      cashRegisterId: 'reg-1',
      closingDate: '2026-08-28',
    });

    expect(result.success).toBe(true);
    expect(mock).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/fiskaly/receipt/tagesabschluss',
        method: 'POST',
        data: { cashRegisterId: 'reg-1', closingDate: '2026-08-28' },
      })
    );
  });

  it('posts tagesabschluss to the closing-id route', async () => {
    const mock = vi.mocked(customInstance);
    mock.mockResolvedValueOnce({
      success: true,
      data: { receiptId: 'rx-1', receiptNumber: 'TA-20260829' },
    });

    const result = await postFiskalyReceiptOperation('tagesabschluss', {
      cashRegisterId: 'reg-1',
      closingId: 'closing-1',
    });

    expect(result.success).toBe(true);
    expect(mock).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/fiskaly/receipt/tagesabschluss/closing-1',
        method: 'POST',
      })
    );
  });
});
