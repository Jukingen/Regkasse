/**
 * Client-only copy and helpers: voucher redemption requires server-side validation.
 */

export const POS_VOUCHER_REQUIRES_ONLINE_MESSAGE_DE = 'Gutschein erfordert Online-Verbindung';

export function posOfflineBlocksVoucherByMethod(
  isOnline: boolean,
  paymentMethod: string | null | undefined
): boolean {
  return !isOnline && paymentMethod === 'voucher';
}

/** Split-amount POS entry: any voucher share needs connectivity. */
export function posOfflineBlocksVoucherSplitEntry(
  isOnline: boolean,
  voucherAmountEntered: number
): boolean {
  return !isOnline && Number.isFinite(voucherAmountEntered) && voucherAmountEntered > 0;
}
