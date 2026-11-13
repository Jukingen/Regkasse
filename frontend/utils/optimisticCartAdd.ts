import type { Cart, CartItem, CartItemModifier } from '../contexts/CartContext';

export type OptimisticCartAddInput = {
  productId: string;
  quantity?: number;
  modifiers?: CartItemModifier[];
  productName?: string;
  unitPrice?: number;
  variantId?: string;
  now?: number;
  clientId?: string;
};

function modifierKey(modifiers?: CartItemModifier[]): string {
  if (!modifiers?.length) return '';
  return [...modifiers]
    .sort((a, b) => a.id.localeCompare(b.id))
    .map((modifier) => `${modifier.id}:${modifier.quantity ?? 1}`)
    .join(',');
}

/**
 * Immediate cart shown before POST /pos/cart/add-item returns.
 * Does not mutate `current`, so a failed request can restore that snapshot.
 */
export function applyOptimisticCartAdd(current: Cart, input: OptimisticCartAddInput): Cart {
  const quantity = input.quantity ?? 1;
  const modifiers = input.modifiers ?? [];
  const key = modifierKey(modifiers);
  const variantKey = input.variantId ?? '';
  const existingIndex = current.items.findIndex(
    (item) =>
      item.productId === input.productId &&
      modifierKey(item.modifiers) === key &&
      (item.variantId ?? '') === variantKey
  );

  if (existingIndex !== -1) {
    const item = current.items[existingIndex];
    const newQty = item.qty + quantity;
    const modifierTotal = (item.modifiers ?? []).reduce(
      (sum, modifier) => sum + modifier.price * (modifier.quantity ?? 1),
      0
    );
    const newTotal = (item.unitPrice ?? item.price ?? 0) * newQty + modifierTotal;
    return {
      ...current,
      items: current.items.map((line, index) =>
        index === existingIndex ? { ...line, qty: newQty, totalPrice: newTotal } : line
      ),
      updatedAt: input.now ?? Date.now(),
    };
  }

  const unitPrice = input.unitPrice ?? 0;
  const modifierTotal = modifiers.reduce(
    (sum, modifier) => sum + modifier.price * modifier.quantity,
    0
  );
  const added: CartItem = {
    productId: input.productId,
    productName: input.productName ?? 'Loading...',
    price: unitPrice,
    qty: quantity,
    unitPrice,
    totalPrice: unitPrice * quantity + modifierTotal,
    modifiers: modifiers.length ? modifiers : undefined,
    variantId: input.variantId,
    itemId: undefined,
    clientId: input.clientId ?? `ci-${Date.now()}`,
  };

  return {
    ...current,
    items: [...current.items, added],
    updatedAt: input.now ?? Date.now(),
  };
}
