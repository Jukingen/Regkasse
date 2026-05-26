import type { AdminCashRegisterListItem } from '@/features/cash-registers/api/cashRegisters';
import {
    canDecommissionRegister,
    isDecommissionedRegister,
    REGISTER_STATUS,
} from '@/features/cash-registers/utils/registerStatus';

export type TenantDecommissionManualChecks = {
    noOpenPayments: boolean;
    dailyClosingDone: boolean;
    fiscalExportAcknowledged: boolean;
};

export type TenantDecommissionCheckStatus = 'passed' | 'failed' | 'warning';

export type TenantDecommissionPreflightCheck = {
    key: 'noOpenPayments' | 'dailyClosingDone' | 'registersPrepared' | 'fiscalExport';
    status: TenantDecommissionCheckStatus;
    blocking: boolean;
    kind: 'manual' | 'automatic' | 'recommended';
};

export type TenantDecommissionRegisterSummary = {
    total: number;
    readyForDecommission: number;
    blocked: number;
    open: number;
    maintenance: number;
    disabled: number;
    decommissioned: number;
    remainingActive: number;
};

function readRegisterStatus(register: Pick<AdminCashRegisterListItem, 'status'>): number | undefined {
    return typeof register.status === 'number' ? register.status : undefined;
}

export function buildTenantDecommissionRegisterSummary(
    registers: AdminCashRegisterListItem[],
): TenantDecommissionRegisterSummary {
    return registers.reduce<TenantDecommissionRegisterSummary>(
        (acc, register) => {
            const status = readRegisterStatus(register);

            if (isDecommissionedRegister(status)) {
                acc.decommissioned += 1;
            } else {
                acc.remainingActive += 1;
            }

            if (canDecommissionRegister(status)) {
                acc.readyForDecommission += 1;
            } else if (!isDecommissionedRegister(status)) {
                acc.blocked += 1;
            }

            switch (status) {
                case REGISTER_STATUS.open:
                    acc.open += 1;
                    break;
                case REGISTER_STATUS.maintenance:
                    acc.maintenance += 1;
                    break;
                case REGISTER_STATUS.disabled:
                    acc.disabled += 1;
                    break;
                default:
                    break;
            }

            return acc;
        },
        {
            total: registers.length,
            readyForDecommission: 0,
            blocked: 0,
            open: 0,
            maintenance: 0,
            disabled: 0,
            decommissioned: 0,
            remainingActive: 0,
        },
    );
}

export function buildTenantDecommissionPreflightChecks(
    summary: TenantDecommissionRegisterSummary,
    manualChecks: TenantDecommissionManualChecks,
): TenantDecommissionPreflightCheck[] {
    const registersPrepared = summary.blocked === 0;

    return [
        {
            key: 'noOpenPayments',
            status: manualChecks.noOpenPayments ? 'passed' : 'failed',
            blocking: true,
            kind: 'manual',
        },
        {
            key: 'dailyClosingDone',
            status: manualChecks.dailyClosingDone ? 'passed' : 'failed',
            blocking: true,
            kind: 'manual',
        },
        {
            key: 'registersPrepared',
            status: registersPrepared ? 'passed' : 'failed',
            blocking: true,
            kind: 'automatic',
        },
        {
            key: 'fiscalExport',
            status: manualChecks.fiscalExportAcknowledged ? 'passed' : 'warning',
            blocking: false,
            kind: 'recommended',
        },
    ];
}

export function areTenantDecommissionChecksSatisfied(
    checks: TenantDecommissionPreflightCheck[],
): boolean {
    return checks.every((check) => !check.blocking || check.status === 'passed');
}
