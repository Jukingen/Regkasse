/**
 * Local preferences for export/download completion notifications (browser-scoped).
 */

export const DOWNLOAD_NOTIFY_PREFS_KEY = 'regkasse.fa.downloadNotifyPrefs.v1';

export type DownloadNotifyPrefs = {
  /** Master switch: show preparing / success / error panels for exports. */
  notifyOnExports: boolean;
  /** Optional short Web Audio chime on success/error. */
  playSound: boolean;
};

export const DEFAULT_DOWNLOAD_NOTIFY_PREFS: DownloadNotifyPrefs = {
  notifyOnExports: true,
  playSound: false,
};

export function readDownloadNotifyPrefs(): DownloadNotifyPrefs {
  if (typeof window === 'undefined') return { ...DEFAULT_DOWNLOAD_NOTIFY_PREFS };
  try {
    const raw = window.localStorage.getItem(DOWNLOAD_NOTIFY_PREFS_KEY);
    if (!raw) return { ...DEFAULT_DOWNLOAD_NOTIFY_PREFS };
    const parsed = JSON.parse(raw) as Partial<DownloadNotifyPrefs>;
    return {
      notifyOnExports:
        typeof parsed.notifyOnExports === 'boolean'
          ? parsed.notifyOnExports
          : DEFAULT_DOWNLOAD_NOTIFY_PREFS.notifyOnExports,
      playSound:
        typeof parsed.playSound === 'boolean'
          ? parsed.playSound
          : DEFAULT_DOWNLOAD_NOTIFY_PREFS.playSound,
    };
  } catch {
    return { ...DEFAULT_DOWNLOAD_NOTIFY_PREFS };
  }
}

export function writeDownloadNotifyPrefs(patch: Partial<DownloadNotifyPrefs>): DownloadNotifyPrefs {
  const next = { ...readDownloadNotifyPrefs(), ...patch };
  if (typeof window !== 'undefined') {
    try {
      window.localStorage.setItem(DOWNLOAD_NOTIFY_PREFS_KEY, JSON.stringify(next));
    } catch {
      /* restricted storage */
    }
  }
  return next;
}
