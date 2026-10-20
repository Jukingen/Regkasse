import { registerApiErrorCodeTranslation } from '@/shared/errors/apiErrorCodeRegistry';

let registered = false;

/** Map product-variant API `code` values to FA i18n keys. */
export function ensureProductVariantApiErrorTranslations(): void {
  if (registered) return;
  registered = true;
  registerApiErrorCodeTranslation('SKU_DUPLICATE', 'products.variants.skuDuplicate');
  registerApiErrorCodeTranslation(
    'VARIANT_LABEL_DUPLICATE',
    'products.variants.labelDuplicate'
  );
}
