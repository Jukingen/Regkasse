/**
 * POS fiscal signing flag for POST /api/pos/payment.
 *
 * Source of truth: GET /api/pos/tse/status `requiresFiscalSignature`
 * (backend `Tse:TseMode` not Off). Health (Active/Inactive) and RKSV demo
 * labels must not skip signing. Fail closed when the field is missing.
 *
 * Do not use `__DEV__`, local simulation toggles, or `isDemoFiscal`.
 */
export function shouldRequireTseForPosPayment(
  requiresFiscalSignature: boolean | null | undefined
): boolean {
  return requiresFiscalSignature !== false;
}
