import { describe, expect, it } from '@jest/globals';

import {
  canShowReceiptStornoButton,
  hasPosReceiptStornoPermission,
  isReceiptStatusStornoable,
  isViennaCalendarToday,
} from '../utils/posReceiptStorno';

const todayIso = new Date().toISOString();

describe('posReceiptStorno', () => {
  it('grants permission for payment.cancel or refund.create', () => {
    expect(hasPosReceiptStornoPermission({ permissions: ['payment.cancel'] })).toBe(true);
    expect(hasPosReceiptStornoPermission({ permissions: ['refund.create'] })).toBe(true);
    expect(hasPosReceiptStornoPermission({ permissions: ['receipt.reprint'] })).toBe(false);
  });

  it('only Paid receipts are stornoable', () => {
    expect(isReceiptStatusStornoable('Paid')).toBe(true);
    expect(isReceiptStatusStornoable('Storno')).toBe(false);
    expect(isReceiptStatusStornoable('Refund')).toBe(false);
    expect(isReceiptStatusStornoable('Nullbeleg')).toBe(false);
  });

  it('hides Storno for already cancelled or other cashiers', () => {
    const cashier = {
      id: 'cashier-1',
      role: 'Cashier',
      permissions: ['payment.cancel'],
    };
    expect(
      canShowReceiptStornoButton(
        { status: 'Storno', grandTotal: 10, cashierId: 'cashier-1', issuedAt: todayIso },
        cashier
      )
    ).toBe(false);
    expect(
      canShowReceiptStornoButton(
        { status: 'Paid', grandTotal: 10, cashierId: 'other', issuedAt: todayIso },
        cashier
      )
    ).toBe(false);
    expect(
      canShowReceiptStornoButton(
        { status: 'Paid', grandTotal: 10, cashierId: 'cashier-1', issuedAt: todayIso },
        cashier
      )
    ).toBe(true);
  });

  it('lets Manager storno another cashiers receipt', () => {
    expect(
      canShowReceiptStornoButton(
        { status: 'Paid', grandTotal: 10, cashierId: 'cashier-1', issuedAt: todayIso },
        { id: 'mgr-1', role: 'Manager', permissions: ['payment.cancel'] }
      )
    ).toBe(true);
  });

  it('treats current instant as Vienna today', () => {
    expect(isViennaCalendarToday(todayIso)).toBe(true);
    expect(isViennaCalendarToday('2020-01-01T10:00:00Z')).toBe(false);
  });
});
