import { describe, expect, it } from '@jest/globals';

import type { PosDailyClosingStatusDto } from '../services/api/shiftService';
import { formatUserDate, formatUserDateTime } from '../utils/dateFormatter';
import {
  resolveAlreadyClosedDailyMessage,
  resolveDailyClosingStatusMessage,
} from '../utils/resolveDailyClosingStatusMessage';

const t = (key: string, options?: Record<string, unknown>) => {
  if (key === 'settings:shift.dailyClosing.statusCanClose') return 'CAN_CLOSE';
  if (key === 'settings:shift.dailyClosing.statusAlreadyClosedAt') {
    return `CLOSED_AT_${options?.dateTime}`;
  }
  if (key === 'settings:shift.dailyClosing.statusAlreadyClosedOnDate') {
    return `CLOSED_ON_${options?.date}`;
  }
  if (key === 'settings:shift.dailyClosing.statusAlreadyClosedToday') return 'CLOSED_TODAY';
  if (key === 'settings:shift.dailyClosing.statusPaymentsWithoutInvoice') {
    return `PAYMENTS_${options?.count}`;
  }
  if (key === 'settings:shift.dailyClosing.statusRegisterUnavailable')
    return 'REGISTER_UNAVAILABLE';
  if (key === 'settings:shift.dailyClosing.statusNoActiveShift') return 'NO_ACTIVE_SHIFT';
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

  it('includes date and time when lastClosingPerformedAt is available', () => {
    const lastClosingPerformedAt = '2026-07-11T12:30:00.000Z';
    expect(
      resolveDailyClosingStatusMessage(
        baseStatus({
          blockReason: 'already_closed_today',
          lastClosingPerformedAt,
        }),
        t
      )
    ).toBe(`CLOSED_AT_${formatUserDateTime(lastClosingPerformedAt)}`);
  });

  it('includes last closing date when performedAt is missing', () => {
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

  it('falls back to today wording without closing timestamps', () => {
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

  it('maps no active shift', () => {
    expect(
      resolveDailyClosingStatusMessage(
        baseStatus({
          blockReason: 'no_active_shift',
          hasActiveShift: false,
        }),
        t
      )
    ).toBe('NO_ACTIVE_SHIFT');
  });
});

describe('resolveAlreadyClosedDailyMessage', () => {
  it('prefers performedAt over closingDate anchor', () => {
    expect(
      resolveAlreadyClosedDailyMessage('2026-07-11T14:00:00.000Z', '2026-07-11T00:00:00.000Z', t)
    ).toBe(`CLOSED_AT_${formatUserDateTime('2026-07-11T14:00:00.000Z')}`);
  });
});
