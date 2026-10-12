export type StayCarrier = {
  id: string;
  isActive?: boolean;
  imeiTracked?: boolean;
  categoryId?: string | null;
  stockQuantity?: number;
  taxType?: string;
};

/** Prefer a lodging (13%) product that can carry the folio gross into the existing RKSV sale. */
export function pickStayCarrier<T extends StayCarrier>(products: T[]): T | null {
  const suitable = products.filter(
    (product) => product.isActive !== false && !product.imeiTracked && Boolean(product.categoryId) && product.id
  );
  const withStock = suitable.filter((product) => (product.stockQuantity ?? 0) > 0);
  const pool = withStock.length > 0 ? withStock : suitable;
  return (
    pool.find((product) => product.taxType === 'Special') ??
    pool.find((product) => product.taxType === 'Reduced') ??
    pool[0] ??
    null
  );
}
