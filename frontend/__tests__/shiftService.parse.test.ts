import { describe, expect, it, jest } from '@jest/globals';

jest.mock('../services/api/config', () => ({
  apiClient: {
    get: jest.fn(),
    post: jest.fn(),
  },
  API_BASE_URL: 'http://test/api',
  resolveTenantFetchHeaders: jest.fn(async (headers: Record<string, string>) => headers),
}));

jest.mock('../services/session/sessionManager', () => ({
  sessionManager: {
    getAccessToken: jest.fn(async () => 'token'),
  },
}));

import {
  parseCashierShiftDto,
  parseCurrentShiftResponse,
  parseEndShiftResponse,
  parsePosDailyClosingResult,
  parsePosDailyClosingStatus,
} from '../services/api/shiftService';

describe('shiftService parsers', () => {
  it('parseCurrentShiftResponse reads camelCase active shift', () => {
    const res = parseCurrentShiftResponse({
      hasActiveShift: true,
      shift: {
        id: '11111111-1111-1111-1111-111111111111',
        cashRegisterId: '22222222-2222-2222-2222-222222222222',
        cashierId: 'u1',
        cashierName: 'Max',
        startBalance: 100,
        totalSales: 0,
        status: 'Active',
        startedAt: '2026-06-11T10:00:00Z',
      },
    });
    expect(res.hasActiveShift).toBe(true);
    expect(res.shift?.cashierName).toBe('Max');
    expect(res.shift?.startBalance).toBe(100);
  });

  it('parseCurrentShiftResponse handles empty shift', () => {
    const res = parseCurrentShiftResponse({ hasActiveShift: false });
    expect(res.hasActiveShift).toBe(false);
    expect(res.shift ?? null).toBeNull();
  });

  it('parseCashierShiftDto returns null without id', () => {
    expect(parseCashierShiftDto({ startBalance: 1 })).toBeNull();
  });

  it('parsePosDailyClosingStatus reads canClose and blockReason', () => {
    const res = parsePosDailyClosingStatus({
      canClose: false,
      hasActiveShift: true,
      message: 'Daily closing already performed for today',
      blockReason: 'already_closed_today',
      paymentsWithoutInvoiceCount: 0,
    });
    expect(res.canClose).toBe(false);
    expect(res.hasActiveShift).toBe(true);
    expect(res.blockReason).toBe('already_closed_today');
  });

  it('parsePosDailyClosingResult maps report', () => {
    const res = parsePosDailyClosingResult({
      success: true,
      dailyClosingId: 'dc-1',
      report: {
        businessDate: '2026-06-11',
        totalSales: 100,
        cashCount: 50,
        difference: 0,
        fiscalTotalAmount: 100,
        fiscalTransactionCount: 3,
      },
    });
    expect(res.success).toBe(true);
    expect(res.report?.fiscalTransactionCount).toBe(3);
  });

  it('parseEndShiftResponse maps receipt', () => {
    const res = parseEndShiftResponse({
      shift: {
        id: '11111111-1111-1111-1111-111111111111',
        cashRegisterId: '22222222-2222-2222-2222-222222222222',
        cashierId: 'u1',
        cashierName: 'Max',
        startBalance: 100,
        endBalance: 130,
        totalSales: 50,
        totalCash: 30,
        totalCard: 20,
        difference: 0,
        status: 'Completed',
        startedAt: '2026-06-11T10:00:00Z',
        endedAt: '2026-06-11T18:00:00Z',
      },
      receipt: {
        shiftId: '11111111-1111-1111-1111-111111111111',
        registerNumber: 'K1',
        totalSales: 50,
        difference: 0,
        status: 'Completed',
        endedAt: '2026-06-11T18:00:00Z',
      },
    });
    expect(res?.receipt.registerNumber).toBe('K1');
    expect(res?.shift.totalSales).toBe(50);
  });
});
