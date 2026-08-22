import { POS_CASH_REGISTER_SELECT_HREF } from './posCashRegister';

/** Machine-readable codes from POST /api/pos/shift/auto-open (ShiftAutoOpenCodes). */
export const SHIFT_AUTO_OPEN_CODES = {
  SUCCESS: 'SUCCESS',
  OK: 'SUCCESS',
  NEED_REGISTER_SELECTION: 'NEED_REGISTER_SELECTION',
  REGISTER_UNAVAILABLE: 'REGISTER_UNAVAILABLE',
  REGISTER_NOT_FOUND: 'REGISTER_NOT_FOUND',
  SHIFT_ALREADY_OPEN: 'SHIFT_ALREADY_OPEN',
  REGISTER_DECOMMISSIONED: 'REGISTER_DECOMMISSIONED',
  NO_ACTIVE_REGISTERS: 'NO_ACTIVE_REGISTERS',
  UNKNOWN_ERROR: 'UNKNOWN_ERROR',
} as const;

export type ShiftAutoOpenCode =
  (typeof SHIFT_AUTO_OPEN_CODES)[keyof typeof SHIFT_AUTO_OPEN_CODES];

export class ShiftAutoOpenError extends Error {
  readonly code: string;
  readonly httpStatus?: number;

  constructor(code: string, message: string, httpStatus?: number) {
    super(message);
    this.name = 'ShiftAutoOpenError';
    this.code = code;
    this.httpStatus = httpStatus;
  }
}

function isRecord(v: unknown): v is Record<string, unknown> {
  return v != null && typeof v === 'object' && !Array.isArray(v);
}

export function readShiftAutoOpenCode(raw: unknown): string | null {
  if (!isRecord(raw)) return null;
  const code = raw.code ?? raw.Code;
  return typeof code === 'string' && code.trim() ? code.trim() : null;
}

export function readShiftAutoOpenMessage(raw: unknown): string | null {
  if (!isRecord(raw)) return null;
  const message = raw.message ?? raw.Message ?? raw.error ?? raw.Error;
  return typeof message === 'string' && message.trim() ? message.trim() : null;
}

export function extractShiftAutoOpenErrorBody(error: unknown): Record<string, unknown> | null {
  if (!isRecord(error)) return null;
  const data = error.data ?? error.response;
  if (isRecord(data)) {
    if (isRecord(data.data)) return data.data;
    return data;
  }
  return error;
}

export function parseShiftAutoOpenError(error: unknown): ShiftAutoOpenError {
  if (error instanceof ShiftAutoOpenError) return error;
  const body = extractShiftAutoOpenErrorBody(error);
  const code = readShiftAutoOpenCode(body) ?? SHIFT_AUTO_OPEN_CODES.UNKNOWN_ERROR;
  const message =
    readShiftAutoOpenMessage(body) ??
    (error instanceof Error ? error.message : 'Die Schicht konnte nicht geöffnet werden.');
  const httpStatus = isRecord(error)
    ? typeof error.status === 'number'
      ? error.status
      : isRecord(error.response) && typeof error.response.status === 'number'
        ? error.response.status
        : undefined
    : undefined;
  return new ShiftAutoOpenError(code, message, httpStatus);
}

/** Stale default register must not keep the cashier off the picker. */
export function shouldClearPosRegisterAssignment(code: string): boolean {
  return (
    code === SHIFT_AUTO_OPEN_CODES.NEED_REGISTER_SELECTION ||
    code === SHIFT_AUTO_OPEN_CODES.REGISTER_UNAVAILABLE ||
    code === SHIFT_AUTO_OPEN_CODES.REGISTER_NOT_FOUND ||
    code === SHIFT_AUTO_OPEN_CODES.REGISTER_DECOMMISSIONED ||
    code === SHIFT_AUTO_OPEN_CODES.NO_ACTIVE_REGISTERS
  );
}

export function shiftAutoOpenNavigateHref(
  code: string
): typeof POS_CASH_REGISTER_SELECT_HREF | null {
  if (shouldClearPosRegisterAssignment(code) || code === SHIFT_AUTO_OPEN_CODES.NEED_REGISTER_SELECTION) {
    return POS_CASH_REGISTER_SELECT_HREF;
  }
  return null;
}

export function shiftAutoOpenAlertI18nKeys(code: string): {
  titleKey: string;
  messageKey: string;
} {
  switch (code) {
    case SHIFT_AUTO_OPEN_CODES.NEED_REGISTER_SELECTION:
      return {
        titleKey: 'shift:alerts.needRegisterTitle',
        messageKey: 'shift:errors.needRegisterSelection',
      };
    case SHIFT_AUTO_OPEN_CODES.REGISTER_NOT_FOUND:
      return {
        titleKey: 'shift:alerts.registerNotFoundTitle',
        messageKey: 'shift:errors.registerNotFound',
      };
    case SHIFT_AUTO_OPEN_CODES.REGISTER_UNAVAILABLE:
      return {
        titleKey: 'shift:alerts.registerUnavailableTitle',
        messageKey: 'shift:errors.registerUnavailable',
      };
    case SHIFT_AUTO_OPEN_CODES.REGISTER_DECOMMISSIONED:
      return {
        titleKey: 'shift:alerts.registerDecommissionedTitle',
        messageKey: 'shift:errors.registerDecommissioned',
      };
    case SHIFT_AUTO_OPEN_CODES.NO_ACTIVE_REGISTERS:
      return {
        titleKey: 'shift:alerts.noActiveRegistersTitle',
        messageKey: 'shift:errors.noActiveRegisters',
      };
    case SHIFT_AUTO_OPEN_CODES.SHIFT_ALREADY_OPEN:
      return {
        titleKey: 'shift:alerts.shiftAlreadyOpenTitle',
        messageKey: 'shift:errors.shiftAlreadyOpen',
      };
    default:
      return {
        titleKey: 'shift:alerts.unknownTitle',
        messageKey: 'shift:errors.unknown',
      };
  }
}
