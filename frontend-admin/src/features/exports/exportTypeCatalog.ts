import { PERMISSIONS } from '@/shared/auth/permissions';

/**
 * Canonical admin export surfaces that can be starred / quick-linked.
 */
export type ExportTypeId =
  | 'dep-export'
  | 'tagesbericht'
  | 'backup'
  | 'invoice'
  | 'report-center'
  | 'fiscal-exports';

export type ExportTypeDef = {
  id: ExportTypeId;
  /** Route to open the export workflow. */
  href: string;
  /** Optional download-history sourceKind filter. */
  sourceKind?: string;
  /** Permission required to see / use this export type. */
  permission: string;
  /** i18n key for list label (under common.exportFavorites.types.*). */
  labelKey: string;
  /** i18n key for quick-action verb phrase. */
  quickActionKey: string;
};

export const EXPORT_TYPE_CATALOG: readonly ExportTypeDef[] = [
  {
    id: 'dep-export',
    href: '/admin/rksv/dep-export',
    sourceKind: 'dep-export',
    permission: PERMISSIONS.REPORT_EXPORT,
    labelKey: 'common.exportFavorites.types.depExport',
    quickActionKey: 'common.exportFavorites.quick.depExport',
  },
  {
    id: 'tagesbericht',
    href: '/reporting/tagesbericht',
    sourceKind: undefined,
    permission: PERMISSIONS.REPORT_VIEW,
    labelKey: 'common.exportFavorites.types.tagesbericht',
    quickActionKey: 'common.exportFavorites.quick.tagesbericht',
  },
  {
    id: 'backup',
    href: '/backup',
    sourceKind: 'backup',
    permission: PERMISSIONS.BACKUP_MANAGE,
    labelKey: 'common.exportFavorites.types.backup',
    quickActionKey: 'common.exportFavorites.quick.backup',
  },
  {
    id: 'invoice',
    href: '/invoices',
    sourceKind: 'invoice',
    permission: PERMISSIONS.INVOICE_EXPORT,
    labelKey: 'common.exportFavorites.types.invoice',
    quickActionKey: 'common.exportFavorites.quick.invoice',
  },
  {
    id: 'report-center',
    href: '/reporting/report-center',
    permission: PERMISSIONS.REPORT_EXPORT,
    labelKey: 'common.exportFavorites.types.reportCenter',
    quickActionKey: 'common.exportFavorites.quick.reportCenter',
  },
  {
    id: 'fiscal-exports',
    href: '/admin/audit/fiscal-exports',
    permission: PERMISSIONS.AUDIT_VIEW,
    labelKey: 'common.exportFavorites.types.fiscalExports',
    quickActionKey: 'common.exportFavorites.quick.fiscalExports',
  },
] as const;

export const DEFAULT_EXPORT_FAVORITE_IDS: readonly ExportTypeId[] = [
  'dep-export',
  'backup',
  'tagesbericht',
];

export function getExportTypeById(id: string): ExportTypeDef | undefined {
  return EXPORT_TYPE_CATALOG.find((e) => e.id === id);
}

export function isExportTypeId(value: unknown): value is ExportTypeId {
  return typeof value === 'string' && EXPORT_TYPE_CATALOG.some((e) => e.id === value);
}
