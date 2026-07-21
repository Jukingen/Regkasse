/**
 * Mirrors PaymentModal.tsx voucher/coverage/total gates (see comments there).
 * Keep in sync when changing PAYMENT_COVERAGE_TOLERANCE_EUR, totalAmount IIFE,
 * voucherSettlementValid, or handlePayment validateAmount bypass.
 */
import { describe, expect, it } from '@jest/globals';

import { validateAmount } from '../utils/validation';

const PAYMENT_COVERAGE_TOLERANCE_EUR = 0.02;

function parseLocaleDecimal(input: string): number {
  const s = input.trim().replace(',', '.');
  if (!s) return NaN;
  return parseFloat(s);
}

/** Same logic as PaymentModal `computeVoucherPlusCashCoversTotal`. */
function computeVoucherPlusCashCoversTotal(input: {
  voucherEnabled: boolean;
  appliedVoucherAmount: number;
  totalCartAmount: number;
  settlementAmountDue: number;
  requiresCashAmount: boolean;
  amountReceivedStr: string;
}): { sumPaid: number; coversTotal: boolean } {
  const v = input.voucherEnabled ? Math.max(0, input.appliedVoucherAmount) : 0;
  const cashParsed = parseLocaleDecimal(input.amountReceivedStr);
  const cashReceived = Number.isFinite(cashParsed) ? Math.max(0, cashParsed) : 0;

  let sumPaid: number;
  if (!input.voucherEnabled) {
    sumPaid = input.requiresCashAmount ? cashReceived : input.totalCartAmount;
  } else if (input.settlementAmountDue <= PAYMENT_COVERAGE_TOLERANCE_EUR) {
    sumPaid = v;
  } else if (input.requiresCashAmount) {
    sumPaid = v + cashReceived;
  } else {
    sumPaid = v + Math.max(0, input.settlementAmountDue);
  }

  return {
    sumPaid,
    coversTotal: sumPaid >= input.totalCartAmount - PAYMENT_COVERAGE_TOLERANCE_EUR,
  };
}

/** Same logic as PaymentModal `totalAmount` IIFE (grandTotalGross + cartLineSumGross). */
function resolveTotalAmountLikePaymentModal(
  grandTotalGross: number | undefined | null,
  cartLineSumGross: number
): number {
  if (grandTotalGross != null && Number.isFinite(grandTotalGross)) {
    if (grandTotalGross > 0) return grandTotalGross;
    if (grandTotalGross === 0 && cartLineSumGross <= PAYMENT_COVERAGE_TOLERANCE_EUR) return 0;
    if (grandTotalGross === 0 && cartLineSumGross > PAYMENT_COVERAGE_TOLERANCE_EUR)
      return cartLineSumGross;
  }
  return cartLineSumGross;
}

/** Same logic as PaymentModal `voucherSettlementValid`. */
function voucherSettlementValidLikePaymentModal(input: {
  voucherEnabled: boolean;
  voucherSnapshotPresent: boolean;
  voucherCodeMatchesValidated: boolean;
  totalAmount: number;
  voucherRedeemParsed: number;
  voucherRedeemAmountEffective: number;
}): boolean {
  const voucherCodeMatchesValidated =
    input.voucherSnapshotPresent && input.voucherCodeMatchesValidated;

  if (!input.voucherEnabled) return true;

  return (
    voucherCodeMatchesValidated &&
    (input.totalAmount <= PAYMENT_COVERAGE_TOLERANCE_EUR ||
      (Number.isFinite(input.voucherRedeemParsed) &&
        input.voucherRedeemParsed > 0 &&
        input.voucherRedeemAmountEffective > 0))
  );
}

/** Same logic as PaymentModal `handlePayment` validateAmount gate (+ optional voucher €0 bypass). */
function passesFiscalTotalGateLikePaymentModal(input: {
  totalAmount: number;
  voucherEnabled: boolean;
  voucherSettlementValid: boolean;
}): boolean {
  const allowZeroTotalWithValidatedVoucher =
    input.voucherEnabled &&
    input.voucherSettlementValid &&
    input.totalAmount <= PAYMENT_COVERAGE_TOLERANCE_EUR;

  return validateAmount(input.totalAmount) || allowZeroTotalWithValidatedVoucher;
}

