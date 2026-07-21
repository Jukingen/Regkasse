import { beforeEach, describe, expect, it } from 'vitest';

import {
  FA_QUICK_CASH_REGISTER_STORAGE_KEY,
  readQuickCashRegisterId,
  writeQuickCashRegisterId,
} from '@/features/cash-registers/constants/quickSwitch';

describe('quickSwitch storage', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('stores tenant-scoped register ids separately', () => {
    writeQuickCashRegisterId('reg-a', 'tenant-a');
    writeQuickCashRegisterId('reg-b', 'tenant-b');

    expect(readQuickCashRegisterId('tenant-a')).toBe('reg-a');
    expect(readQuickCashRegisterId('tenant-b')).toBe('reg-b');
  });

  it('migrates legacy global storage into tenant scope once', () => {
    sessionStorage.setItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY, 'legacy-reg');

    expect(readQuickCashRegisterId('tenant-a')).toBe('legacy-reg');
    expect(sessionStorage.getItem(`${FA_QUICK_CASH_REGISTER_STORAGE_KEY}:tenant-a`)).toBe(
      'legacy-reg'
    );
    expect(sessionStorage.getItem(FA_QUICK_CASH_REGISTER_STORAGE_KEY)).toBeNull();
  });

  it('clears tenant-scoped storage', () => {
    writeQuickCashRegisterId('reg-a', 'tenant-a');
    writeQuickCashRegisterId(null, 'tenant-a');

    expect(readQuickCashRegisterId('tenant-a')).toBeNull();
  });
});
