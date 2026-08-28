import { describe, expect, it, jest } from '@jest/globals';

import {
  parsePosReceiptList,
  parsePosReceiptListItem,
  parsePosReceiptReprint,
} from '../services/api/receiptService';

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: jest.fn(),
    post: jest.fn(),
  },
  API_BASE_URL: 'http://test/api',
}));

describe('receiptService parsers', () => {
  it('parsePosReceiptListItem reads camelCase POS list row', () => {
    const item = parsePosReceiptListItem({
      receiptId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      paymentId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      receiptNumber: 'AT-1',
      issuedAt: '2026-08-28T08:00:00Z',
      grandTotal: 12.5,
      cashRegisterEntityId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    });
    expect(item).toEqual({
      receiptId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      paymentId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      receiptNumber: 'AT-1',
      issuedAt: '2026-08-28T08:00:00Z',
      grandTotal: 12.5,
      cashRegisterId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    });
  });

  it('parsePosReceiptList unwraps items envelope', () => {
    const rows = parsePosReceiptList({
      items: [
        {
          ReceiptId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
          PaymentId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
          ReceiptNumber: 'AT-2',
          IssuedAt: '2026-08-28T09:00:00Z',
          GrandTotal: 3,
          CashRegisterId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
        },
      ],
    });
    expect(rows).toHaveLength(1);
    expect(rows[0].receiptNumber).toBe('AT-2');
  });

  it('parsePosReceiptReprint reads paymentId from nested receipt', () => {
    const result = parsePosReceiptReprint({
      outcome: 'Success',
      receipt: {
        receiptId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        paymentId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        receiptNumber: 'AT-3',
      },
    });
    expect(result?.paymentId).toBe('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');
    expect(result?.outcome).toBe('Success');
  });

  it('drops rows without receipt number or payment id', () => {
    expect(parsePosReceiptListItem({ receiptNumber: 'AT-1' })).toBeNull();
  });
});
