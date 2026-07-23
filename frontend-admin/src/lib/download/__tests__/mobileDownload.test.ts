import { afterEach, describe, expect, it, vi } from 'vitest';

import {
  BACKGROUND_DOWNLOAD_MIN_BYTES,
  TOUCH_TARGET_MIN_PX,
  formatMobileFileSize,
  formatMobileSpeed,
  isMobileUserAgent,
  shouldOfferBackgroundDownload,
  touchFriendlyButtonStyle,
} from '@/lib/download/mobileDownload';

describe('mobileDownload', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('TOUCH_TARGET_MIN_PX is 44', () => {
    expect(TOUCH_TARGET_MIN_PX).toBe(44);
    expect(touchFriendlyButtonStyle().minHeight).toBe(44);
    expect(touchFriendlyButtonStyle().minWidth).toBe(44);
  });

  it('isMobileUserAgent detects common mobile UAs', () => {
    expect(isMobileUserAgent('Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X)')).toBe(true);
    expect(isMobileUserAgent('Mozilla/5.0 (Linux; Android 14)')).toBe(true);
    expect(isMobileUserAgent('Mozilla/5.0 (Windows NT 10.0; Win64; x64)')).toBe(false);
  });

  it('formatMobileFileSize prefers MB and never shows raw B for tiny files', () => {
    expect(formatMobileFileSize(0, 'en-US')).toBe('0 MB');
    expect(formatMobileFileSize(500, 'en-US')).toMatch(/MB$/);
    expect(formatMobileFileSize(50 * 1024, 'en-US')).toMatch(/KB$/);
    expect(formatMobileFileSize(2.5 * 1024 * 1024, 'en-US')).toMatch(/2\.5 MB/);
    expect(formatMobileFileSize(2 * 1024 * 1024 * 1024, 'en-US')).toMatch(/GB$/);
    expect(formatMobileFileSize(12345, 'en-US')).not.toMatch(/\d+ B$/);
  });

  it('formatMobileSpeed uses KB/s or MB/s', () => {
    expect(formatMobileSpeed(0, 'en-US')).toBe('0 KB/s');
    expect(formatMobileSpeed(50 * 1024, 'en-US')).toMatch(/KB\/s$/);
    expect(formatMobileSpeed(2.5 * 1024 * 1024, 'en-US')).toMatch(/MB\/s$/);
  });

  it('shouldOfferBackgroundDownload respects threshold', () => {
    expect(shouldOfferBackgroundDownload(1024)).toBe(false);
    expect(shouldOfferBackgroundDownload(BACKGROUND_DOWNLOAD_MIN_BYTES)).toBe(true);
    expect(shouldOfferBackgroundDownload(100, true)).toBe(true);
    expect(shouldOfferBackgroundDownload(null)).toBe(false);
  });
});
