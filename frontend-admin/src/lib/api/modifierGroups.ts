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

/** Tüm modifier gruplarını getir (modifier listesi ile). */
export async function getModifierGroups(): Promise<ModifierGroupDto[]> {
  const res = await AXIOS_INSTANCE.get<ApiResponse<ModifierGroupDto[]>>('/api/modifier-groups');
  const data = res.data?.data ?? res.data;
  return Array.isArray(data) ? data : [];
}

/** Ürüne atanmış modifier gruplarını getir (admin API). */
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

/**
 * Gruba yeni modifier ekle (Legacy).
 * @deprecated Phase 2: Legacy modifier creation is frozen. Backend returns 410. Use addProductToGroup instead.
 */
export async function addModifierToGroup(
  groupId: string,
  body: { name: string; price?: number; taxType?: number; sortOrder?: number }
): Promise<ModifierDto> {
  const res = await AXIOS_INSTANCE.post<ApiResponse<ModifierDto>>(
    `/api/modifier-groups/${groupId}/modifiers`,
    body
  );
  const data = res.data?.data ?? res.data;
  return data as ModifierDto;
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
