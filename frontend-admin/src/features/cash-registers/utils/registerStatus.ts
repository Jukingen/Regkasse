import type { CashRegister } from '@/api/generated/model';

/** Numeric `RegisterStatus` from API (1=Closed … 5=Decommissioned). */
export function rawRegisterStatus(reg: CashRegister): number | undefined {
    const r = reg as unknown as { status?: number };
    return typeof r.status === 'number' ? r.status : undefined;
}

export const REGISTER_STATUS = {
    closed: 1,
    open: 2,
    maintenance: 3,
    disabled: 4,
    decommissioned: 5,
} as const;

export type RegisterStatusTagColor = 'success' | 'error' | 'warning' | 'default' | 'processing';

export function registerStatusTagColor(status: number | undefined): RegisterStatusTagColor {
    switch (status) {
        case REGISTER_STATUS.open:
            return 'success';
        case REGISTER_STATUS.closed:
            return 'error';
        case REGISTER_STATUS.decommissioned:
            return 'default';
        case REGISTER_STATUS.maintenance:
            return 'warning';
        case REGISTER_STATUS.disabled:
            return 'default';
        default:
            return 'default';
    }
}

export function registerStatusEmoji(status: number | undefined): string {
    switch (status) {
        case REGISTER_STATUS.open:
            return '🟢';
        case REGISTER_STATUS.closed:
            return '🔴';
        case REGISTER_STATUS.decommissioned:
            return '⚫';
        default:
            return '';
    }
}

export function canDecommissionRegister(status: number | undefined): boolean {
    return status === REGISTER_STATUS.closed;
}

export function isDecommissionedRegister(status: number | undefined): boolean {
    return status === REGISTER_STATUS.decommissioned;
}

export type CashRegisterDecommissionMeta = {
    decommissionedAtUtc?: string | null;
    decommissionReason?: string | null;
};

export function readDecommissionMeta(reg: CashRegister): CashRegisterDecommissionMeta {
    const r = reg as unknown as CashRegisterDecommissionMeta;
    return {
        decommissionedAtUtc: r.decommissionedAtUtc ?? null,
        decommissionReason: r.decommissionReason ?? null,
    };
}
