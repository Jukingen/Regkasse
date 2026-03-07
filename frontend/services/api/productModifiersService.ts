/**
 * POS: Ürün modifier grupları (Extra Zutaten). Cache ile gereksiz fetch önlenir.
 * Phase D PR-C: POS endpoints return modifier groups with empty modifiers; use .products only.
 */
import { apiClient } from './config';
import { API_PATHS } from './apiPaths';

/** Shape for group.modifiers. POS endpoints return empty array; type kept for DTO shape and admin compat. */
export interface ModifierDto {
  id: string;
  name: string;
  price: number;
  taxType: number;
  sortOrder: number;
}

/** Faz 1:  (sellable add-on). Fiyat/vergi Product’tan; sepette ayrı satır. */
/**
 * Add-on product inside a modifier group. Source of truth for POS add-on selection.
 * Each item = sellable product; tapping adds a separate cart line (flat cart).
 */
export interface AddOnGroupProductItemDto {
  productId: string;
  productName: string;
  price: number;
  taxType: number;
  sortOrder: number;
}

/** Alias for POS clarity: add-on product in a group. */
export type AddOnProduct = AddOnGroupProductItemDto;

/**
 * Selection payload when user picks an add-on product (e.g. chip tap).
 * Used by OnAddAddOn; leads to addItem(productId, 1, { productName, unitPrice }).
 */
export interface AddOnSelection {
  productId: string;
  productName: string;
  price: number;
}

/**
 * Modifier group from API. Phase D PR-C: POS endpoints (catalog, Product modifier-groups) return empty modifiers.
 * Use .products only for add-on UI. Admin endpoints still return .modifiers for legacy display/migration.
 */
export interface ModifierGroupDto {
  id: string;
  name: string;
  minSelections: number;
  maxSelections: number | null;
  isRequired: boolean;
  sortOrder: number;
  isActive: boolean;
  /** Add-on products in this group. Source of truth for POS. */
  products?: AddOnGroupProductItemDto[];
  /** Legacy modifier list. Empty from POS endpoints (Phase D PR-C); admin endpoints may still populate. Do not use for POS add-on display. */
  modifiers: ModifierDto[];
}

interface ApiResponse<T> {
  success?: boolean;
  data?: T;
}

const CACHE_TTL_MS = 5 * 60 * 1000; // 5 dakika
const modifierGroupsCache = new Map<string, { data: ModifierGroupDto[]; ts: number }>();

function getCached(productId: string): ModifierGroupDto[] | null {
  const entry = modifierGroupsCache.get(productId);
  if (!entry) return null;
  if (Date.now() - entry.ts > CACHE_TTL_MS) {
    modifierGroupsCache.delete(productId);
    return null;
  }
  return entry.data;
}

/** Normalize API group for POS. Phase D PR-C: use .products only for add-on options; .modifiers must not be used (guard). */
function mapGroupForPOS(g: any): ModifierGroupDto {
  return {
    id: g.Id ?? g.id,
    name: g.Name ?? g.name,
    minSelections: g.MinSelections ?? g.minSelections ?? 0,
    maxSelections: g.MaxSelections ?? g.maxSelections ?? undefined,
    isRequired: g.IsRequired ?? g.isRequired ?? false,
    sortOrder: g.SortOrder ?? g.sortOrder ?? 0,
    isActive: g.IsActive ?? g.isActive ?? true,
    modifiers: [],
    products: Array.isArray(g.Products ?? g.products)
      ? (g.Products ?? g.products).map((p: any) => ({
          productId: p.ProductId ?? p.productId,
          productName: p.ProductName ?? p.productName ?? '',
          price: Number(p.Price ?? p.price ?? 0),
          taxType: p.TaxType ?? p.taxType ?? 1,
          sortOrder: p.SortOrder ?? p.sortOrder ?? 0,
        }))
      : [],
  };
}

export async function getProductModifierGroups(productId: string): Promise<ModifierGroupDto[]> {
  const cached = getCached(productId);
  if (cached) return cached;

  const res = await apiClient.get<ApiResponse<ModifierGroupDto[]> | ModifierGroupDto[]>(
    API_PATHS.PRODUCT.MODIFIER_GROUPS(productId)
  );
  const body = (res as any)?.data ?? res;
  const raw = Array.isArray(body) ? body : (body?.data ?? []);
  const list = raw.map((g: any) => mapGroupForPOS(g));
  modifierGroupsCache.set(productId, { data: list, ts: Date.now() });
  return list;
}
