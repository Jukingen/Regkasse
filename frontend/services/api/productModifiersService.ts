/**
 * POS: Ürün modifier grupları (Extra Zutaten). Kasiyer ürün seçince modal'da gösterilir.
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
  data?: T;
}

export async function getProductModifierGroups(productId: string): Promise<ModifierGroupDto[]> {
  const res = await apiClient.get<ApiResponse<ModifierGroupDto[]> | ModifierGroupDto[]>(
    API_PATHS.PRODUCT.MODIFIER_GROUPS(productId)
  );
  const body = (res as any)?.data ?? res;
  const list = Array.isArray(body) ? body : (body?.data ?? []);
  return list;
}
