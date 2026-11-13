/**
 * GET /api/system/time/status (ASP.NET default camelCase JSON).
 */
export type TimeSyncWarningLevel = 'ok' | 'warning' | 'critical';

export type SystemTimeStatusDto = {
  systemTimeUtc: string;
  ntpTimeUtc: string | null;
  offsetSeconds: number | null;
  isSynchronized: boolean;
  lastSyncAt: string | null;
  warningLevel: TimeSyncWarningLevel;
};

export function normalizeSystemTimeStatusDto(raw: unknown): SystemTimeStatusDto | null {
  if (raw == null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const warningLevel = (o.warningLevel ?? o.WarningLevel) as string | undefined;
  const level: TimeSyncWarningLevel =
    warningLevel === 'warning' || warningLevel === 'critical' || warningLevel === 'ok'
      ? warningLevel
      : 'ok';

  const off = o.offsetSeconds ?? o.OffsetSeconds;
  const offsetSeconds =
    typeof off === 'number' && Number.isFinite(off)
      ? off
      : typeof off === 'string' && off.trim() !== '' && Number.isFinite(Number(off))
        ? Number(off)
        : null;

  const syncRaw = o.lastSyncAt ?? o.LastSyncAt;
  const lastSyncAt =
    typeof syncRaw === 'string' && syncRaw.length > 0
      ? syncRaw
      : syncRaw instanceof Date
        ? syncRaw.toISOString()
        : null;

  const sysRaw = o.systemTimeUtc ?? o.SystemTimeUtc;
  const systemTimeUtc =
    typeof sysRaw === 'string'
      ? sysRaw
      : sysRaw instanceof Date
        ? sysRaw.toISOString()
        : new Date().toISOString();

  const ntpRaw = o.ntpTimeUtc ?? o.NtpTimeUtc;
  let ntpTimeUtc: string | null = null;
  if (typeof ntpRaw === 'string') {
    ntpTimeUtc = ntpRaw;
  } else if (ntpRaw instanceof Date) {
    ntpTimeUtc = ntpRaw.toISOString();
  }

  const isSynRaw = o.isSynchronized ?? o.IsSynchronized;
  const isSynchronized = typeof isSynRaw === 'boolean' ? isSynRaw : false;

  return {
    systemTimeUtc,
    ntpTimeUtc,
    offsetSeconds,
    isSynchronized,
    lastSyncAt,
    warningLevel: level,
  };
}

/** Align POS banners/guards with backend thresholds (seconds). */
export function deriveTimeSyncUiFlags(status: SystemTimeStatusDto | null): {
  absOffsetSeconds: number | null;
  timeSyncCritical: boolean;
  timeSyncWarningBand: boolean;
} {
  if (!status) {
    return { absOffsetSeconds: null, timeSyncCritical: false, timeSyncWarningBand: false };
  }

  const off = status.offsetSeconds;
  const abs = typeof off === 'number' && Number.isFinite(off) ? Math.abs(off) : null;

  const criticalByLevel = status.warningLevel === 'critical';
  const warnByLevel = status.warningLevel === 'warning';

  const timeSyncCritical = criticalByLevel || (abs != null && abs > 60);

  const timeSyncWarningBand =
    !timeSyncCritical && (warnByLevel || (abs != null && abs > 5 && abs <= 60));

  return {
    absOffsetSeconds: abs,
    timeSyncCritical,
    timeSyncWarningBand,
  };
}
