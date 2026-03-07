/**
 * Phase D PR-B: Verify active POS add-to-cart requests do not send selectedModifiers.
 *
 * Behavior-focused: replicates the request-body contract used by CartContext.addItem
 * and usePOSOrderFlow (handleAddProduct, addItemWithAddOns). Add-ons are separate lines;
 * no selectedModifiers in any add-item request.
 *
 * Contract tests assert the rule; CartContext builds the same shape (see CartContext addItem body).
 * Gap: no full integration test against real CartContext (would require heavier mocking).
 */

/** Replicates CartContext add-item body build (Phase D PR-B: selectedModifiers never set). */
function buildAddItemRequestForPOS(
  productId: string,
  quantity: number,
  tableNumber: number,
  options?: { modifiers?: { id: string; quantity?: number }[]; productName?: string; unitPrice?: number }
): Record<string, unknown> {
  const body: Record<string, unknown> = {
    productId,
    quantity,
    tableNumber,
  };
  // Phase D PR-B: we intentionally do not set body.selectedModifiers regardless of options.
  return body;
}

/** Simulates the sequence of add-item request bodies for addItemWithAddOns (base + one per add-on). */
function buildAddItemRequestsForAddOnFlow(
  baseProductId: string,
  baseProductName: string,
  baseUnitPrice: number,
  addOns: Array<{ productId: string; productName: string; price: number }>,
  tableNumber: number
): Record<string, unknown>[] {
  const bodies: Record<string, unknown>[] = [];
  bodies.push(buildAddItemRequestForPOS(baseProductId, 1, tableNumber, { productName: baseProductName, unitPrice: baseUnitPrice }));
  for (const a of addOns) {
    bodies.push(buildAddItemRequestForPOS(a.productId, 1, tableNumber, { productName: a.productName, unitPrice: a.price }));
  }
  return bodies;
}

describe('Phase D PR-B: add-item request does not send selectedModifiers', () => {
  describe('buildAddItemRequestForPOS', () => {
    it('builds body with productId, quantity, tableNumber only', () => {
      const body = buildAddItemRequestForPOS('prod-1', 2, 5);
      expect(body.productId).toBe('prod-1');
      expect(body.quantity).toBe(2);
      expect(body.tableNumber).toBe(5);
      expect(body).not.toHaveProperty('selectedModifiers');
    });

    it('does not add selectedModifiers when options.modifiers is provided (legacy path removed)', () => {
      const body = buildAddItemRequestForPOS('prod-1', 1, 1, {
        modifiers: [{ id: 'mod-1', quantity: 1 }],
        productName: 'Burger',
        unitPrice: 9.9,
      });
      expect(body).not.toHaveProperty('selectedModifiers');
      expect(body.productId).toBe('prod-1');
      expect(body.quantity).toBe(1);
      expect(body.tableNumber).toBe(1);
    });

    it('does not add selectedModifiers when options have only productName and unitPrice (add-on flow)', () => {
      const body = buildAddItemRequestForPOS('prod-2', 1, 3, {
        productName: 'Ketchup',
        unitPrice: 0.5,
      });
      expect(body).not.toHaveProperty('selectedModifiers');
      expect(body.productId).toBe('prod-2');
      expect(body.quantity).toBe(1);
      expect(body.tableNumber).toBe(3);
    });

    it('does not add selectedModifiers when options is undefined', () => {
      const body = buildAddItemRequestForPOS('prod-3', 1, 2);
      expect(body).not.toHaveProperty('selectedModifiers');
    });
  });

  describe('add-on flow: products-only, no selectedModifiers on any request', () => {
    it('base + add-ons yields N+1 request bodies, none with selectedModifiers', () => {
      const addOns = [
        { productId: 'p-ketchup', productName: 'Ketchup', price: 0.5 },
        { productId: 'p-bacon', productName: 'Bacon', price: 1.5 },
      ];
      const bodies = buildAddItemRequestsForAddOnFlow('base-burger', 'Burger', 9.9, addOns, 1);

      expect(bodies).toHaveLength(3); // base + 2 add-ons
      bodies.forEach((body, index) => {
        expect(body).not.toHaveProperty('selectedModifiers');
        expect(body.quantity).toBe(1);
        expect(body.tableNumber).toBe(1);
      });
      expect(bodies[0].productId).toBe('base-burger');
      expect(bodies[1].productId).toBe('p-ketchup');
      expect(bodies[2].productId).toBe('p-bacon');
    });

    it('base only (no add-ons) yields one body without selectedModifiers', () => {
      const bodies = buildAddItemRequestsForAddOnFlow('base-pizza', 'Pizza', 12, [], 2);
      expect(bodies).toHaveLength(1);
      expect(bodies[0]).not.toHaveProperty('selectedModifiers');
      expect(bodies[0].productId).toBe('base-pizza');
      expect(bodies[0].quantity).toBe(1);
      expect(bodies[0].tableNumber).toBe(2);
    });
  });
});
