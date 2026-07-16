import type { CashRegister } from '@/api/generated/model';
import {
    inferClosedSubStatus,
    resolveCashRegisterStatusBadge,
} from '@/features/cash-registers/utils/cashRegisterStatusConfig';

const t = (key: string) => key;

function reg(
    partial: Partial<CashRegister> & { status?: number; startbelegCreatedAtUtc?: string },
): CashRegister {
    return partial as CashRegister;
}

describe('cashRegisterStatusConfig', () => {
    it('resolves open register', () => {
        const config = resolveCashRegisterStatusBadge(reg({ status: 2 }), t);
        expect(config.text).toBe('cashRegisters.statusBadge.open.text');
        expect(config.tagColor).toBe('success');
    });

    it('infers neverOpened when startbeleg is missing', () => {
        expect(inferClosedSubStatus(reg({ status: 1 }))).toBe('neverOpened');
    });

    it('does not treat admin Utc startbeleg as neverOpened', () => {
        expect(
            inferClosedSubStatus(
                reg({
                    status: 1,
                    startbelegCreatedAtUtc: '2026-01-01T00:00:00Z',
                    lastBalanceUpdate: '2026-01-02T18:00:00Z',
                }),
            ),
        ).toBe('shiftChange');
    });

    it('infers licenseExpired when option is set', () => {
        expect(inferClosedSubStatus(reg({ status: 1, startbelegCreatedAt: '2026-01-01' }), { licenseExpired: true }))
            .toBe('licenseExpired');
    });

    it('infers shiftChange for closed register with prior activity', () => {
        expect(
            inferClosedSubStatus(
                reg({
                    status: 1,
                    startbelegCreatedAt: '2026-01-01T10:00:00Z',
                    lastBalanceUpdate: '2026-01-02T18:00:00Z',
                }),
            ),
        ).toBe('shiftChange');
    });

    it('resolves decommissioned status separately from closed sub-status', () => {
        const config = resolveCashRegisterStatusBadge(reg({ status: 5 }), t);
        expect(config.text).toBe('cashRegisters.statusBadge.decommissioned.text');
        expect(config.tagColor).toBe('error');
    });
});
