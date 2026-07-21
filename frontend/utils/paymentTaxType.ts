/**
 * Maps cart line tax values from the POS cart API (numeric TaxType or legacy strings)
 * to POST /api/pos/payment PaymentItem.taxType.
 * Backend: Product.TaxTypes Standard=1, Reduced=2, Special=3, ZeroRate=4.
 */
export function normalizeCartLineTaxTypeForPayment(
  raw: string | number | undefined | null
): 'standard' | 'reduced' | 'special' {
  if (typeof raw === 'number' && !Number.isNaN(raw)) {
    if (raw === 2) return 'reduced';
    if (raw === 3) return 'special';
    return 'standard';
  }
  const r = String(raw ?? '')
    .trim()
    .toLowerCase();
  if (r === 'reduced' || r === '10') return 'reduced';
  if (r === 'special' || r === '13') return 'special';
  return 'standard';
}

/** One payment line before POST /api/pos/payment (taxType may be cart/API shaped). */
export type PosPaymentItemTaxInput = {
  productId: string;
  quantity: number;
  taxType?: string | number | null;
};

/**
 * Canonical item normalization for POS payment: call from paymentService.processPayment
 * so HTTP and offline queue always see enum strings.
 */
export function normalizePosPaymentItemsForRequest(
  items: readonly PosPaymentItemTaxInput[]
): { productId: string; quantity: number; taxType: 'standard' | 'reduced' | 'special' }[] {
  return items.map((item) => ({
    productId: item.productId,
    quantity: item.quantity,
    taxType: normalizeCartLineTaxTypeForPayment(item.taxType),
  }));
}
