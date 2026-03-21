/**
 * POS payment requires a non-empty cash register GUID (backend PaymentService.CreatePaymentAsync).
 */
const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';

export function isValidPosCashRegisterId(id: string | null | undefined): boolean {
  const t = String(id ?? '').trim();
  return t !== '' && t.toLowerCase() !== EMPTY_GUID;
}
