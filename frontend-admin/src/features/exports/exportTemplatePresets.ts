import type { ExportTemplate } from '@/features/exports/exportTemplateTypes';

const PRESET_TS = '2026-01-01T00:00:00.000Z';

/**
 * Built-in export scenario templates (not persisted).
 * Display names come from i18n via preset id in the UI.
 */
export const EXPORT_TEMPLATE_PRESETS: readonly ExportTemplate[] = [
  {
    id: 'preset-daily-dep',
    name: 'Täglicher DEP Export',
    shared: false,
    isPreset: true,
    config: {
      kind: 'dep-export',
      registerNumberHint: 'KASSE-001',
      period: 'last24h',
      includeSpecialReceipts: true,
      includeDailyClosings: true,
    },
    createdAt: PRESET_TS,
    updatedAt: PRESET_TS,
  },
  {
    id: 'preset-monthly-tagesbericht',
    name: 'Monatlicher Tagesbericht',
    shared: false,
    isPreset: true,
    config: {
      kind: 'tagesbericht',
      period: 'currentMonth',
      formats: ['pdf', 'csv'],
    },
    createdAt: PRESET_TS,
    updatedAt: PRESET_TS,
  },
  {
    id: 'preset-quarterly-backup',
    name: 'Quartals Backup',
    shared: false,
    isPreset: true,
    config: {
      kind: 'backup',
      strategy: 'system',
      retentionDays: 90,
    },
    createdAt: PRESET_TS,
    updatedAt: PRESET_TS,
  },
];

export function getPresetById(id: string): ExportTemplate | undefined {
  return EXPORT_TEMPLATE_PRESETS.find((p) => p.id === id);
}
