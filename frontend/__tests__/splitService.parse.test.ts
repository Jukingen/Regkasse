import { describe, expect, it, jest } from '@jest/globals';

import { parseSplitItemDto, parseSplitSessionDto } from '../services/api/splitService';

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: jest.fn(),
    post: jest.fn(),
  },
}));

describe('splitService parsers', () => {
  it('parseSplitSessionDto reads items and totals', () => {
    const session = parseSplitSessionDto({
      id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      originalCartId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      originalCartKey: 'cart-key-1',
      tableNumber: 3,
      isCompleted: false,
      createdAt: '2026-06-11T12:00:00Z',
      grandTotal: 24,
      items: [
        {
          id: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
          productId: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
          productName: 'Pizza',
          quantity: 2,
          price: 12,
          lineTotal: 24,
          customerName: '',
          seatNumber: 0,
        },
      ],
    });

    expect(session?.id).toBe('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
    expect(session?.items).toHaveLength(1);
    expect(session?.items[0]?.productName).toBe('Pizza');
    expect(session?.grandTotal).toBe(24);
  });

  it('parseSplitItemDto returns null for invalid row', () => {
    expect(parseSplitItemDto({})).toBeNull();
  });
});
