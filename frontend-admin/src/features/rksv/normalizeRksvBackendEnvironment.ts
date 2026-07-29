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

  const reasonsRaw =
    body.fiscalConfigLockReasons ?? body.FiscalConfigLockReasons ?? body.fiscalConfigLockReasons;
  const reasons: string[] = Array.isArray(reasonsRaw)
    ? reasonsRaw.filter((r): r is string => typeof r === 'string' && r.trim().length > 0)
    : [];

  const lockOkRaw = body.fiscalConfigLockOk ?? body.FiscalConfigLockOk;
  const fiscalConfigLockOk = lockOkRaw === undefined || lockOkRaw === null ? true : lockOkRaw === true;

  return {
    environment,
    isSimulated: readBool(body.isSimulated ?? body.IsSimulated),
    showDemoLabel: readBool(body.showDemoLabel ?? body.ShowDemoLabel),
    tseStatusDisplay: readString(
      body.tseStatusDisplay ?? body.TseStatusDisplay ?? body.tseStatus ?? body.TseStatus
    ),
    tseStatusBadge: readString(body.tseStatusBadge ?? body.TseStatusBadge),
    environmentDisplayName: readString(
      body.environmentDisplayName ??
        body.EnvironmentDisplayName ??
        body.displayName ??
        body.DisplayName
    ),
    hostEnvironment: readString(body.hostEnvironment ?? body.HostEnvironment),
    isHostDevelopment: readBool(body.isHostDevelopment ?? body.IsHostDevelopment),
    isHostStaging: readBool(body.isHostStaging ?? body.IsHostStaging),
    releaseStage: readString(body.releaseStage ?? body.ReleaseStage) || 'production',
    isCanary: readBool(body.isCanary ?? body.IsCanary),
    isFinanzOnlineSimulated: readBool(
      body.isFinanzOnlineSimulated ?? body.IsFinanzOnlineSimulated
    ),
    isSimulationMode:
      readBool(body.isSimulationMode ?? body.IsSimulationMode) ||
      readBool(body.isSimulated ?? body.IsSimulated) ||
      readBool(body.isFinanzOnlineSimulated ?? body.IsFinanzOnlineSimulated),
    fiscalConfigLockOk,
    fiscalConfigLockEscapeHatchActive: readBool(
      body.fiscalConfigLockEscapeHatchActive ?? body.FiscalConfigLockEscapeHatchActive
    ),
    fiscalConfigLockReasons: reasons,
  };
}
