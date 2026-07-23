/**
 * Role permission presets – predefined permission sets for quick apply in role management.
 * Keys must exist in backend permission catalog; apply only updates local draft (or post-create save).
 *
 * Canonical keys only (`user.view`, `cash_register.view`, `daily-closing.view`, …).
 */
import { resolvePermissionGroupSlugForPermissionKey } from '@/shared/auth/permissionGroupRegistry';
import { AppPermissions, PERMISSIONS } from '@/shared/auth/permissions';

export interface RolePreset {
  id: string;
  /** Display label (de default; UI may override via i18n `users.roleDrawer.presets.<id>.label`). */
  label: string;
  /** Short description for preset cards / create-role preview. */
  description: string;
  /** Permission keys (e.g. "sale.view"). Only keys present in catalog should be applied. */
  permissionKeys: readonly string[];
  /** Optional subset highlighted in preview (defaults to first few keys). */
  highlightKeys?: readonly string[];
}

export type RolePresetPreview = {
  permissionCount: number;
  highlightKeys: string[];
  /** Catalog group slug → count of preset keys in that group. */
  groupDistribution: Array<{ slug: string; count: number }>;
};

/** Kasa Operasyon: POS günlük işlemler – sepet, sipariş, ödeme, fiş, kasa. */
export const PRESET_KASA_OPERASYON: RolePreset = {
  id: 'kasa-operasyon',
  label: 'Kasa Operasyon',
  description: 'Kasa işlemleri ve günlük POS operasyonları (sepet, ödeme, fiş).',
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
    AppPermissions.CashRegisterView,
    PERMISSIONS.TABLE_VIEW,
  ],
  highlightKeys: [
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.PAYMENT_TAKE,
    AppPermissions.CashRegisterView,
    PERMISSIONS.TSE_SIGN,
  ],
};

/** Muhasebe: Fatura, rapor, denetim, FinanzOnline. */
export const PRESET_MUHASEBE: RolePreset = {
  id: 'muhasebe',
  label: 'Muhasebe',
  description: 'Fatura, rapor, denetim ve FinanzOnline işlemleri.',
  permissionKeys: [
    PERMISSIONS.INVOICE_VIEW,
    PERMISSIONS.INVOICE_MANAGE,
    PERMISSIONS.INVOICE_EXPORT,
    PERMISSIONS.CREDIT_NOTE_CREATE,
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.REPORT_EXPORT,
    PERMISSIONS.FISCAL_EXPORT_COMPLIANCE,
    PERMISSIONS.AUDIT_VIEW,
    PERMISSIONS.AUDIT_EXPORT,
    PERMISSIONS.FINANZONLINE_VIEW,
    PERMISSIONS.FINANZONLINE_MANAGE,
    PERMISSIONS.FINANZONLINE_SUBMIT,
  ],
  highlightKeys: [
    PERMISSIONS.INVOICE_VIEW,
    PERMISSIONS.REPORT_EXPORT,
    PERMISSIONS.FINANZONLINE_VIEW,
  ],
};

/** Rapor Görüntüleme: Salt okuma – rapor ve denetim. */
export const PRESET_RAPOR_GORUNTULEME: RolePreset = {
  id: 'rapor-goruntuleme',
  label: 'Rapor Görüntüleme',
  description: 'Rapor ve denetim verilerini görüntüleme (sınırlı yazma yok).',
  permissionKeys: [
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.REPORT_EXPORT,
    PERMISSIONS.AUDIT_VIEW,
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.INVOICE_VIEW,
  ],
  highlightKeys: [PERMISSIONS.REPORT_VIEW, PERMISSIONS.AUDIT_VIEW],
};

/** Mağaza Yöneticisi: mağaza operasyonları, personel, katalog, raporlar, kapanış. */
export const PRESET_MAGAZA_YONETICISI: RolePreset = {
  id: 'magaza-yoneticisi',
  label: 'Mağaza Yöneticisi',
  description: 'Tüm mağaza operasyonları, raporlar, personel ve günlük kapanış.',
  permissionKeys: [
    PERMISSIONS.USER_VIEW,
    PERMISSIONS.USER_MANAGE,
    PERMISSIONS.ROLE_VIEW,
    AppPermissions.CashRegisterView,
    AppPermissions.CashRegisterManage,
    PERMISSIONS.SHIFT_VIEW,
    PERMISSIONS.SHIFT_MANAGE,
    PERMISSIONS.PRODUCT_VIEW,
    PERMISSIONS.PRODUCT_MANAGE,
    PERMISSIONS.CATEGORY_VIEW,
    PERMISSIONS.CATEGORY_MANAGE,
    PERMISSIONS.CUSTOMER_VIEW,
    PERMISSIONS.CUSTOMER_MANAGE,
    PERMISSIONS.ORDER_VIEW,
    PERMISSIONS.ORDER_CREATE,
    PERMISSIONS.ORDER_UPDATE,
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.SALE_CREATE,
    PERMISSIONS.PAYMENT_VIEW,
    PERMISSIONS.INVOICE_VIEW,
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.REPORT_EXPORT,
    PERMISSIONS.DAILY_CLOSING_VIEW,
    PERMISSIONS.DAILY_CLOSING_EXECUTE,
    PERMISSIONS.SETTINGS_VIEW,
    PERMISSIONS.TABLE_VIEW,
  ],
  highlightKeys: [
    PERMISSIONS.USER_MANAGE,
    AppPermissions.CashRegisterManage,
    PERMISSIONS.PRODUCT_MANAGE,
    PERMISSIONS.DAILY_CLOSING_EXECUTE,
    PERMISSIONS.REPORT_EXPORT,
  ],
};

