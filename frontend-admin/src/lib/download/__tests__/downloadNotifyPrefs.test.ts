import { describe, expect, it, beforeEach, afterEach } from 'vitest';

import {
  DEFAULT_DOWNLOAD_NOTIFY_PREFS,
  DOWNLOAD_NOTIFY_PREFS_KEY,
  readDownloadNotifyPrefs,
  writeDownloadNotifyPrefs,
} from '@/lib/download/downloadNotifyPrefs';

describe('downloadNotifyPrefs', () => {
  beforeEach(() => {
    window.localStorage.removeItem(DOWNLOAD_NOTIFY_PREFS_KEY);
  });

  afterEach(() => {
    window.localStorage.removeItem(DOWNLOAD_NOTIFY_PREFS_KEY);
  });

  it('returns defaults when empty', () => {
    expect(readDownloadNotifyPrefs()).toEqual(DEFAULT_DOWNLOAD_NOTIFY_PREFS);
  });

  it('persists patches', () => {
    const next = writeDownloadNotifyPrefs({ playSound: true });
    expect(next).toEqual({ notifyOnExports: true, playSound: true });
    expect(readDownloadNotifyPrefs().playSound).toBe(true);

    writeDownloadNotifyPrefs({ notifyOnExports: false });
    expect(readDownloadNotifyPrefs()).toEqual({ notifyOnExports: false, playSound: true });
  });
});
