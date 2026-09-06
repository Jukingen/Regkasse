import { describe, expect, it } from '@jest/globals';

import {
  computeVoucherPlusCashCoversTotal,
  resolveOptionalCashTender,
} from '../utils/posPaymentCoverage';

describe('resolveOptionalCashTender', () => {
  it('treats empty field as exact settlement', () => {
    const tender = resolveOptionalCashTender('', 15.5);
    expect(tender.fieldEmpty).toBe(true);
    expect(tender.effectiveTender).toBe(15.5);
    expect(tender.isInsufficient).toBe(false);
  });

  it('flags a typed amount below the due amount', () => {
    const tender = resolveOptionalCashTender('10', 15.5);
    expect(tender.fieldEmpty).toBe(false);
    expect(tender.isInsufficient).toBe(true);
    expect(tender.effectiveTender).toBe(10);
  });

  it('accepts a typed amount at or above the due amount', () => {
    expect(resolveOptionalCashTender('15,50', 15.5).isInsufficient).toBe(false);
    expect(resolveOptionalCashTender('20', 15.5).effectiveTender).toBe(20);
  });
});

describe('computeVoucherPlusCashCoversTotal — optional cash', () => {
  it('covers the cart when cash received is empty (exact Bar)', () => {
    const coverage = computeVoucherPlusCashCoversTotal({
      voucherEnabled: false,
      appliedVoucherAmount: 0,
      totalCartAmount: 50,
      settlementAmountDue: 50,
      requiresCashAmount: true,
      amountReceivedStr: '',
    });
    expect(coverage.sumPaid).toBe(50);
    expect(coverage.coversTotal).toBe(true);
  });

  it('does not cover when typed cash is below the total', () => {
    const coverage = computeVoucherPlusCashCoversTotal({
      voucherEnabled: false,
      appliedVoucherAmount: 0,
      totalCartAmount: 50,
      settlementAmountDue: 50,
      requiresCashAmount: true,
      amountReceivedStr: '20',
    });
    expect(coverage.coversTotal).toBe(false);
  });

  it('covers voucher rest with empty cash received', () => {
    const coverage = computeVoucherPlusCashCoversTotal({
      voucherEnabled: true,
      appliedVoucherAmount: 30,
      totalCartAmount: 50,
      settlementAmountDue: 20,
      requiresCashAmount: true,
      amountReceivedStr: '',
    });
    expect(coverage.sumPaid).toBe(50);
    expect(coverage.coversTotal).toBe(true);
  });
});