describe('PaymentModal voucher / settlement mirrors (fix regression)', () => {
  it('1) Cart 50 €, Gutschein 50 € → Restbetrag 0, coverage OK, settlement valid (card settlement)', () => {
    const totalAmount = 50;
    const voucherRedeemAmountEffective = 50;
    const settlementAmountDue = Math.max(0, totalAmount - voucherRedeemAmountEffective);

    expect(settlementAmountDue).toBe(0);

    const coverage = computeVoucherPlusCashCoversTotal({
      voucherEnabled: true,
      appliedVoucherAmount: voucherRedeemAmountEffective,
      totalCartAmount: totalAmount,
      settlementAmountDue,
      requiresCashAmount: false,
      amountReceivedStr: '',
    });
    expect(coverage.coversTotal).toBe(true);
    expect(coverage.sumPaid).toBe(50);

    const settlementValid = voucherSettlementValidLikePaymentModal({
      voucherEnabled: true,
      voucherSnapshotPresent: true,
      voucherCodeMatchesValidated: true,
      totalAmount,
      voucherRedeemParsed: 50,
      voucherRedeemAmountEffective: 50,
    });
    expect(settlementValid).toBe(true);

    expect(
      passesFiscalTotalGateLikePaymentModal({
        totalAmount,
        voucherEnabled: true,
        voucherSettlementValid: settlementValid,
      })
    ).toBe(true);
  });

  it('2) Cart 50 €, Gutschein 30 € → Restbetrag 20 €, coverage OK with card (30 + 20)', () => {
    const totalAmount = 50;
    const voucherRedeemAmountEffective = 30;
    const settlementAmountDue = Math.max(0, totalAmount - voucherRedeemAmountEffective);

    expect(settlementAmountDue).toBe(20);

    const coverage = computeVoucherPlusCashCoversTotal({
      voucherEnabled: true,
      appliedVoucherAmount: voucherRedeemAmountEffective,
      totalCartAmount: totalAmount,
      settlementAmountDue,
      requiresCashAmount: false,
      amountReceivedStr: '',
    });
    expect(coverage.sumPaid).toBeCloseTo(50, 5);
    expect(coverage.coversTotal).toBe(true);

    const settlementValid = voucherSettlementValidLikePaymentModal({
      voucherEnabled: true,
      voucherSnapshotPresent: true,
      voucherCodeMatchesValidated: true,
      totalAmount,
      voucherRedeemParsed: 30,
      voucherRedeemAmountEffective: 30,
    });
    expect(settlementValid).toBe(true);
  });

  it('3) Cart 50 €, no Gutschein → total 50 €, coverage OK for non-cash (card) path', () => {
    const totalAmount = 50;

    const coverage = computeVoucherPlusCashCoversTotal({
      voucherEnabled: false,
      appliedVoucherAmount: 0,
      totalCartAmount: totalAmount,
      settlementAmountDue: totalAmount,
      requiresCashAmount: false,
      amountReceivedStr: '',
    });
    expect(coverage.sumPaid).toBe(50);
    expect(coverage.coversTotal).toBe(true);

    expect(
      passesFiscalTotalGateLikePaymentModal({
        totalAmount,
        voucherEnabled: false,
        voucherSettlementValid: true,
      })
    ).toBe(true);
  });

  it('4) Cart total 0 € without Gutschein → fiscal total gate fails (no validateAmount bypass)', () => {
    const totalAmount = resolveTotalAmountLikePaymentModal(0, 0);

    expect(totalAmount).toBe(0);

    const settlementValidWhenVoucherOff = voucherSettlementValidLikePaymentModal({
      voucherEnabled: false,
      voucherSnapshotPresent: false,
      voucherCodeMatchesValidated: false,
      totalAmount,
      voucherRedeemParsed: NaN,
      voucherRedeemAmountEffective: 0,
    });
    expect(settlementValidWhenVoucherOff).toBe(true);

    expect(
      passesFiscalTotalGateLikePaymentModal({
        totalAmount,
        voucherEnabled: false,
        voucherSettlementValid: settlementValidWhenVoucherOff,
      })
    ).toBe(false);
  });

  it('4b) €0 cart with validated Gutschein only → fiscal gate allows (Prüfen path)', () => {
    const totalAmount = 0;
    const settlementValid = voucherSettlementValidLikePaymentModal({
      voucherEnabled: true,
      voucherSnapshotPresent: true,
      voucherCodeMatchesValidated: true,
      totalAmount,
      voucherRedeemParsed: NaN,
      voucherRedeemAmountEffective: 0,
    });
    expect(settlementValid).toBe(true);

    expect(
      passesFiscalTotalGateLikePaymentModal({
        totalAmount,
        voucherEnabled: true,
        voucherSettlementValid: settlementValid,
      })
    ).toBe(true);
  });
});
