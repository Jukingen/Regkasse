/**
 * Local draft persistence for CreateTenantWizard.
 * Never persists passwords or other secrets.
 */
import {
  type CreateTenantWizardData,
  createEmptyWizardData,
} from '@/features/super-admin/components/CreateTenantWizard/types';
import { clearAutoSaveDraft, readAutoSaveDraft, writeAutoSaveDraft } from '@/hooks/useAutoSave';

export const CREATE_TENANT_WIZARD_DRAFT_KEY = 'fa:draft:create-tenant-wizard:v1';

export const CREATE_TENANT_WIZARD_DRAFT_VERSION = 1 as const;

/** Max form step index persisted (summary = 3). Result step is never restored. */
export const CREATE_TENANT_WIZARD_MAX_DRAFT_STEP = 3;

export type CreateTenantWizardDraftV1 = {
  version: typeof CREATE_TENANT_WIZARD_DRAFT_VERSION;
  stepIndex: number;
  updatedAt: string;
  data: CreateTenantWizardData;
};

const SENSITIVE_KEYS = ['adminPassword'] as const;

/**
 * Strip secrets before writing to localStorage.
 * Always clears `adminPassword`; keeps passwordMode so UI can show auto vs manual.
 */
export function sanitizeCreateTenantWizardDataForDraft(
  data: CreateTenantWizardData
): CreateTenantWizardData {
  const next: CreateTenantWizardData = {
    ...createEmptyWizardData(),
    ...data,
    adminPassword: '',
  };
  for (const key of SENSITIVE_KEYS) {
    next[key] = '';
  }
  return next;
}

export function clampCreateTenantWizardDraftStep(stepIndex: number): number {
  if (!Number.isFinite(stepIndex)) return 0;
  return Math.min(CREATE_TENANT_WIZARD_MAX_DRAFT_STEP, Math.max(0, Math.trunc(stepIndex)));
}

export function buildCreateTenantWizardDraft(
  data: CreateTenantWizardData,
  stepIndex: number,
  updatedAt: string = new Date().toISOString()
): CreateTenantWizardDraftV1 {
  return {
    version: CREATE_TENANT_WIZARD_DRAFT_VERSION,
    stepIndex: clampCreateTenantWizardDraftStep(stepIndex),
    updatedAt,
    data: sanitizeCreateTenantWizardDataForDraft(data),
  };
}

function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

function pickString(v: unknown, fallback = ''): string {
  return typeof v === 'string' ? v : fallback;
}

function normalizeDraftData(raw: unknown): CreateTenantWizardData | null {
  if (!isRecord(raw)) return null;
  const empty = createEmptyWizardData();
  const licenseDays = raw.licenseDays;
  const passwordMode = raw.passwordMode === 'manual' ? 'manual' : 'auto';

  return sanitizeCreateTenantWizardDataForDraft({
    ...empty,
    name: pickString(raw.name, empty.name),
    slug: pickString(raw.slug, empty.slug),
    email: pickString(raw.email, empty.email),
    phone: typeof raw.phone === 'string' ? raw.phone : empty.phone,
    address: typeof raw.address === 'string' ? raw.address : empty.address,
    adminEmail: pickString(raw.adminEmail, empty.adminEmail),
    adminPassword: '',
    passwordMode,
    registerNumber: pickString(raw.registerNumber, empty.registerNumber),
    licenseDays:
      licenseDays === 30 || licenseDays === 90 || licenseDays === 365
        ? licenseDays
        : empty.licenseDays,
    licenseStartDate: pickString(raw.licenseStartDate, empty.licenseStartDate),
    importDemoProducts:
      typeof raw.importDemoProducts === 'boolean'
        ? raw.importDemoProducts
        : empty.importDemoProducts,
  });
}

/** Read and validate a stored draft; returns null if missing/invalid. */
export function readCreateTenantWizardDraft(): CreateTenantWizardDraftV1 | null {
  const raw = readAutoSaveDraft<unknown>(CREATE_TENANT_WIZARD_DRAFT_KEY);
  if (!isRecord(raw)) return null;
  if (raw.version !== CREATE_TENANT_WIZARD_DRAFT_VERSION) return null;
  const data = normalizeDraftData(raw.data);
  if (!data) return null;
  // Ignore empty drafts (user never typed anything meaningful).
  if (!data.name.trim() && !data.slug.trim() && !data.email.trim() && !data.adminEmail.trim()) {
    return null;
  }
  return {
    version: CREATE_TENANT_WIZARD_DRAFT_VERSION,
    stepIndex: clampCreateTenantWizardDraftStep(
      typeof raw.stepIndex === 'number' ? raw.stepIndex : 0
    ),
    updatedAt: pickString(raw.updatedAt, new Date(0).toISOString()),
    data,
  };
}

export function writeCreateTenantWizardDraft(
  data: CreateTenantWizardData,
  stepIndex: number
): void {
  writeAutoSaveDraft(CREATE_TENANT_WIZARD_DRAFT_KEY, buildCreateTenantWizardDraft(data, stepIndex));
}

export function clearCreateTenantWizardDraft(): void {
  clearAutoSaveDraft(CREATE_TENANT_WIZARD_DRAFT_KEY);
}
