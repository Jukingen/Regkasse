import type { CashRegister } from '@/api/generated/model';
import {
  REGISTER_STATUS,
  rawRegisterStatus,
  readStartbelegCreatedAt,
} from '@/features/cash-registers/utils/registerStatus';

/** Inferred operational context when a register is in Closed status. */
export type ClosedRegisterContext = 'neverOpened' | 'afterShift' | 'generic';

const CLOSED_REASON_KEYS = [
  'endOfShift',
  'tagesabschluss',
  'adminAction',
  'neverOpened',
  'decommissionPrep',
] as const;

export type ClosedReasonKey = (typeof CLOSED_REASON_KEYS)[number];

export const CLOSED_REGISTER_REASON_KEYS: readonly ClosedReasonKey[] = CLOSED_REASON_KEYS;

export const RKSV_SHIFT_RULE_KEYS = [
  'schlussbeleg',
  'tagesabschluss',
  'startbeleg',
  'reopenStartbeleg',
  'singleCashier',
] as const;

export type RksvShiftRuleKey = (typeof RKSV_SHIFT_RULE_KEYS)[number];

export function isClosedRegister(register: CashRegister): boolean {
  return rawRegisterStatus(register) === REGISTER_STATUS.closed;
}

export function inferClosedRegisterContext(register: CashRegister): ClosedRegisterContext | null {
  if (!isClosedRegister(register)) {
    return null;
  }

  if (!readStartbelegCreatedAt(register)) {
    return 'neverOpened';
  }

  if (register.lastBalanceUpdate) {
    return 'afterShift';
  }

  return 'generic';
}

export function closedContextMessageKey(context: ClosedRegisterContext): string {
  switch (context) {
    case 'neverOpened':
      return 'cashRegisters.shiftGuidance.detailContextNeverOpened';
    case 'afterShift':
      return 'cashRegisters.shiftGuidance.detailContextAfterShift';
    default:
      return 'cashRegisters.shiftGuidance.detailContextGeneric';
  }
}
