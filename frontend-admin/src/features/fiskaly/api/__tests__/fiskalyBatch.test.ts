import { describe, expect, it } from 'vitest';

import {
  asFiskalyBatchProgressEvent,
  clampFiskalyBatchLimits,
  isFiskalyStornoEligible,
  toFiskalyBatchStornoItems,
} from '../fiskalyBatch';

describe('fiskalyBatch helpers', () => {
  it('clamps limits to 1–50', () => {
    expect(clampFiskalyBatchLimits({ maxItems: 200, warnAtItems: 100 })).toEqual({
      maxItems: 50,
      warnAtItems: 50,
    });
    expect(clampFiskalyBatchLimits({ maxItems: 0, warnAtItems: 0 })).toEqual({
      maxItems: 50,
      warnAtItems: 10,
    });
    expect(clampFiskalyBatchLimits({ maxItems: 5, warnAtItems: 10 })).toEqual({
      maxItems: 5,
      warnAtItems: 5,
    });
  });

  it('rejects Sonderbelege and rows without payment or register', () => {
    expect(
      isFiskalyStornoEligible({
        paymentId: 'p1',
        cashRegisterId: 'r1',
        rksvSpecialReceiptKind: null,
      })
    ).toBe(true);
    expect(
      isFiskalyStornoEligible({
        paymentId: 'p1',
        cashRegisterId: 'r1',
        rksvSpecialReceiptKind: 'Startbeleg',
      })
    ).toBe(false);
    expect(isFiskalyStornoEligible({ paymentId: '', cashRegisterId: 'r1' })).toBe(false);
    expect(isFiskalyStornoEligible({ paymentId: 'p1', cashRegisterId: '' })).toBe(false);
  });

  it('deduplicates storno items by payment id', () => {
    const items = toFiskalyBatchStornoItems([
      { paymentId: 'p1', cashRegisterId: 'r1', receiptNumber: 'A' },
      { paymentId: 'p1', cashRegisterId: 'r1', receiptNumber: 'A' },
      { paymentId: 'p2', cashRegisterId: 'r1', receiptNumber: 'B' },
      { paymentId: 'p3', cashRegisterId: 'r1', rksvSpecialReceiptKind: 'Jahresbeleg' },
    ]);
    expect(items).toEqual([
      { cashRegisterId: 'r1', originalReceiptId: 'p1', receiptNumber: 'A' },
      { cashRegisterId: 'r1', originalReceiptId: 'p2', receiptNumber: 'B' },
    ]);
  });

  it('parses batch progress events', () => {
    expect(asFiskalyBatchProgressEvent(null)).toBeNull();
    expect(
      asFiskalyBatchProgressEvent({
        batchId: 'b1',
        kind: 'storno',
        current: 2,
        total: 4,
        currentLabel: 'R-2',
        successCount: 1,
        failedCount: 1,
        done: false,
      })
    ).toMatchObject({
      batchId: 'b1',
      kind: 'storno',
      current: 2,
      total: 4,
      done: false,
    });
  });
});
