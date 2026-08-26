/**
 * Client-only copy and helpers: voucher redemption requires server-side validation.
 */
import { isExpoPublicOfflineGutscheinEnabled } from './expoPublicEnv';

export const POS_VOUCHER_REQUIRES_ONLINE_MESSAGE_DE = 'Gutschein erfordert Online-Verbindung';

function isVoucherMethod(paymentMethod: string | null | undefined): boolean {
  return (paymentMethod ?? '').trim().toLowerCase() === 'voucher';
}

export function posOfflineBlocksVoucherByMethod(
  isOnline: boolean,
  paymentMethod: string | null | undefined
): boolean {
  if (isOnline || isExpoPublicOfflineGutscheinEnabled()) return false;
  return isVoucherMethod(paymentMethod);
}

/** Split-amount POS entry: any voucher share needs connectivity (unless Gutschein offline is explicitly enabled). */
export function posOfflineBlocksVoucherSplitEntry(
  isOnline: boolean,
  voucherAmountEntered: number
): boolean {
  if (isOnline || isExpoPublicOfflineGutscheinEnabled()) return false;
  return Number.isFinite(voucherAmountEntered) && voucherAmountEntered > 0;
}
