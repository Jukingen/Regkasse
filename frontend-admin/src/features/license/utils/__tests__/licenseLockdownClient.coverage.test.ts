import { beforeEach, describe, expect, it, vi } from 'vitest';

import { openLicenseRenewalModal } from '@/features/license/stores/licenseRenewalModalStore';
import {
  handleLicenseExpiredForbidden,
  isLicenseExpiredForbiddenPayload,
  notifyLicenseGuardBlocked,
  notifyLicenseWriteBlocked,
} from '@/features/license/utils/licenseLockdownClient';
import { notifyError, notifyWarning } from '@/lib/notificationService';

vi.mock('@/lib/notificationService', () => ({
  notifyError: vi.fn(),
  notifyWarning: vi.fn(),
}));

vi.mock('@/features/license/stores/licenseRenewalModalStore', () => ({
  openLicenseRenewalModal: vi.fn(),
}));

describe('isLicenseExpiredForbiddenPayload', () => {
  it('returns false for empty payloads', () => {
    expect(isLicenseExpiredForbiddenPayload(null)).toBe(false);
    expect(isLicenseExpiredForbiddenPayload({})).toBe(false);
  });

  it('detects known error codes (case-insensitive)', () => {
    expect(isLicenseExpiredForbiddenPayload({ error: 'license_expired' })).toBe(true);
    expect(isLicenseExpiredForbiddenPayload({ Error: 'LICENSE_LOCKED' })).toBe(true);
    expect(isLicenseExpiredForbiddenPayload({ code: 'LICENSE_EXPIRED_WRITE_BLOCKED' })).toBe(
      true
    );
    expect(isLicenseExpiredForbiddenPayload({ code: 'PREFIX_LICENSE_EXPIRED_SUFFIX' })).toBe(
      true
    );
  });

  it('detects message heuristics in EN/DE', () => {
    expect(
      isLicenseExpiredForbiddenPayload({ message: 'The license has expired for this tenant' })
    ).toBe(true);
    expect(
      isLicenseExpiredForbiddenPayload({ Message: 'Die Lizenz ist abgelaufen.' })
    ).toBe(true);
    expect(isLicenseExpiredForbiddenPayload({ message: 'unrelated' })).toBe(false);
  });
});

describe('handleLicenseExpiredForbidden', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('notifies and opens renewal modal by default', () => {
    handleLicenseExpiredForbidden({ locale: 'de' });
    expect(notifyError).toHaveBeenCalled();
    expect(openLicenseRenewalModal).toHaveBeenCalled();
  });

  it('can skip opening the renewal modal', () => {
    handleLicenseExpiredForbidden({ locale: 'de', openRenewalModal: false });
    expect(notifyError).toHaveBeenCalled();
    expect(openLicenseRenewalModal).not.toHaveBeenCalled();
  });
});

describe('guard/write blocked copy without labels', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('uses generic descriptions when labels are blank', () => {
    notifyLicenseGuardBlocked('  ', 'de');
    notifyLicenseWriteBlocked(undefined, 'de');
    expect(notifyWarning).toHaveBeenCalled();
    expect(notifyError).toHaveBeenCalled();
  });
});
