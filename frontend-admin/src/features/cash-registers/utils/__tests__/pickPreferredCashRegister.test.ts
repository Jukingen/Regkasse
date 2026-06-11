import { describe, expect, it } from 'vitest';

import { pickPreferredCashRegisterId, pickCashRegisterOnTenantSwitch } from '@/features/cash-registers/utils/pickPreferredCashRegister';

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
