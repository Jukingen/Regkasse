import type { TFunction } from 'i18next';

/**
 * Maps backend/catalog category display names (German labels from API) to
 * canonical `products` namespace keys. Use when you need a localized label;
 * compact UI chips may still prefer the raw `name` from the server.
 */
export const BACKEND_CATEGORY_NAME_TO_PRODUCTS_KEY: Record<string, string> = {
  Hauptgerichte: 'hauptgerichte',
  Getränke: 'getraenke',
  Desserts: 'desserts',
  'Alkoholische Getränke': 'alkoholischeGetraenke',
  Snacks: 'snacks',
  Suppen: 'suppen',
  Vorspeisen: 'vorspeisen',
  Salate: 'salate',
  'Kaffee & Tee': 'kaffeeTee',
  Süßigkeiten: 'suessigkeiten',
  Spezialitäten: 'spezialitaeten',
  'Brot & Gebäck': 'brotGebaeck',
};

function translateProductsLeafKey(leaf: string, t: TFunction): string {
  switch (leaf) {
    case 'hauptgerichte':
      return t('products:hauptgerichte');
    case 'getraenke':
      return t('products:getraenke');
    case 'desserts':
      return t('products:desserts');
    case 'alkoholischeGetraenke':
      return t('products:alkoholischeGetraenke');
    case 'snacks':
      return t('products:snacks');
    case 'suppen':
      return t('products:suppen');
    case 'vorspeisen':
      return t('products:vorspeisen');
    case 'salate':
      return t('products:salate');
    case 'kaffeeTee':
      return t('products:kaffeeTee');
    case 'suessigkeiten':
      return t('products:suessigkeiten');
    case 'spezialitaeten':
      return t('products:spezialitaeten');
    case 'brotGebaeck':
      return t('products:brotGebaeck');
    default:
      return '';
  }
}

/**
 * Returns translated category title when `backendName` is known; otherwise returns the raw name.
 */
export function resolveProductsCategoryLabel(
  backendCategoryName: string | null | undefined,
  t: TFunction
): string {
  const n = (backendCategoryName ?? '').trim();
  if (!n) return '';
  const leaf = BACKEND_CATEGORY_NAME_TO_PRODUCTS_KEY[n];
  if (leaf) {
    const translated = translateProductsLeafKey(leaf, t);
    if (translated) return translated;
  }
  return n;
}
