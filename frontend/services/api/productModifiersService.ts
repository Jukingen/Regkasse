/**
 * POS: Ürün modifier grupları (Extra Zutaten). Cache ile gereksiz fetch önlenir.
 *
 * LEGACY: group.modifiers = deprecated (read-only for historical data; Phase D will remove from API).
 * Phase C: POS uses only group.products for add-on UI. Add-on = Product is the only active model.
 */
import { apiClient } from './config';
import { API_PATHS } from './apiPaths';

export interface ModifierDto {
  id: string;
  name: string;
  price: number;
  taxType: number;
  sortOrder: number;
}

/** Faz 1: Grup içi önerilen ürün (sellable add-on). Fiyat/vergi Product’tan; sepette ayrı satır. */
export interface AddOnGroupProductItemDto {
  productId: string;
  productName: string;
  price: number;
  taxType: number;
  sortOrder: number;
}

export interface ModifierGroupDto {
  id: string;
  name: string;
  minSelections: number;
  maxSelections: number | null;
  isRequired: boolean;
  sortOrder: number;
  isActive: boolean;
  /** Legacy: modifier listesi (satıra bağlı). */
  modifiers: ModifierDto[];
  /** Faz 1: Önerilen add-on ürünler – tıklanınca sepette ayrı satır (addItem(productId, 1)). */
  products?: AddOnGroupProductItemDto[];
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

export async function getProductModifierGroups(productId: string): Promise<ModifierGroupDto[]> {
  const cached = getCached(productId);
  if (cached) return cached;

  const res = await apiClient.get<ApiResponse<ModifierGroupDto[]> | ModifierGroupDto[]>(
    API_PATHS.PRODUCT.MODIFIER_GROUPS(productId)
  );
  const body = (res as any)?.data ?? res;
  const list = Array.isArray(body) ? body : (body?.data ?? []);
  modifierGroupsCache.set(productId, { data: list, ts: Date.now() });
  return list;
}
