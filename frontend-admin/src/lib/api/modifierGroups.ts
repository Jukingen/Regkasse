/**
 * Extra Zutaten (Modifier Groups) API – manuel client.
 * Backend: GET/POST /api/modifier-groups, POST /api/Product/{id}/modifier-groups
 */

import { AXIOS_INSTANCE } from '@/lib/axios';

export interface ModifierDto {
  id: string;
  name: string;
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
  modifiers: ModifierDto[];
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

/** Ürüne atanmış modifier gruplarını getir. */
export async function getProductModifierGroups(productId: string): Promise<ModifierGroupDto[]> {
  const res = await AXIOS_INSTANCE.get<ApiResponse<ModifierGroupDto[]>>(
    `/api/Product/${productId}/modifier-groups`
  );
  const data = res.data?.data ?? res.data;
  return Array.isArray(data) ? data : [];
}

/** Ürüne modifier gruplarını ata (mevcut atamalar replace edilir). */
export async function setProductModifierGroups(
  productId: string,
  modifierGroupIds: string[]
): Promise<void> {
  await AXIOS_INSTANCE.post(`/api/Product/${productId}/modifier-groups`, {
    modifierGroupIds,
  });
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

/** Gruba yeni modifier ekle. */
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
