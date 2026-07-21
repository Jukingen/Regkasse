import type { CashRegister } from '@/api/generated/model';
import {
  inferClosedRegisterContext,
  isClosedRegister,
} from '@/features/cash-registers/utils/registerClosedContext';

function reg(
  partial: Partial<CashRegister> & { status?: number; startbelegCreatedAtUtc?: string }
): CashRegister {
  return partial as CashRegister;
}

describe('registerClosedContext', () => {
  it('isClosedRegister returns true only for status 1', () => {
    expect(isClosedRegister(reg({ status: 1 }))).toBe(true);
    expect(isClosedRegister(reg({ status: 2 }))).toBe(false);
  });

  it('inferClosedRegisterContext returns neverOpened without startbeleg', () => {
    expect(inferClosedRegisterContext(reg({ status: 1 }))).toBe('neverOpened');
  });

  it('inferClosedRegisterContext accepts admin Utc startbeleg field', () => {
    expect(
      inferClosedRegisterContext(
        reg({
          status: 1,
          startbelegCreatedAtUtc: '2026-01-01T10:00:00Z',
          lastBalanceUpdate: '2026-01-02T18:00:00Z',
        })
      )
    ).toBe('afterShift');
  });

  it('inferClosedRegisterContext returns afterShift when startbeleg and last balance update exist', () => {
    expect(
      inferClosedRegisterContext(
        reg({
          status: 1,
          startbelegCreatedAt: '2026-01-01T10:00:00Z',
          lastBalanceUpdate: '2026-06-12T18:00:00Z',
        })
      )
    ).toBe('afterShift');
  });

  it('inferClosedRegisterContext returns null for open register', () => {
    expect(inferClosedRegisterContext(reg({ status: 2 }))).toBeNull();
  });
});
