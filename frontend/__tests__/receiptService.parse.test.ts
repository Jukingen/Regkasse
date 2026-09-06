import { describe, expect, it, jest } from '@jest/globals';

import { apiClient } from '../services/api/config';
import {
  cancelReceipt,
  fetchRecentReceipts,
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
      status: 'Paid',
    });
    expect(item?.cashierId).toBeUndefined();
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
    expect(rows[0].status).toBe('Paid');
  });

  it('parsePosReceiptListItem reads status from list row', () => {
    const item = parsePosReceiptListItem({
      receiptId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      paymentId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      receiptNumber: 'AT-STO',
      issuedAt: '2026-08-28T08:00:00Z',
      grandTotal: -12.5,
      cashRegisterEntityId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      status: 'Storno',
    });
    expect(item?.status).toBe('Storno');
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

  it('parsePosReceiptListItem reads cashierId', () => {
    const item = parsePosReceiptListItem({
      receiptId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      paymentId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      receiptNumber: 'AT-1',
      issuedAt: '2026-08-28T08:00:00Z',
      grandTotal: 12.5,
      cashRegisterEntityId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      cashierId: 'cashier-1',
    });
    expect(item?.cashierId).toBe('cashier-1');
  });
});

describe('fetchRecentReceipts', () => {
  it('GETs /pos/receipts/recent with cashRegisterId and limit', async () => {
    const get = apiClient.get as jest.MockedFunction<typeof apiClient.get>;
    get.mockResolvedValueOnce({ items: [] });
    await fetchRecentReceipts({
      cashRegisterId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      pageSize: 20,
    });
    expect(get).toHaveBeenCalledWith('/pos/receipts/recent', {
      params: {
        cashRegisterId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
        limit: 20,
      },
    });
  });
});

describe('cancelReceipt', () => {
  it('POSTs /pos/receipts/{id}/cancel with cashRegisterId and optional reason', async () => {
    const post = apiClient.post as jest.MockedFunction<typeof apiClient.post>;
    post.mockResolvedValueOnce({ success: true, stornoPaymentId: 'dddddddd-dddd-dddd-dddd-dddddddddddd' });
    const result = await cancelReceipt({
      receiptId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      cashRegisterId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
      reason: 'Falscher Betrag',
    });
    expect(post).toHaveBeenCalledWith(
      '/pos/receipts/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/cancel',
      { reason: 'Falscher Betrag' },
      { params: { cashRegisterId: 'cccccccc-cccc-cccc-cccc-cccccccccccc' } }
    );
    expect(result.success).toBe(true);
    expect(result.stornoPaymentId).toBe('dddddddd-dddd-dddd-dddd-dddddddddddd');
  });
});
