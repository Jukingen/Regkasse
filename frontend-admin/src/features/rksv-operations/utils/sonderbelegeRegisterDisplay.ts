import type { EnhancedCashRegister } from '@/features/cash-registers/types/enhancedCashRegister';
import {
  REGISTER_STATUS,
  rawRegisterStatus,
} from '@/features/cash-registers/utils/registerStatus';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';

export type SonderbelegeRegisterStatusKey =
  | 'active'
  | 'closed'
  | 'maintenance'
  | 'disabled'
  | 'decommissioned'
  | 'unknown';

export type SonderbelegeStatusVisual = {
  key: SonderbelegeRegisterStatusKey;
  /** Ant Design Tag color */
  color: string;
  emoji: string;
};

/** Map backend RegisterStatus → Sonderbelege display status (Active = Open). */
export function resolveSonderbelegeStatusKey(
  status: number | undefined
): SonderbelegeRegisterStatusKey {
  switch (status) {
    case REGISTER_STATUS.open:
      return 'active';
    case REGISTER_STATUS.closed:
      return 'closed';
    case REGISTER_STATUS.maintenance:
      return 'maintenance';
    case REGISTER_STATUS.disabled:
      return 'disabled';
    case REGISTER_STATUS.decommissioned:
      return 'decommissioned';
    default:
      return 'unknown';
  }
}

export function sonderbelegeStatusVisual(
  status: number | undefined
): SonderbelegeStatusVisual {
  const key = resolveSonderbelegeStatusKey(status);
  switch (key) {
    case 'active':
      return { key, color: 'success', emoji: '🟢' };
    case 'decommissioned':
      return { key, color: 'error', emoji: '🔴' };
    case 'closed':
      return { key, color: 'default', emoji: '🟡' };
    case 'maintenance':
      return { key, color: 'warning', emoji: '🟠' };
    case 'disabled':
      return { key, color: 'default', emoji: '⚪' };
    default:
      return { key, color: 'default', emoji: '⚪' };
  }
}

/** Sort: Active (Open) first, then by tenant name, then by register number. */
export function compareSonderbelegeRegisters(
  a: EnhancedCashRegister,
  b: EnhancedCashRegister
): number {
  const aActive = rawRegisterStatus(a) === REGISTER_STATUS.open ? 0 : 1;
  const bActive = rawRegisterStatus(b) === REGISTER_STATUS.open ? 0 : 1;
  if (aActive !== bActive) return aActive - bActive;

  const tenantCmp = resolveTenantLabel(a).localeCompare(resolveTenantLabel(b), 'de', {
    sensitivity: 'base',
  });
  if (tenantCmp !== 0) return tenantCmp;

  return formatRegisterDisplayLabel(a.registerNumber).localeCompare(
    formatRegisterDisplayLabel(b.registerNumber),
    'de',
    { numeric: true, sensitivity: 'base' }
  );
}

export function resolveTenantLabel(reg: EnhancedCashRegister): string {
  return (
    reg.tenantName?.trim() ||
    reg.tenantSlug?.trim() ||
    (reg.tenantId ? String(reg.tenantId) : '') ||
    '—'
  );
}

export function resolveRegisterName(reg: EnhancedCashRegister): string {
  return reg.location?.trim() || formatRegisterDisplayLabel(reg.registerNumber);
}

/**
 * Display: `{registerNumber} - {registerName} ({tenantName})`
 * Example: `001 - Hauptkasse (Dev Tenant)`
 */
export function formatSonderbelegeRegisterLabel(
  reg: EnhancedCashRegister,
  options?: { includeTenant?: boolean }
): string {
  const number = formatRegisterDisplayLabel(reg.registerNumber);
  const name = reg.location?.trim();
  const base = name ? `${number} - ${name}` : number;
  if (options?.includeTenant === false) {
    return base;
  }
  const tenant = resolveTenantLabel(reg);
  if (!tenant || tenant === '—') {
    return base;
  }
  return `${base} (${tenant})`;
}

export type SonderbelegeTenantGroup = {
  tenantId: string;
  tenantName: string;
  tenantSlug: string | null;
  registers: EnhancedCashRegister[];
};

export function groupRegistersByTenant(
  registers: EnhancedCashRegister[]
): SonderbelegeTenantGroup[] {
  const byTenant = new Map<string, SonderbelegeTenantGroup>();

  for (const reg of registers) {
    const tenantId = String(reg.tenantId ?? '');
    if (!tenantId) continue;
    let group = byTenant.get(tenantId);
    if (!group) {
      group = {
        tenantId,
        tenantName: resolveTenantLabel(reg),
        tenantSlug: reg.tenantSlug?.trim() || null,
        registers: [],
      };
      byTenant.set(tenantId, group);
    }
    group.registers.push(reg);
  }

  const groups = [...byTenant.values()];
  for (const group of groups) {
    group.registers.sort(compareSonderbelegeRegisters);
  }
  groups.sort((a, b) => a.tenantName.localeCompare(b.tenantName, 'de', { sensitivity: 'base' }));
  return groups;
}
