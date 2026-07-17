import { describe, expect, it } from 'vitest';

import { pickPreferredCashRegisterId, pickCashRegisterOnTenantSwitch, pickOperationalCashRegisterId } from '@/features/cash-registers/utils/pickPreferredCashRegister';
import { REGISTER_STATUS } from '@/features/cash-registers/utils/registerStatus';

const tenantA = 'tenant-a';
const regDefault = { id: 'reg-default', tenantId: tenantA, isDefaultForTenant: true };
const regOther = { id: 'reg-other', tenantId: tenantA, isDefaultForTenant: false };
const regForeign = { id: 'reg-foreign', tenantId: 'tenant-b', isDefaultForTenant: true };

describe('pickPreferredCashRegisterId', () => {
    it('returns null for an empty list', () => {
        expect(pickPreferredCashRegisterId([], null, tenantA)).toBeNull();
    });

    it('keeps the current selection when it belongs to the tenant', () => {
        expect(pickPreferredCashRegisterId([regDefault, regOther], 'reg-other', tenantA)).toBe('reg-other');
    });

    it('prefers the tenant default when the stored id belongs to another tenant', () => {
        expect(
            pickPreferredCashRegisterId([regDefault, regOther], 'reg-foreign', tenantA),
        ).toBe('reg-default');
    });

    it('returns null when multiple registers exist without a default and nothing is stored', () => {
        expect(
            pickPreferredCashRegisterId(
                [
                    { id: 'reg-1', tenantId: tenantA, isDefaultForTenant: false },
                    { id: 'reg-2', tenantId: tenantA, isDefaultForTenant: false },
                ],
                null,
                tenantA,
            ),
        ).toBeNull();
    });

    it('scopes by tenant when tenantId is provided', () => {
        expect(
            pickPreferredCashRegisterId([regDefault, regForeign], null, tenantA),
        ).toBe('reg-default');
    });
});

describe('pickCashRegisterOnTenantSwitch', () => {
    it('returns null when tenant has no registers', () => {
        expect(pickCashRegisterOnTenantSwitch([], tenantA)).toBeNull();
    });

    it('selects the sole register automatically', () => {
        expect(
            pickCashRegisterOnTenantSwitch(
                [{ id: 'reg-1', tenantId: tenantA, isDefaultForTenant: false }],
                tenantA,
            ),
        ).toBe('reg-1');
    });

    it('selects the flagged default when multiple registers exist', () => {
        expect(
            pickCashRegisterOnTenantSwitch(
                [
                    { id: 'reg-other', tenantId: tenantA, isDefaultForTenant: false },
                    { id: 'reg-default', tenantId: tenantA, isDefaultForTenant: true },
                ],
                tenantA,
            ),
        ).toBe('reg-default');
    });

    it('returns null when multiple registers exist without a default', () => {
        expect(
            pickCashRegisterOnTenantSwitch(
                [
                    { id: 'reg-1', tenantId: tenantA, isDefaultForTenant: false },
                    { id: 'reg-2', tenantId: tenantA, isDefaultForTenant: false },
                ],
                tenantA,
            ),
        ).toBeNull();
    });
});

describe('pickOperationalCashRegisterId', () => {
    const closed = REGISTER_STATUS.closed;
    const open = REGISTER_STATUS.open;

    it('keeps an open preferred register', () => {
        expect(
            pickOperationalCashRegisterId(
                [
                    { id: 'reg-default', tenantId: tenantA, isDefaultForTenant: true, status: closed },
                    { id: 'reg-open', tenantId: tenantA, isDefaultForTenant: false, status: open },
                ],
                'reg-open',
                tenantA,
            ),
        ).toBe('reg-open');
    });

    it('follows the open POS register when stored preference is closed', () => {
        expect(
            pickOperationalCashRegisterId(
                [
                    { id: 'reg-default', tenantId: tenantA, isDefaultForTenant: true, status: closed },
                    { id: 'reg-open', tenantId: tenantA, isDefaultForTenant: false, status: open },
                ],
                'reg-default',
                tenantA,
            ),
        ).toBe('reg-open');
    });

    it('prefers tenant default among multiple open registers', () => {
        expect(
            pickOperationalCashRegisterId(
                [
                    { id: 'reg-a', tenantId: tenantA, isDefaultForTenant: false, status: open },
                    { id: 'reg-b', tenantId: tenantA, isDefaultForTenant: true, status: open },
                ],
                null,
                tenantA,
            ),
        ).toBe('reg-b');
    });

    it('falls back to first register when nothing is open and no default exists', () => {
        expect(
            pickOperationalCashRegisterId(
                [
                    { id: 'reg-1', tenantId: tenantA, isDefaultForTenant: false, status: closed },
                    { id: 'reg-2', tenantId: tenantA, isDefaultForTenant: false, status: closed },
                ],
                null,
                tenantA,
            ),
        ).toBe('reg-1');
    });
});
