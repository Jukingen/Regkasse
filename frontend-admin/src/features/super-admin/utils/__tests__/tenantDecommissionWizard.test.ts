import { describe, expect, it } from 'vitest';

import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import { REGISTER_STATUS } from '@/features/cash-registers/utils/registerStatus';
import {
    areTenantDecommissionChecksSatisfied,
    buildTenantDecommissionPreflightChecks,
    buildTenantDecommissionRegisterSummary,
} from '@/features/super-admin/utils/tenantDecommissionWizard';

function register(status: number): AdminCashRegisterListItem {
    return {
        id: crypto.randomUUID(),
        tenantId: 'tenant-1',
        registerNumber: 'REG-1',
        location: 'Front',
        status,
    };
}

describe('tenantDecommissionWizard', () => {
    it('counts decommission-ready and blocked registers separately', () => {
        const summary = buildTenantDecommissionRegisterSummary([
            register(REGISTER_STATUS.closed),
            register(REGISTER_STATUS.decommissioned),
            register(REGISTER_STATUS.open),
            register(REGISTER_STATUS.maintenance),
        ]);

        expect(summary.total).toBe(4);
        expect(summary.readyForDecommission).toBe(1);
        expect(summary.decommissioned).toBe(1);
        expect(summary.remainingActive).toBe(3);
        expect(summary.blocked).toBe(2);
        expect(summary.open).toBe(1);
        expect(summary.maintenance).toBe(1);
    });

    it('requires manual confirmations and prepared registers before continuing', () => {
        const summary = buildTenantDecommissionRegisterSummary([
            register(REGISTER_STATUS.closed),
            register(REGISTER_STATUS.open),
        ]);

        const checks = buildTenantDecommissionPreflightChecks(summary, {
            noOpenPayments: false,
            dailyClosingDone: true,
            fiscalExportAcknowledged: false,
        });

        expect(checks.map((check) => check.status)).toEqual([
            'failed',
            'passed',
            'failed',
            'warning',
        ]);
        expect(areTenantDecommissionChecksSatisfied(checks)).toBe(false);
    });

    it('allows continuing when only closed or already decommissioned registers remain', () => {
        const summary = buildTenantDecommissionRegisterSummary([
            register(REGISTER_STATUS.closed),
            register(REGISTER_STATUS.decommissioned),
        ]);

        const checks = buildTenantDecommissionPreflightChecks(summary, {
            noOpenPayments: true,
            dailyClosingDone: true,
            fiscalExportAcknowledged: false,
        });

        expect(areTenantDecommissionChecksSatisfied(checks)).toBe(true);
        expect(checks[2]?.status).toBe('passed');
        expect(checks[3]?.status).toBe('warning');
    });
});
