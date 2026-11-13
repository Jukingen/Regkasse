import type { DemoImportRequest } from '@/api/admin/products';

export type DemoImportImageMode = 'none' | 'categoryPlaceholder' | 'defaultAsset';

export const DEFAULT_DEMO_IMPORT_IMAGE_MODE: DemoImportImageMode = 'categoryPlaceholder';

export function toImageModeRequest(
  mode: DemoImportImageMode
): Pick<DemoImportRequest, 'imageMode'> {
  return { imageMode: mode };
}

/** Preview hue aligned with backend category placeholder generator. */
export function categoryAvatarColor(categoryName: string): string {
  let hash = 0;
  const key = categoryName.trim().toLowerCase();
  for (let i = 0; i < key.length; i++) {
    hash = (hash * 31 + key.charCodeAt(i)) >>> 0;
  }
  const hue = hash % 360;
  return `hsl(${hue} 42% 48%)`;
}

export function categoryAvatarLabel(categoryName: string): string {
  const trimmed = categoryName.trim();
  return (trimmed.length <= 3 ? trimmed : trimmed.slice(0, 3)).toUpperCase();
}

export { applyPriceAdjustment } from './priceAdjustment';