/** Sadece Görüntüleme: geniş salt-okuma (yazma yok). */
export const PRESET_READ_ONLY: RolePreset = {
  id: 'read-only',
  label: 'Sadece Görüntüleme',
  description: 'Tüm ana verileri görüntüleme; değişiklik yetkisi yok.',
  permissionKeys: [
    PERMISSIONS.USER_VIEW,
    PERMISSIONS.ROLE_VIEW,
    AppPermissions.CashRegisterView,
    PERMISSIONS.SHIFT_VIEW,
    PERMISSIONS.PRODUCT_VIEW,
    PERMISSIONS.CATEGORY_VIEW,
    PERMISSIONS.CUSTOMER_VIEW,
    PERMISSIONS.ORDER_VIEW,
    PERMISSIONS.SALE_VIEW,
    PERMISSIONS.PAYMENT_VIEW,
    PERMISSIONS.INVOICE_VIEW,
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.AUDIT_VIEW,
    PERMISSIONS.DAILY_CLOSING_VIEW,
    PERMISSIONS.SETTINGS_VIEW,
    PERMISSIONS.TABLE_VIEW,
    PERMISSIONS.INVENTORY_VIEW,
  ],
  highlightKeys: [
    PERMISSIONS.USER_VIEW,
    PERMISSIONS.PRODUCT_VIEW,
    PERMISSIONS.REPORT_VIEW,
    PERMISSIONS.DAILY_CLOSING_VIEW,
  ],
};

/** İK Yöneticisi: personel ve rol görüntüleme. */
export const PRESET_HR_MANAGER: RolePreset = {
  id: 'hr-manager',
  label: 'İK Yöneticisi',
  description: 'Personel yönetimi ve rol görünümü (İK odaklı).',
  permissionKeys: [
    PERMISSIONS.USER_VIEW,
    PERMISSIONS.USER_MANAGE,
    PERMISSIONS.USER_RESET_PASSWORD,
    PERMISSIONS.ROLE_VIEW,
    PERMISSIONS.SHIFT_VIEW,
    PERMISSIONS.REPORT_VIEW,
  ],
  highlightKeys: [PERMISSIONS.USER_MANAGE, PERMISSIONS.ROLE_VIEW, PERMISSIONS.SHIFT_VIEW],
};

export const ROLE_PRESETS: readonly RolePreset[] = [
  PRESET_KASA_OPERASYON,
  PRESET_MUHASEBE,
  PRESET_RAPOR_GORUNTULEME,
  PRESET_MAGAZA_YONETICISI,
  PRESET_READ_ONLY,
  PRESET_HR_MANAGER,
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

export function findRolePresetById(id: string | null | undefined): RolePreset | undefined {
  if (!id) return undefined;
  return ROLE_PRESETS.find((p) => p.id === id);
}

/** Build preview stats for create-role / preset picker UI. */
export function getRolePresetPreview(
  preset: RolePreset,
  catalogKeys?: Set<string> | string[]
): RolePresetPreview {
  const keys = catalogKeys
    ? getPresetKeysInCatalog(preset, catalogKeys)
    : [...preset.permissionKeys];
  const counts = new Map<string, number>();
  for (const key of keys) {
    const slug = resolvePermissionGroupSlugForPermissionKey(key);
    counts.set(slug, (counts.get(slug) ?? 0) + 1);
  }
  const groupDistribution = [...counts.entries()]
    .map(([slug, count]) => ({ slug, count }))
    .sort((a, b) => b.count - a.count || a.slug.localeCompare(b.slug));

  const highlightSource = preset.highlightKeys?.length
    ? preset.highlightKeys
    : preset.permissionKeys.slice(0, 6);
  const keySet = new Set(keys);
  const highlightKeys = highlightSource.filter((k) => keySet.has(k)).slice(0, 8);

  return {
    permissionCount: keys.length,
    highlightKeys,
    groupDistribution,
  };
}
