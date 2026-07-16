import type { CashRegister } from '@/api/generated/model';
import { isSameViennaCalendarDay } from '@/shared/utils/viennaCalendar';
import {
    inferClosedRegisterContext,
    isClosedRegister,
} from '@/features/cash-registers/utils/registerClosedContext';
import {
    rawRegisterStatus,
    readStartbelegCreatedAt,
    REGISTER_STATUS,
} from '@/features/cash-registers/utils/registerStatus';

export type ClosedSubStatus =
    | 'dailyClosing'
    | 'shiftChange'
    | 'neverOpened'
    | 'licenseExpired'
    | 'generic';

export type CashRegisterStatusBadgeConfig = {
    tagColor: 'success' | 'processing' | 'error' | 'warning' | 'default' | 'blue' | 'orange';
    emoji: string;
    text: string;
    tooltip?: string;
};

type TranslateFn = (key: string, params?: Record<string, string | number>) => string;

export type ResolveCashRegisterStatusBadgeOptions = {
    licenseExpired?: boolean;
};

export function inferClosedSubStatus(
    register: CashRegister,
    options?: ResolveCashRegisterStatusBadgeOptions,
): ClosedSubStatus {
    if (options?.licenseExpired) {
        return 'licenseExpired';
    }

    if (!readStartbelegCreatedAt(register)) {
        return 'neverOpened';
    }

    if (isSameViennaCalendarDay(register.lastBalanceUpdate)) {
        return 'dailyClosing';
    }

    if (inferClosedRegisterContext(register) === 'afterShift' || register.lastBalanceUpdate) {
        return 'shiftChange';
    }

    return 'generic';
}

export function resolveCashRegisterStatusBadge(
    register: CashRegister,
    t: TranslateFn,
    options?: ResolveCashRegisterStatusBadgeOptions,
): CashRegisterStatusBadgeConfig {
    const status = rawRegisterStatus(register);

    if (status === REGISTER_STATUS.open) {
        return {
            tagColor: 'success',
            emoji: '🟢',
            text: t('cashRegisters.statusBadge.open.text'),
            tooltip: t('cashRegisters.statusBadge.open.tooltip'),
        };
    }

    if (status === REGISTER_STATUS.decommissioned) {
        return {
            tagColor: 'error',
            emoji: '🔴',
            text: t('cashRegisters.statusBadge.decommissioned.text'),
            tooltip: t('cashRegisters.statusBadge.decommissioned.tooltip'),
        };
    }

    if (status === REGISTER_STATUS.maintenance) {
        return {
            tagColor: 'warning',
            emoji: '🟡',
            text: t('cashRegisters.status.maintenance'),
            tooltip: t('cashRegisters.statusBadge.maintenance.tooltip'),
        };
    }

    if (status === REGISTER_STATUS.disabled) {
        return {
            tagColor: 'default',
            emoji: '⚪',
            text: t('cashRegisters.status.disabled'),
            tooltip: t('cashRegisters.statusBadge.disabled.tooltip'),
        };
    }

    if (isClosedRegister(register)) {
        const subStatus = inferClosedSubStatus(register, options);
        return {
            tagColor: closedSubStatusTagColor(subStatus),
            emoji: closedSubStatusEmoji(subStatus),
            text: t(`cashRegisters.statusBadge.closed.${subStatus}.text`),
            tooltip: t(`cashRegisters.statusBadge.closed.${subStatus}.tooltip`),
        };
    }

    return {
        tagColor: 'default',
        emoji: '⚪',
        text: t('cashRegisters.status.unknown', { status: String(status ?? '—') }),
    };
}

function closedSubStatusTagColor(
    subStatus: ClosedSubStatus,
): CashRegisterStatusBadgeConfig['tagColor'] {
    switch (subStatus) {
        case 'dailyClosing':
            return 'blue';
        case 'shiftChange':
            return 'orange';
        case 'licenseExpired':
            return 'error';
        case 'neverOpened':
            return 'default';
        default:
            return 'default';
    }
}

function closedSubStatusEmoji(subStatus: ClosedSubStatus): string {
    switch (subStatus) {
        case 'dailyClosing':
            return '🔵';
        case 'shiftChange':
            return '🟠';
        case 'licenseExpired':
            return '⚠️';
        case 'neverOpened':
            return '⚫';
        default:
            return '⚫';
    }
}
