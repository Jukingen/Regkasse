/**
 * Backup/DR known-gap acceptance — GET/PUT /api/admin/backup/known-gaps
 * Catalog titles come from the API. The frontend does not ship a second copy of the JSON.
 */
import { customInstance } from '@/lib/axios';

export const BACKUP_KNOWN_GAPS_PATH = '/api/admin/backup/known-gaps' as const;

export type BackupKnownGapCard = {
  id: string;
  title: string;
  severity: string;
  open: boolean;
  accepted: boolean;
  acceptedAtUtc: string | null;
  acceptedBy: string | null;
  displayColor: string;
};

export type BackupKnownGapAcceptance = {
  acceptedGaps: string[];
  acceptedBy: string;
  acceptedAtUtc: string | null;
};

export type BackupKnownGapView = {
  catalogVersion: number;
  allowsGateOverride: boolean;
  verdict: 'pass' | 'fail';
  acceptance: BackupKnownGapAcceptance;
  gaps: BackupKnownGapCard[];
};

export function backupKnownGapsQueryKey(tenantId: string) {
  return [BACKUP_KNOWN_GAPS_PATH, tenantId] as const;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return value != null && typeof value === 'object' && !Array.isArray(value);
}

function asString(value: unknown): string | null {
  return typeof value === 'string' && value.length > 0 ? value : null;
}

export function normalizeBackupKnownGapView(raw: unknown): BackupKnownGapView {
  if (!isRecord(raw)) {
    throw new Error('Invalid known-gaps response: expected an object');
  }
  const gapsRaw = raw.gaps;
  if (!Array.isArray(gapsRaw)) {
    throw new Error('Invalid known-gaps response: gaps must be an array');
  }
  const acceptance = isRecord(raw.acceptance) ? raw.acceptance : {};
  const acceptedList = Array.isArray(acceptance.acceptedGaps)
    ? acceptance.acceptedGaps.filter((id): id is string => typeof id === 'string' && id.length > 0)
    : [];

  return {
    catalogVersion: typeof raw.catalogVersion === 'number' ? raw.catalogVersion : 0,
    allowsGateOverride: raw.allowsGateOverride === true,
    verdict: raw.verdict === 'pass' ? 'pass' : 'fail',
    acceptance: {
      acceptedGaps: acceptedList,
      acceptedBy: typeof acceptance.acceptedBy === 'string' ? acceptance.acceptedBy : '',
      acceptedAtUtc: asString(acceptance.acceptedAtUtc),
    },
    gaps: gapsRaw.filter(isRecord).map((gap) => ({
      id: typeof gap.id === 'string' ? gap.id : '',
      title: typeof gap.title === 'string' ? gap.title : '',
      severity: typeof gap.severity === 'string' ? gap.severity : 'warning',
      open: gap.open === true,
      accepted: gap.accepted === true,
      acceptedAtUtc: asString(gap.acceptedAtUtc),
      acceptedBy: asString(gap.acceptedBy),
      displayColor: typeof gap.displayColor === 'string' ? gap.displayColor : 'red',
    })).filter((gap) => gap.id.length > 0),
  };
}

export async function getBackupKnownGaps(tenantId: string): Promise<BackupKnownGapView> {
  const raw = await customInstance<unknown>({
    url: BACKUP_KNOWN_GAPS_PATH,
    method: 'GET',
    params: { tenantId },
  });
  return normalizeBackupKnownGapView(raw);
}

export async function putBackupKnownGaps(input: {
  tenantId: string;
  acceptedGapIds: string[];
  acceptedBy: string;
}): Promise<BackupKnownGapView> {
  const raw = await customInstance<unknown>({
    url: BACKUP_KNOWN_GAPS_PATH,
    method: 'PUT',
    data: input,
  });
  return normalizeBackupKnownGapView(raw);
}

/** Localized label. Unknown catalog ids fall back to the API title. */
export function backupKnownGapLabel(
  t: (key: string) => string,
  id: string,
  fallbackTitle: string
): string {
  switch (id) {
    case 'external-archive-not-set':
      return t('backupDr.knownGapAcceptance.gaps.externalArchiveNotSet');
    case 'scheduled-drill-not-run':
      return t('backupDr.knownGapAcceptance.gaps.scheduledDrillNotRun');
    case 'system-legal-hold-not-configured':
      return t('backupDr.knownGapAcceptance.gaps.systemLegalHoldNotConfigured');
    case 'dev-only-fake-adapter':
      return t('backupDr.knownGapAcceptance.gaps.devOnlyFakeAdapter');
    default:
      return fallbackTitle || id;
  }
}
