import { beforeEach, describe, expect, it, jest } from '@jest/globals';

import { apiClient } from '../services/api/config';
import {
  autoOpenShiftApi,
  parseCashierShiftDto,
  parseCurrentShiftResponse,
  parseEndShiftResponse,
  parsePosDailyClosingResult,
  parsePosDailyClosingStatus,
  parseShiftAutoOpenResult,
} from '../services/api/shiftService';
import { SHIFT_AUTO_OPEN_CODES } from '../utils/shiftAutoOpenError';

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

describe('autoOpenShiftApi', () => {
  const postMock = apiClient.post as jest.Mock;

  beforeEach(() => {
    postMock.mockReset();
  });

  it('POSTs empty body when cash register id is missing or empty GUID', async () => {
    postMock.mockResolvedValue({
      success: false,
      code: SHIFT_AUTO_OPEN_CODES.NEED_REGISTER_SELECTION,
      message: 'Bitte wählen Sie eine Kasse aus, bevor Sie fortfahren.',
    });
    await expect(autoOpenShiftApi('')).rejects.toMatchObject({
      name: 'ShiftAutoOpenError',
      code: SHIFT_AUTO_OPEN_CODES.NEED_REGISTER_SELECTION,
    });
    await expect(autoOpenShiftApi('00000000-0000-0000-0000-000000000000')).rejects.toMatchObject({
      code: SHIFT_AUTO_OPEN_CODES.NEED_REGISTER_SELECTION,
    });
    expect(postMock).toHaveBeenCalledWith('/pos/shift/auto-open', {});
  });

  it('returns shift from wrapped ShiftAutoOpenResult', async () => {
    postMock.mockResolvedValue({
      success: true,
      code: 'OK',
      data: {
        id: '11111111-1111-1111-1111-111111111111',
        cashRegisterId: '22222222-2222-2222-2222-222222222222',
        cashierId: 'u1',
        cashierName: 'Max',
        startBalance: 10,
        totalSales: 0,
        status: 'Active',
        startedAt: '2026-08-21T08:00:00Z',
      },
    });
    const shift = await autoOpenShiftApi('22222222-2222-2222-2222-222222222222');
    expect(shift.id).toBe('11111111-1111-1111-1111-111111111111');
    expect(postMock).toHaveBeenCalledWith('/pos/shift/auto-open', {
      cashRegisterId: '22222222-2222-2222-2222-222222222222',
    });
  });
});

describe('parseShiftAutoOpenResult', () => {
  it('reads structured success payload', () => {
    const res = parseShiftAutoOpenResult({
      success: true,
      code: 'SHIFT_ALREADY_OPEN',
      message: 'Die Schicht ist bereits geöffnet.',
      data: {
        id: '11111111-1111-1111-1111-111111111111',
        cashRegisterId: '22222222-2222-2222-2222-222222222222',
        cashierId: 'u1',
        cashierName: 'Max',
        status: 'Active',
        startedAt: '2026-08-21T08:00:00Z',
      },
    });
    expect(res.success).toBe(true);
    expect(res.code).toBe('SHIFT_ALREADY_OPEN');
    expect(res.shift?.cashierName).toBe('Max');
  });

  it('still parses a legacy CashierShiftDto at the root', () => {
    const res = parseShiftAutoOpenResult({
      id: '11111111-1111-1111-1111-111111111111',
      cashRegisterId: '22222222-2222-2222-2222-222222222222',
      cashierId: 'u1',
      cashierName: 'Max',
      status: 'Active',
      startedAt: '2026-08-21T08:00:00Z',
    });
    expect(res.success).toBe(true);
    expect(res.shift?.id).toBe('11111111-1111-1111-1111-111111111111');
  });
});
