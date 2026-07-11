import { describe, expect, it } from '@jest/globals';

import { resolveDailyClosingStatusMessage } from '../utils/resolveDailyClosingStatusMessage';
import type { PosDailyClosingStatusDto } from '../services/api/shiftService';
import { formatUserDate } from '../utils/dateFormatter';

const t = (key: string, options?: Record<string, unknown>) => {
  if (key === 'settings:shift.dailyClosing.statusCanClose') return 'CAN_CLOSE';
  if (key === 'settings:shift.dailyClosing.statusAlreadyClosedOnDate') {
    return `CLOSED_ON_${options?.date}`;
  }
  if (key === 'settings:shift.dailyClosing.statusAlreadyClosedToday') return 'CLOSED_TODAY';
  if (key === 'settings:shift.dailyClosing.statusPaymentsWithoutInvoice') {
    return `PAYMENTS_${options?.count}`;
  }
  if (key === 'settings:shift.dailyClosing.statusRegisterUnavailable') return 'REGISTER_UNAVAILABLE';
  return 'BLOCKED';
};

function baseStatus(overrides: Partial<PosDailyClosingStatusDto> = {}): PosDailyClosingStatusDto {
  return {
    canClose: false,
    hasActiveShift: true,
    message: '',
    paymentsWithoutInvoiceCount: 0,
    ...overrides,
  };
}

describe('resolveDailyClosingStatusMessage', () => {
  it('returns can-close message when closing is allowed', () => {
    expect(resolveDailyClosingStatusMessage(baseStatus({ canClose: true }), t)).toBe('CAN_CLOSE');
  });

  it('includes last closing date when already closed today', () => {
    const lastClosingDate = '2026-07-11T10:00:00.000Z';
    expect(
      resolveDailyClosingStatusMessage(
        baseStatus({
          blockReason: 'already_closed_today',
          lastClosingDate,
        }),
        t
      )
    ).toBe(`CLOSED_ON_${formatUserDate(lastClosingDate)}`);
  });

  it('falls back to today wording without lastClosingDate', () => {
    expect(
      resolveDailyClosingStatusMessage(
        baseStatus({ blockReason: 'already_closed_today', lastClosingDate: null }),
        t
      )
    ).toBe('CLOSED_TODAY');
  });

  it('maps payments-without-invoice count', () => {
    expect(
      resolveDailyClosingStatusMessage(
        baseStatus({
          blockReason: 'payments_without_invoice',
          paymentsWithoutInvoiceCount: 3,
        }),
        t
      )
    ).toBe('PAYMENTS_3');
  });
});
