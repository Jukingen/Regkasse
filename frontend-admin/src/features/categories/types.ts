/**
 * Admin category DTO (GET /api/admin/categories) — extends until Orval regenerates from OpenAPI.
 */
import type { CreateCategoryRequest, UpdateCategoryRequest } from '@/api/generated/model';

export type RksvProductCategoryValue = 0 | 1 | 2 | 3 | 4 | 99;

export type AdminCategory = {
  id: string;
  key: string;
  name: string;
  description?: string | null;
  icon?: string | null;
  color?: string | null;
  sortOrder?: number;
  productCount?: number;
  defaultTaxRate?: number;
  fiscalCategory?: RksvProductCategoryValue;
  isSystemCategory?: boolean;
  originalDemoName?: string | null;
  isActive?: boolean;
};

export type AdminCategoryCreatePayload = CreateCategoryRequest & {
  defaultTaxRate?: number;
  vatRate?: number;
  key?: string;
  description?: string | null;
  icon?: string | null;
  color?: string | null;
  fiscalCategory?: RksvProductCategoryValue;
};

export type AdminCategoryUpdatePayload = UpdateCategoryRequest & {
  defaultTaxRate?: number;
  vatRate?: number;
  description?: string | null;
  icon?: string | null;
  color?: string | null;
};

export type CreateCategoryFormValues = {
  name: string;
  defaultTaxRate?: number;
  vatRate?: number;
  sortOrder?: number;
  description?: string | null;
  icon?: string | null;
  isActive?: boolean;
};

export type UpdateCategoryFormValues = CreateCategoryFormValues;

/** @deprecated Use AdminCategory */
export type CategoryWithVat = AdminCategory & { vatRate?: number };

export function categoryTaxRate(category: AdminCategory): number {
  return category.defaultTaxRate ?? (category as { vatRate?: number }).vatRate ?? 20;
}

export function buildCategoryUpdatePayload(
  category: AdminCategory,
  patch: Partial<AdminCategoryUpdatePayload>,
): AdminCategoryUpdatePayload {
  return {
    name: patch.name ?? category.name,
    description: patch.description ?? category.description ?? undefined,
    icon: patch.icon ?? category.icon ?? undefined,
    color: patch.color ?? category.color ?? undefined,
    sortOrder: patch.sortOrder ?? category.sortOrder ?? 0,
    defaultTaxRate: patch.defaultTaxRate ?? categoryTaxRate(category),
    vatRate: patch.defaultTaxRate ?? categoryTaxRate(category),
  };
}
