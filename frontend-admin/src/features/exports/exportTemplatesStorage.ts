/**
 * Personal + tenant-shared export templates and last-used pointer (localStorage).
 */

import { EXPORT_TEMPLATE_PRESETS, getPresetById } from '@/features/exports/exportTemplatePresets';
import type {
  BackupTemplateConfig,
  DepExportTemplateConfig,
  ExportTemplate,
  ExportTemplateConfig,
  ExportTemplateLastUsed,
  TagesberichtTemplateConfig,
} from '@/features/exports/exportTemplateTypes';
import { isExportTemplatePeriod } from '@/features/exports/exportTemplateTypes';

const PERSONAL_PREFIX = 'regkasse.admin.exportTemplates.personal.v1:';
const SHARED_PREFIX = 'regkasse.admin.exportTemplates.shared.v1:';
const LAST_USED_PREFIX = 'regkasse.admin.exportTemplates.lastUsed.v1:';

function personalKey(userId: string): string {
  return `${PERSONAL_PREFIX}${userId || 'anon'}`;
}

function sharedKey(tenantId: string): string {
  return `${SHARED_PREFIX}${tenantId || 'default'}`;
}

function lastUsedKey(userId: string): string {
  return `${LAST_USED_PREFIX}${userId || 'anon'}`;
}

function readList(storageKey: string): ExportTemplate[] {
  if (typeof window === 'undefined') return [];
  try {
    const raw = window.localStorage.getItem(storageKey);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(isExportTemplate);
  } catch {
    return [];
  }
}

function writeList(storageKey: string, items: ExportTemplate[]): void {
  if (typeof window === 'undefined') return;
  try {
    window.localStorage.setItem(storageKey, JSON.stringify(items));
  } catch {
    // ignore quota / private mode
  }
}

function isDepConfig(c: unknown): c is DepExportTemplateConfig {
  if (!c || typeof c !== 'object') return false;
  const v = c as DepExportTemplateConfig;
  return (
    v.kind === 'dep-export' &&
    isExportTemplatePeriod(v.period) &&
    typeof v.includeSpecialReceipts === 'boolean' &&
    typeof v.includeDailyClosings === 'boolean'
  );
}

function isTagesberichtConfig(c: unknown): c is TagesberichtTemplateConfig {
  if (!c || typeof c !== 'object') return false;
  const v = c as TagesberichtTemplateConfig;
  return (
    v.kind === 'tagesbericht' &&
    isExportTemplatePeriod(v.period) &&
    Array.isArray(v.formats) &&
    v.formats.every((f) => f === 'pdf' || f === 'csv')
  );
}

function isBackupConfig(c: unknown): c is BackupTemplateConfig {
  if (!c || typeof c !== 'object') return false;
  const v = c as BackupTemplateConfig;
  return (
    v.kind === 'backup' &&
    (v.strategy === 'tenant' || v.strategy === 'system') &&
    typeof v.retentionDays === 'number' &&
    Number.isFinite(v.retentionDays)
  );
}

export function isExportTemplateConfig(value: unknown): value is ExportTemplateConfig {
  return isDepConfig(value) || isTagesberichtConfig(value) || isBackupConfig(value);
}

export function isExportTemplate(value: unknown): value is ExportTemplate {
  if (!value || typeof value !== 'object') return false;
  const v = value as ExportTemplate;
  return (
    typeof v.id === 'string' &&
    typeof v.name === 'string' &&
    typeof v.shared === 'boolean' &&
    typeof v.createdAt === 'string' &&
    typeof v.updatedAt === 'string' &&
    isExportTemplateConfig(v.config)
  );
}

export function createExportTemplateId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `et_${Date.now()}_${Math.random().toString(36).slice(2, 10)}`;
}

export function loadPersonalExportTemplates(userId: string): ExportTemplate[] {
  if (!userId) return [];
  return readList(personalKey(userId)).filter((t) => !t.isPreset);
}

export function loadSharedExportTemplates(tenantId: string): ExportTemplate[] {
  return readList(sharedKey(tenantId)).filter((t) => !t.isPreset && t.shared);
}

export function listAllExportTemplates(opts: {
  userId: string;
  tenantId: string;
}): ExportTemplate[] {
  const personal = loadPersonalExportTemplates(opts.userId);
  const shared = loadSharedExportTemplates(opts.tenantId).filter(
    (s) => !personal.some((p) => p.id === s.id)
  );
  return [...EXPORT_TEMPLATE_PRESETS, ...personal, ...shared];
}

export function getExportTemplateById(
  id: string,
  opts: { userId: string; tenantId: string }
): ExportTemplate | undefined {
  const preset = getPresetById(id);
  if (preset) return preset;
  return (
    loadPersonalExportTemplates(opts.userId).find((t) => t.id === id) ??
    loadSharedExportTemplates(opts.tenantId).find((t) => t.id === id)
  );
}

export function saveExportTemplate(
  template: ExportTemplate,
  opts: { userId: string; tenantId: string }
): ExportTemplate[] {
  if (template.isPreset) {
    throw new Error('Cannot overwrite built-in presets.');
  }
  const now = new Date().toISOString();
  const row: ExportTemplate = {
    ...template,
    isPreset: false,
    updatedAt: now,
    createdAt: template.createdAt || now,
  };

  if (row.shared) {
    const list = loadSharedExportTemplates(opts.tenantId);
    const next = [...list.filter((t) => t.id !== row.id), { ...row, shared: true }];
    writeList(sharedKey(opts.tenantId), next);
    // Remove from personal if moved to shared
    writeList(
      personalKey(opts.userId),
      loadPersonalExportTemplates(opts.userId).filter((t) => t.id !== row.id)
    );
    return listAllExportTemplates(opts);
  }

  const list = loadPersonalExportTemplates(opts.userId);
  const next = [...list.filter((t) => t.id !== row.id), { ...row, shared: false }];
  writeList(personalKey(opts.userId), next);
  writeList(
    sharedKey(opts.tenantId),
    loadSharedExportTemplates(opts.tenantId).filter((t) => t.id !== row.id)
  );
  return listAllExportTemplates(opts);
}

export function deleteExportTemplate(
  id: string,
  opts: { userId: string; tenantId: string; shared: boolean }
): void {
  if (getPresetById(id)) return;
  if (opts.shared) {
    writeList(
      sharedKey(opts.tenantId),
      loadSharedExportTemplates(opts.tenantId).filter((t) => t.id !== id)
    );
    return;
  }
  writeList(
    personalKey(opts.userId),
    loadPersonalExportTemplates(opts.userId).filter((t) => t.id !== id)
  );
}

export function loadLastUsedExportTemplate(userId: string): ExportTemplateLastUsed | null {
  if (typeof window === 'undefined') return null;
  try {
    const raw = window.localStorage.getItem(lastUsedKey(userId));
    if (!raw) return null;
    const parsed = JSON.parse(raw) as ExportTemplateLastUsed;
    if (
      parsed &&
      typeof parsed.templateId === 'string' &&
      typeof parsed.usedAt === 'string'
    ) {
      return parsed;
    }
    return null;
  } catch {
    return null;
  }
}

export function markExportTemplateUsed(userId: string, templateId: string): void {
  if (typeof window === 'undefined') return;
  try {
    const payload: ExportTemplateLastUsed = {
      templateId,
      usedAt: new Date().toISOString(),
    };
    window.localStorage.setItem(lastUsedKey(userId), JSON.stringify(payload));
  } catch {
    // ignore
  }
}
