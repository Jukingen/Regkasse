/**
 * Extra Zutaten (Modifier Groups) API – Admin: ürün modifier’ları api/admin/products üzerinden.
 * Modifier-groups listesi hâlâ /api/modifier-groups (ortak endpoint).
 */

import { AXIOS_INSTANCE } from '@/lib/axios';
import { getAdminProductModifierGroups, setAdminProductModifierGroups as setAdminProductModifierGroupsApi } from '@/api/admin/products';

export interface ModifierDto {
  id: string;
  name: string;
  price: number;
  taxType: number;
  sortOrder: number;
  /** false = migriert/deaktiviert (Legacy-Modifier nach Migration). */
  isActive?: boolean;
}

/** Faz 1: Grup içi Produkt-Referenz (Fiyat/vergi Product'tan). */
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
  /** Legacy (Fallback). */
  modifiers: ModifierDto[];
  /** Faz 1: Önerilen Produkte – Preis/MwSt. nur aus Produktdaten. */
  products?: AddOnGroupProductItemDto[];
}

interface ApiResponse<T> {
  success?: boolean;
  message?: string;
  data: T;
}

/** All modifier groups with full details (products + modifiers). Use for migration UI and for group list in product form. Phase D PR-D: legacy modifiers only here. */
export async function getModifierGroups(): Promise<ModifierGroupDto[]> {
  const res = await AXIOS_INSTANCE.get<ApiResponse<ModifierGroupDto[]>>('/api/modifier-groups');
  const data = res.data?.data ?? res.data;
  return Array.isArray(data) ? data : [];
}

/** Assigned modifier groups for a product (admin API). Phase D PR-D: returns products-only; used for assigned group IDs. Full details (incl. modifiers) from getModifierGroups(). */
export async function getProductModifierGroups(productId: string): Promise<ModifierGroupDto[]> {
  const data = await getAdminProductModifierGroups(productId);
  return Array.isArray(data) ? data : [];
}

/** Ürüne modifier gruplarını ata (admin API). */
export async function setProductModifierGroups(
  productId: string,
  modifierGroupIds: string[]
): Promise<void> {
  await setAdminProductModifierGroupsApi(productId, modifierGroupIds);
}

/** Yeni modifier group oluştur. */
export async function createModifierGroup(body: {
  name: string;
  minSelections?: number;
  maxSelections?: number | null;
  isRequired?: boolean;
  sortOrder?: number;
}): Promise<{ id: string }> {
  const res = await AXIOS_INSTANCE.post<ApiResponse<{ id: string }>>('/api/modifier-groups', body);
  const data = res.data?.data ?? res.data;
  return data as { id: string };
}

/** Modifier group metadata güncelle (Name, SortOrder, Min/MaxSelections, IsRequired). PUT /api/modifier-groups/{id} */
export async function updateModifierGroup(
  groupId: string,
  body: { name: string; minSelections?: number; maxSelections?: number | null; isRequired?: boolean; sortOrder?: number }
): Promise<void> {
  await AXIOS_INSTANCE.put(`/api/modifier-groups/${groupId}`, body);
}

/**
 * Gruba yeni modifier ekle (Legacy).
 * @deprecated Phase 2: Legacy modifier creation is frozen. Backend returns 410. Use addProductToGroup instead.
 * Unused: no UI calls this; backend POST .../modifiers returns 410. Stub avoids dead HTTP calls.
 */
export function addModifierToGroup(
  _groupId: string,
  _body: { name: string; price?: number; taxType?: number; sortOrder?: number }
): Promise<ModifierDto> {
  return Promise.reject(
    new Error('Legacy modifier creation is disabled (410). Use addProductToGroup to add products to the group instead.')
  );
}

/** Faz 1: Gruba Produkt hinzufügen – bestehendes Produkt (productId) oder neues Add-on (createNewAddOnProduct). */
export type AddProductToGroupBody =
  | { productId: string; createNewAddOnProduct?: never }
  | { productId?: never; createNewAddOnProduct: { name: string; price: number; taxType: number; categoryId?: string; sortOrder: number } };

export async function addProductToGroup(
  groupId: string,
  body: AddProductToGroupBody
): Promise<AddOnGroupProductItemDto> {
  const res = await AXIOS_INSTANCE.post<ApiResponse<AddOnGroupProductItemDto>>(
    `/api/modifier-groups/${groupId}/products`,
    body
  );
  const data = res.data?.data ?? res.data;
  return data as AddOnGroupProductItemDto;
}

/** Produkt aus Gruppe entfernen (nur Zuordnung; Product bleibt erhalten). DELETE /api/modifier-groups/{groupId}/products/{productId} */
export async function removeProductFromGroup(groupId: string, productId: string): Promise<void> {
  await AXIOS_INSTANCE.delete(`/api/modifier-groups/${groupId}/products/${productId}`);
}

/** Legacy-Modifier als Add-on-Produkt migrieren ("Als Produkt migrieren"). Idempotent. */
export interface MigrateLegacyModifierBody {
  categoryId: string;
  markModifierInactive?: boolean;
}

export interface MigrateLegacyModifierResult {
  modifierId: string;
  modifierName: string;
  productId?: string;
  productName: string;
  groupId: string;
  alreadyMigrated: boolean;
  modifierMarkedInactive: boolean;
}

export async function migrateLegacyModifier(
  groupId: string,
  modifierId: string,
  body: MigrateLegacyModifierBody
): Promise<MigrateLegacyModifierResult> {
  const res = await AXIOS_INSTANCE.post<ApiResponse<MigrateLegacyModifierResult>>(
    `/api/modifier-groups/${groupId}/modifiers/${modifierId}/migrate`,
    body
  );
  const data = res.data?.data ?? res.data;
  return data as MigrateLegacyModifierResult;
}
