/** Cart gross tolerance for voucher + cash coverage (aligned with backend money rules). */
export const PAYMENT_COVERAGE_TOLERANCE_EUR = 0.02;

export function parsePosLocaleDecimal(input: string): number {
  const s = input.trim().replace(',', '.');
  if (!s) return NaN;
  return parseFloat(s);
}

export type OptionalCashTender = {
  fieldEmpty: boolean;
  parsed: number;
  /** Empty field → exact settlement (no change). */
  effectiveTender: number;
  isInsufficient: boolean;
};

/** Optional Bar tender: empty means exact payment; a typed value must cover the rest. */
export function resolveOptionalCashTender(
  amountReceivedStr: string,
  settlementAmountDue: number
): OptionalCashTender {
  const fieldEmpty = amountReceivedStr.trim() === '';
  if (fieldEmpty) {
    return {
      fieldEmpty: true,
      parsed: Number.NaN,
      effectiveTender: Math.max(0, settlementAmountDue),
      isInsufficient: false,
    };
  }

  const parsed = parsePosLocaleDecimal(amountReceivedStr);
  const isInsufficient = !Number.isFinite(parsed) || parsed + 0.001 < settlementAmountDue;
  return {
    fieldEmpty: false,
    parsed: Number.isFinite(parsed) ? parsed : Number.NaN,
    effectiveTender: Number.isFinite(parsed) ? Math.max(0, parsed) : 0,
    isInsufficient,
  };
}

/**
 * Whether applied voucher EUR plus cash tender covers cart gross total (German decimal input).
 * When settlement Restbetrag is ~0, only the voucher portion must cover the cart total.
 * Empty cash received is treated as exact payment of the remaining amount.
 */
export function computeVoucherPlusCashCoversTotal(input: {
  voucherEnabled: boolean;
  appliedVoucherAmount: number;
  totalCartAmount: number;
  settlementAmountDue: number;
  requiresCashAmount: boolean;
  amountReceivedStr: string;
}): { sumPaid: number; coversTotal: boolean } {
  const v = input.voucherEnabled ? Math.max(0, input.appliedVoucherAmount) : 0;
  const cash = resolveOptionalCashTender(input.amountReceivedStr, input.settlementAmountDue);

  let sumPaid: number;
  if (!input.voucherEnabled) {
    sumPaid = input.requiresCashAmount ? cash.effectiveTender : input.totalCartAmount;
  } else if (input.settlementAmountDue <= PAYMENT_COVERAGE_TOLERANCE_EUR) {
    sumPaid = v;
  } else if (input.requiresCashAmount) {
    sumPaid = v + cash.effectiveTender;
  } else {
    sumPaid = v + Math.max(0, input.settlementAmountDue);
  }

  return {
    sumPaid,
    coversTotal: sumPaid >= input.totalCartAmount - PAYMENT_COVERAGE_TOLERANCE_EUR,
  };
}
