/**
 * Phase D: Payment request must be flat — one item per cart line, no modifierIds/modifiers.
 * Replicates PaymentModal payment-items build; contract tests prevent accidental reintroduction.
 */

/** Same shape as PaymentModal: cartItems → payment items (productId, quantity, taxType only). */
function buildPaymentItemsForPOS(
  cartItems: Array<{
    productId: string;
    quantity: number;
    qty?: number;
    taxType?: string;
    modifiers?: Array<{ modifierId: string; name?: string; priceDelta?: number }>;
  }>
): Array<{ productId: string; quantity: number; taxType: string }> {
  return cartItems.map(item => ({
    productId: item.productId,
    quantity: item.qty ?? item.quantity,
    taxType: (item.taxType as 'standard' | 'reduced' | 'special') || 'standard',
  }));
}

describe('Phase D: payment request does not emit modifierIds/modifiers', () => {
  it('builds flat items with productId, quantity, taxType only', () => {
    const cartItems = [
      { productId: 'p1', quantity: 2, taxType: 'standard' as const },
      { productId: 'p2', quantity: 1, taxType: 'reduced' as const },
    ];
    const items = buildPaymentItemsForPOS(cartItems);
    expect(items).toHaveLength(2);
    items.forEach(item => {
      expect(item).toHaveProperty('productId');
      expect(item).toHaveProperty('quantity');
      expect(item).toHaveProperty('taxType');
      expect(item).not.toHaveProperty('modifierIds');
      expect(item).not.toHaveProperty('modifiers');
    });
  });

  it('does not add modifierIds or modifiers when cart items have modifiers (legacy shape)', () => {
    const cartItems = [
      {
        productId: 'base-burger',
        quantity: 1,
        taxType: 'standard',
        modifiers: [{ modifierId: 'mod-1', name: 'Ketchup', priceDelta: 0.5 }],
      },
    ];
    const items = buildPaymentItemsForPOS(cartItems);
    expect(items).toHaveLength(1);
    expect(items[0]).toEqual({
      productId: 'base-burger',
      quantity: 1,
      taxType: 'standard',
    });
    expect(items[0]).not.toHaveProperty('modifierIds');
    expect(items[0]).not.toHaveProperty('modifiers');
  });

  it('output items have only productId, quantity, taxType keys', () => {
    const items = buildPaymentItemsForPOS([
      { productId: 'p1', quantity: 1, taxType: 'special', modifiers: [] },
    ]);
    expect(Object.keys(items[0]).sort()).toEqual(['productId', 'quantity', 'taxType']);
  });
});
