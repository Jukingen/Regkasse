import type { CashRegister } from '@/api/generated/model';
import {
  resolveRegisterLifecycleKind,
  resolveRegisterShiftOccupancy,
} from '@/features/cash-registers/utils/registerOperationalStatus';

function reg(partial: Partial<CashRegister> & Record<string, unknown>): CashRegister {
  return partial as CashRegister;
}

describe('registerOperationalStatus', () => {
  it('treats isActive as lifecycle, not as an open shift', () => {
    const closedActive = reg({ status: 1, isActive: true });
    expect(resolveRegisterLifecycleKind(closedActive)).toBe('active');
    expect(resolveRegisterShiftOccupancy(closedActive)).toBe('none');
  });

  it('maps an open till to shift occupancy open', () => {
    expect(resolveRegisterShiftOccupancy(reg({ status: 2, isActive: true }))).toBe('open');
  });

  it('maps a previous shift to closed occupancy', () => {
    expect(
      resolveRegisterShiftOccupancy(
        reg({ status: 1, isActive: true, lastShiftAtUtc: '2026-09-14T10:00:00Z' })
      )
    ).toBe('closed');
  });

  it('maps maintenance to unavailable occupancy', () => {
    expect(resolveRegisterLifecycleKind(reg({ status: 3, isActive: true }))).toBe('maintenance');
    expect(resolveRegisterShiftOccupancy(reg({ status: 3, isActive: true }))).toBe('unavailable');
  });
});
