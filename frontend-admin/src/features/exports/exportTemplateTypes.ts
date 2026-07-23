import type { ExportTypeId } from '@/features/exports/exportTypeCatalog';

export type ExportTemplatePeriod =
  | 'last24h'
  | 'last7d'
  | 'currentMonth'
  | 'lastMonth'
  | 'custom';

export type DepExportTemplateConfig = {
  kind: 'dep-export';
  /** Prefer matching register by number label (e.g. KASSE-001). */
  registerNumberHint?: string;
  cashRegisterId?: string;
  period: ExportTemplatePeriod;
  customFromUtc?: string;
  customToUtc?: string;
  includeSpecialReceipts: boolean;
  includeDailyClosings: boolean;
};

export type TagesberichtTemplateConfig = {
  kind: 'tagesbericht';
  period: ExportTemplatePeriod;
  customFromUtc?: string;
  customToUtc?: string;
  formats: Array<'pdf' | 'csv'>;
};

export type BackupTemplateConfig = {
  kind: 'backup';
  strategy: 'tenant' | 'system';
  retentionDays: number;
};

export type ExportTemplateConfig =
  | DepExportTemplateConfig
  | TagesberichtTemplateConfig
  | BackupTemplateConfig;

export type ExportTemplate = {
  id: string;
  name: string;
  /** Optional free-text note. */
  description?: string;
  /** When true, stored in tenant-shared localStorage list. */
  shared: boolean;
  /** Built-in preset (not persisted). */
  isPreset?: boolean;
  config: ExportTemplateConfig;
  createdAt: string;
  updatedAt: string;
  createdByUserId?: string | null;
  createdByName?: string | null;
};

export type ExportTemplateLastUsed = {
  templateId: string;
  usedAt: string;
};

export function templateKindToExportTypeId(kind: ExportTemplateConfig['kind']): ExportTypeId {
  return kind;
}

export function isExportTemplatePeriod(value: unknown): value is ExportTemplatePeriod {
  return (
    value === 'last24h' ||
    value === 'last7d' ||
    value === 'currentMonth' ||
    value === 'lastMonth' ||
    value === 'custom'
  );
}
