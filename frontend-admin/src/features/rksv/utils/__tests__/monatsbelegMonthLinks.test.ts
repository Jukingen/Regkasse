import { describe, expect, it } from 'vitest';

import {
  buildMonatsbelegMonthDeepLink,
  buildMonthPaymentsHref,
  buildMonthReceiptsHref,
  monthDateRangeIso,
} from '@/features/rksv/utils/monatsbelegMonthLinks';

describe('monatsbelegMonthLinks', () => {
  it('builds inclusive calendar month date ranges', () => {
    expect(monthDateRangeIso(2026, 2)).toEqual({ from: '2026-02-01', to: '2026-02-28' });
    expect(monthDateRangeIso(2024, 2)).toEqual({ from: '2024-02-01', to: '2024-02-29' });
    expect(monthDateRangeIso(2026, 7)).toEqual({ from: '2026-07-01', to: '2026-07-31' });
  });

  it('builds deep links with focus and period', () => {
    expect(
      buildMonatsbelegMonthDeepLink({
        origin: 'https://admin.example',
        registerId: 'reg-1',
        year: 2026,
        month: 3,
      })
    ).toBe(
      'https://admin.example/rksv/sonderbelege?focus=monatsbeleg&year=2026&month=3&registerId=reg-1'
    );
  });

  it('builds receipts and payments filter hrefs', () => {
    expect(buildMonthReceiptsHref('reg-1', 2026, 3)).toBe(
      '/receipts?cashRegisterId=reg-1&issuedFrom=2026-03-01&issuedTo=2026-03-31'
    );
    expect(buildMonthPaymentsHref('reg-1', 2026, 3)).toBe(
      '/payments?cashRegisterId=reg-1&startDate=2026-03-01&endDate=2026-03-31'
    );
  });
});
