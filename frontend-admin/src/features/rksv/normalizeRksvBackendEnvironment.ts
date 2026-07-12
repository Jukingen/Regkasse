import type { RksvBackendEnvironmentStatus } from '@/features/rksv/types/rksvBackendEnvironment';

function readString(value: unknown): string {
  return typeof value === 'string' ? value.trim() : '';
}

function readBool(value: unknown): boolean {
  return value === true;
}

export function normalizeRksvBackendEnvironment(raw: unknown): RksvBackendEnvironmentStatus | null {
  if (!raw || typeof raw !== 'object') return null;
  const body = raw as Record<string, unknown>;
  const environment = readString(body.environment ?? body.Environment);
  if (!environment) return null;

  return {
    environment,
    isSimulated: readBool(body.isSimulated ?? body.IsSimulated),
    showDemoLabel: readBool(body.showDemoLabel ?? body.ShowDemoLabel),
    tseStatusDisplay: readString(
      body.tseStatusDisplay ?? body.TseStatusDisplay ?? body.tseStatus ?? body.TseStatus,
    ),
    tseStatusBadge: readString(body.tseStatusBadge ?? body.TseStatusBadge),
    environmentDisplayName: readString(
      body.environmentDisplayName ?? body.EnvironmentDisplayName ?? body.displayName ?? body.DisplayName,
    ),
  };
}
