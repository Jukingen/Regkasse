/**
 * Category form/table types.
 * vatRate comes from backend; add to generated Category until Orval is regenerated.
 */
import type { Category, CreateCategoryRequest, UpdateCategoryRequest } from '@/api/generated/model';

export type CategoryWithVat = Category & { vatRate?: number };

export type CreateCategoryFormValues = Omit<CreateCategoryRequest, 'vatRate'> & {
  vatRate?: number;
  isActive?: boolean;
};

export type UpdateCategoryFormValues = Omit<UpdateCategoryRequest, 'vatRate'> & {
  vatRate?: number;
  isActive?: boolean;
};
