import { afterEach, describe, expect, it } from '@jest/globals';

import {
  posOfflineBlocksVoucherByMethod,
  posOfflineBlocksVoucherSplitEntry,
} from '../constants/posVoucherOffline';

describe('posOfflineBlocksVoucher helpers', () => {
  const prev = process.env.EXPO_PUBLIC_ENABLE_OFFLINE_GUTSCHEIN;

  afterEach(() => {
    if (prev === undefined) delete process.env.EXPO_PUBLIC_ENABLE_OFFLINE_GUTSCHEIN;
    else process.env.EXPO_PUBLIC_ENABLE_OFFLINE_GUTSCHEIN = prev;
  });

  it('blocks voucher methods when offline by default', () => {
    delete process.env.EXPO_PUBLIC_ENABLE_OFFLINE_GUTSCHEIN;
    expect(posOfflineBlocksVoucherByMethod(false, 'voucher')).toBe(true);
    expect(posOfflineBlocksVoucherByMethod(true, 'voucher')).toBe(false);
    expect(posOfflineBlocksVoucherSplitEntry(false, 5)).toBe(true);
  });

  it('does not block when EXPO_PUBLIC_ENABLE_OFFLINE_GUTSCHEIN is true', () => {
    process.env.EXPO_PUBLIC_ENABLE_OFFLINE_GUTSCHEIN = 'true';
    expect(posOfflineBlocksVoucherByMethod(false, 'voucher')).toBe(false);
    expect(posOfflineBlocksVoucherSplitEntry(false, 5)).toBe(false);
  });
});
