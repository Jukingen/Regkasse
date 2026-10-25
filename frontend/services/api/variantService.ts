import { apiClient } from './config';
import { API_PATHS } from './apiPaths';

export type ProductVariantDto = {
  id: string;
  productId: string;
  sku: string;
  variantLabel: string;
  priceDelta: number;
  stock: number;
  isActive: boolean;
};

function mapVariant(raw: Record<string, unknown>): ProductVariantDto {
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    productId: String(raw.productId ?? raw.ProductId ?? ''),
    sku: String(raw.sku ?? raw.Sku ?? ''),
    variantLabel: String(raw.variantLabel ?? raw.VariantLabel ?? ''),
    priceDelta: Number(raw.priceDelta ?? raw.PriceDelta ?? 0),
    stock: Number(raw.stock ?? raw.Stock ?? 0),
    isActive: Boolean(raw.isActive ?? raw.IsActive),
  };
}

export async function listProductVariants(productId: string): Promise<ProductVariantDto[]> {
  const payload = await apiClient.get<unknown>(API_PATHS.PRODUCT.VARIANTS(productId));
  const wrapped =
    payload !== null && typeof payload === 'object' ? (payload as { data?: unknown }).data : undefined;
  const rows = Array.isArray(payload) ? payload : Array.isArray(wrapped) ? wrapped : [];
  return rows.map((row: Record<string, unknown>) => mapVariant(row));
}

export function productRequiresVariantPicker(product: { hasVariants?: boolean | null }): boolean {
  return product.hasVariants === true;
}

/** Cashier-facing line label. The variant id stays off the receipt. */
export function variantCartLineLabel(productName: string, variantLabel: string): string {
  return `${productName} — ${variantLabel}`;
}
