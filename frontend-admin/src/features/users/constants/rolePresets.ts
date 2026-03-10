/**
 * Role permission presets – predefined permission sets for quick apply in role management.
 * Keys must exist in backend permission catalog; apply only updates local draft, not backend.
 */

import { PERMISSIONS } from '@/shared/auth/permissions';

export interface RolePreset {
  id: string;
  label: string;
  /** Permission keys (e.g. "sale.view"). Only keys present in catalog should be applied. */
  permissionKeys: readonly string[];
}

/** Kasa Operasyon: POS günlük işlemler – sepet, sipariş, ödeme, fiş, kasa. */
export const PRESET_KASA_OPERASYON: RolePreset = {
  id: 'kasa-operasyon',
  label: 'Kasa Operasyon',
  permissionKeys: [
    PERMISSIONS.CART_VIEW,
    PERMISSIONS.CART_MANAGE,
    PERMISSIONS.ORDER_VIEW,
    PERMISSIONS.ORDER_CREATE,
    PERMISSIONS.ORDER_UPDATE,
    PERMISSIONS.ORDER_CANCEL,
    PERMISSIONS.PAYMENT_VIEW,
    PERMISSIONS.PAYMENT_TAKE,
    PERMISSIONS.PAYMENT_CANCEL,
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.SALE_CREATE,
    PERMISSIONS.REFUND_CREATE,
    PERMISSIONS.TSE_SIGN,
    PERMISSIONS.RECEIPT_REPRINT,
  ],
};

/** Muhasebe: Fatura, rapor, denetim, FinanzOnline. */
export const PRESET_MUHASEBE: RolePreset = {
  id: 'muhasebe',
  label: 'Muhasebe',
  permissionKeys: [
    PERMISSIONS.INVOICE_VIEW,
    PERMISSIONS.INVOICE_MANAGE,
    PERMISSIONS.INVOICE_EXPORT,
    PERMISSIONS.CREDIT_NOTE_CREATE,
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.REPORT_EXPORT,
    PERMISSIONS.AUDIT_VIEW,
    PERMISSIONS.AUDIT_EXPORT,
    PERMISSIONS.FINANZONLINE_VIEW,
    PERMISSIONS.FINANZONLINE_MANAGE,
    PERMISSIONS.FINANZONLINE_SUBMIT,
  ],
};

/** Rapor Görüntüleme: Salt okuma – rapor ve denetim. */
export const PRESET_RAPOR_GORUNTULEME: RolePreset = {
  id: 'rapor-goruntuleme',
  label: 'Rapor Görüntüleme',
  permissionKeys: [
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.REPORT_EXPORT,
    PERMISSIONS.AUDIT_VIEW,
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.INVOICE_VIEW,
  ],
};

/** Mağaza Yöneticisi: Ürün, kategori, sipariş, kullanıcı listesi, ayarlar görüntüleme, rapor. */
export const PRESET_MAGAZA_YONETICISI: RolePreset = {
  id: 'magaza-yoneticisi',
  label: 'Mağaza Yöneticisi',
  permissionKeys: [
    PERMISSIONS.PRODUCT_VIEW,
    PERMISSIONS.PRODUCT_MANAGE,
    PERMISSIONS.CATEGORY_VIEW,
    PERMISSIONS.CATEGORY_MANAGE,
    PERMISSIONS.ORDER_VIEW,
    PERMISSIONS.USER_VIEW,
    PERMISSIONS.SETTINGS_VIEW,
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.INVOICE_VIEW,
    'customer.view',
  ],
};

export const ROLE_PRESETS: readonly RolePreset[] = [
  PRESET_KASA_OPERASYON,
  PRESET_MUHASEBE,
  PRESET_RAPOR_GORUNTULEME,
  PRESET_MAGAZA_YONETICISI,
];

/**
 * Returns preset permission keys filtered to only those present in the catalog.
 * Use when applying a preset so we never add keys the backend doesn't know.
 */
export function getPresetKeysInCatalog(
  preset: RolePreset,
  catalogKeys: Set<string> | string[]
): string[] {
  const set = catalogKeys instanceof Set ? catalogKeys : new Set(catalogKeys);
  return preset.permissionKeys.filter((key) => set.has(key));
}
