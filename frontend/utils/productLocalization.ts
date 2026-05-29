import type { Product } from '../services/api/productService';

export type ProductTextLocale = 'de' | 'en' | 'tr';

export type LocalizedProductFields = {
  name?: string;
  nameDe?: string | null;
  nameEn?: string | null;
  nameTr?: string | null;
  description?: string | null;
  descriptionDe?: string | null;
  descriptionEn?: string | null;
  descriptionTr?: string | null;
};

export function normalizeProductTextLocale(language: string | undefined): ProductTextLocale {
  const base = (language ?? 'de').split('-')[0].toLowerCase();
  if (base === 'en' || base === 'tr') return base;
  return 'de';
}

function firstNonEmpty(...values: Array<string | null | undefined>): string {
  for (const value of values) {
    if (value != null && String(value).trim() !== '') {
      return String(value).trim();
    }
  }
  return '';
}

function firstNonEmptyOptional(...values: Array<string | null | undefined>): string | undefined {
  const hit = firstNonEmpty(...values);
  return hit || undefined;
}

export function resolveProductDisplayName(
  product: LocalizedProductFields,
  locale?: string,
): string {
  const lang = normalizeProductTextLocale(locale);
  switch (lang) {
    case 'en':
      return firstNonEmpty(product.nameEn, product.nameDe, product.name);
    case 'tr':
      return firstNonEmpty(product.nameTr, product.nameDe, product.name);
    default:
      return firstNonEmpty(product.nameDe, product.name);
  }
}

export function resolveProductDisplayDescription(
  product: LocalizedProductFields,
  locale?: string,
): string | undefined {
  const lang = normalizeProductTextLocale(locale);
  switch (lang) {
    case 'en':
      return firstNonEmptyOptional(product.descriptionEn, product.descriptionDe, product.description);
    case 'tr':
      return firstNonEmptyOptional(product.descriptionTr, product.descriptionDe, product.description);
    default:
      return firstNonEmptyOptional(product.descriptionDe, product.description);
  }
}

/** View-model with locale-resolved name/description for POS lists. */
export function withLocalizedProductDisplay<T extends LocalizedProductFields>(
  product: T,
  locale?: string,
): T & { displayName: string; displayDescription?: string } {
  return {
    ...product,
    displayName: resolveProductDisplayName(product, locale),
    displayDescription: resolveProductDisplayDescription(product, locale),
  };
}

export function productMatchesSearchQuery(product: Product, query: string, locale?: string): boolean {
  const q = query.trim().toLowerCase();
  if (!q) return true;
  const names = [
    product.name,
    product.nameDe,
    product.nameEn,
    product.nameTr,
    resolveProductDisplayName(product, locale),
  ];
  return names.some((n) => n?.toLowerCase().includes(q));
}
