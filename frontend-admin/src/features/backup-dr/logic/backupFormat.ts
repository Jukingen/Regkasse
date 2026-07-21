/**
 * Backup panosu için bayt ve süre biçimlendirme (i18n anahtarları backupDr.latestRun.*).
 */

export function formatBackupBytes(
  n: number | undefined,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  if (n === undefined) return '—';
  if (n < 1024) return t('backupDr.latestRun.bytesB', { n: String(n) });
  const kb = n / 1024;
  if (kb < 1024) return t('backupDr.latestRun.bytesKb', { n: kb.toFixed(1) });
  const mb = kb / 1024;
  return t('backupDr.latestRun.bytesMb', { n: mb.toFixed(2) });
}

export function formatBackupDurationMs(
  ms: number | undefined,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  if (ms === undefined) return '—';
  if (ms < 1000) return t('backupDr.latestRun.durationMs', { ms: String(Math.round(ms)) });
  const s = Math.round(ms / 1000);
  if (s < 120) return t('backupDr.latestRun.durationSec', { s: String(s) });
  const m = Math.floor(s / 60);
  const rs = s % 60;
  return t('backupDr.latestRun.durationMin', { m: String(m), s: String(rs) });
}

/** İnsan okunur RPO/RTO tahmini (saniye → kısa metin). */
export function formatBackupAgeSeconds(
  seconds: number | undefined,
  t: (key: string, options?: Record<string, string | number>) => string
): string {
  if (seconds === undefined || Number.isNaN(seconds)) return '—';
  if (seconds < 60) {
    return t('backupDr.monitoring.rpoRto.ageSeconds', { s: String(Math.round(seconds)) });
  }
  if (seconds < 3600) {
    return t('backupDr.monitoring.rpoRto.ageMinutes', { m: String(Math.round(seconds / 60)) });
  }
  if (seconds < 86400) {
    return t('backupDr.monitoring.rpoRto.ageHours', { h: String(Math.round(seconds / 3600)) });
  }
  return t('backupDr.monitoring.rpoRto.ageDays', { d: String(Math.round(seconds / 86400)) });
}
