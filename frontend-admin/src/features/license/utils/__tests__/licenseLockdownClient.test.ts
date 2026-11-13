import { describe, expect, it, vi, beforeEach } from 'vitest';

import {
  getLicenseLockdownToastCopy,
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

describe('license lockdown client toasts', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('exposes write-blocked copy keys', () => {
    const copy = getLicenseLockdownToastCopy('de');
    expect(copy.writeBlockedTitle).toMatch(/Schreiboperation/i);
    expect(copy.writeBlockedDescription).toContain('{{operation}}');
  });

  it('notifyLicenseGuardBlocked uses warning toast', () => {
    notifyLicenseGuardBlocked('Produkt erstellen', 'de');
    expect(notifyWarning).toHaveBeenCalled();
    expect(notifyError).not.toHaveBeenCalled();
  });

  it('notifyLicenseWriteBlocked uses error toast', () => {
    notifyLicenseWriteBlocked('Produkt erstellen', 'de');
    expect(notifyError).toHaveBeenCalled();
    const [, opts] = vi.mocked(notifyError).mock.calls[0]!;
    expect(String(opts?.description)).toContain('Produkt erstellen');
  });
});
