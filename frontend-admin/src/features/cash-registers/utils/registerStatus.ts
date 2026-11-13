import type { CashRegister } from '@/api/generated/model';

const REGISTER_STATUS_BY_NAME: Record<string, number> = {
  closed: 1,
  open: 2,
  maintenance: 3,
  disabled: 4,
  decommissioned: 5,
};

/** Numeric `RegisterStatus` from API (1=Closed … 5=Decommissioned). Accepts the numeric enum and its name. */
export function rawRegisterStatus(
  reg: { status?: number | string | null } | null | undefined
): number | undefined {
  const status = reg?.status;
  if (typeof status === 'number' && Number.isFinite(status)) return status;
  if (typeof status !== 'string') return undefined;
  const trimmed = status.trim();
  if (!trimmed) return undefined;
  const asNumber = Number(trimmed);
  if (Number.isFinite(asNumber)) return asNumber;
  return REGISTER_STATUS_BY_NAME[trimmed.toLowerCase()];
}

/**
 * Admin list DTO uses `startbelegCreatedAtUtc`; entity/OpenAPI CashRegister uses `startbelegCreatedAt`.
 * Read either so status badges and RKSV detail stay consistent across Manager and Super Admin views.
 */
export function readStartbelegCreatedAt(reg: CashRegister | null | undefined): string | null {
  if (!reg) {
    return null;
  }
  const r = reg as unknown as {
    startbelegCreatedAt?: string | null;
    startbelegCreatedAtUtc?: string | null;
  };
  const value = r.startbelegCreatedAt ?? r.startbelegCreatedAtUtc;
  if (typeof value !== 'string') {
    return null;
  }
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
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

/** Schlussbeleg / Decommissioned: the register must not receive new fiscal receipts. */
export function isPermanentlyClosedRegister(
  reg: { status?: number | string | null; decommissionedAtUtc?: string | null } | null | undefined
): boolean {
  if (!reg) return false;
  if (isDecommissionedRegister(rawRegisterStatus(reg))) return true;
  return typeof reg.decommissionedAtUtc === 'string' && reg.decommissionedAtUtc.trim().length > 0;
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
