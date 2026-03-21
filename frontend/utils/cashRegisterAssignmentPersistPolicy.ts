/**
 * Rules for optimistic cash-register assignment after PUT /api/user/settings/cash-register fails.
 * Backend rejects invalid/closed/forbidden registers with 4xx + optional code; infra errors often lack status.
 */

import { isValidPosCashRegisterId } from './posCashRegister';
import { POS_CASH_REGISTER_CODES } from './posRegisterGateCopy';

const DOMAIN_CODES = new Set<string>(Object.values(POS_CASH_REGISTER_CODES));

export type ApiLikeError = {
  status?: number;
  data?: unknown;
  message?: string;
};

export function isApiLikeError(e: unknown): e is ApiLikeError {
  return e != null && typeof e === 'object' && 'status' in e;
}

function readResponseCode(data: unknown): string | undefined {
  if (!data || typeof data !== 'object') return undefined;
  const d = data as Record<string, unknown>;
  const c = d.code ?? d.Code;
  return typeof c === 'string' ? c.trim() : undefined;
}

/**
 * True when the server explicitly rejected the assignment (register policy), not a transient save glitch.
 * Includes 401 — session problems must not be described as “still usable for payment”.
 */
export function isCashRegisterAssignmentRejectedByBackend(error: unknown): boolean {
  if (!isApiLikeError(error)) return false;
  const status = error.status;
  if (status === 401 || status === 403 || status === 404) return true;
  if (status === 400) {
    const code = readResponseCode(error.data);
    if (code && DOMAIN_CODES.has(code)) return true;
    // Other 400s on this endpoint are treated as validation/policy failures.
    return true;
  }
  return false;
}

/**
 * When persist fails but the server did not reject the register, we may keep the optimistic id only if
 * ensure-ready already approved this exact register for the current session (payment gate alignment).
 */
export function shouldRetainOptimisticCashRegisterAfterPersistFailure(
  error: unknown,
  input: {
    nextAction: string | null | undefined;
    effectiveRegisterId: string | null | undefined;
    attemptedRegisterId: string;
  }
): boolean {
  if (isCashRegisterAssignmentRejectedByBackend(error)) return false;
  if (input.nextAction !== 'ready') return false;
  const eff = input.effectiveRegisterId?.trim() ?? '';
  const att = input.attemptedRegisterId.trim();
  if (!isValidPosCashRegisterId(eff) || !isValidPosCashRegisterId(att)) return false;
  return eff.toLowerCase() === att.toLowerCase();
}

const ASSIGNMENT_REJECT_DE: Partial<Record<string, string>> = {
  [POS_CASH_REGISTER_CODES.CLOSED]: 'Diese Kasse ist nicht geöffnet und kann nicht zugewiesen werden.',
  [POS_CASH_REGISTER_CODES.FORBIDDEN]: 'Diese Kasse ist nicht berechtigt oder wird bereits verwendet.',
  [POS_CASH_REGISTER_CODES.INVALID]: 'Ungültige Kassen-ID.',
  [POS_CASH_REGISTER_CODES.NOT_FOUND]: 'Kasse wurde nicht gefunden.',
  [POS_CASH_REGISTER_CODES.REQUIRED]: 'Keine gültige Kasse angegeben.',
  [POS_CASH_REGISTER_CODES.SELECTION_REQUIRED]: 'Bitte wählen Sie eine zulässige Kasse aus.',
};

export function cashRegisterPersistFailureAlertDe(
  error: unknown,
  retainedForSession: boolean
): { title: string; message: string } {
  if (isCashRegisterAssignmentRejectedByBackend(error)) {
    const code = isApiLikeError(error) ? readResponseCode(error.data) : undefined;
    const fallback =
      'Diese Kasse kann nicht zugewiesen werden (geschlossen, nicht berechtigt, belegt oder ungültig). Bitte eine andere Kasse wählen.';
    const byCode = code ? ASSIGNMENT_REJECT_DE[code] : undefined;
    return {
      title: 'Zuweisung abgelehnt',
      message: byCode ?? fallback,
    };
  }
  if (retainedForSession) {
    return {
      title: 'Hinweis',
      message:
        'Die Zuweisung konnte im Profil nicht gespeichert werden. Für diese Sitzung bleibt die gewählte Kasse nur nutzbar, solange der Server sie freigibt (Kassenbereitschaft). Bitte später erneut speichern.',
    };
  }
  return {
    title: 'Fehler',
    message:
      'Die Zuweisung konnte nicht gespeichert werden. Bitte erneut versuchen oder eine andere Kasse wählen.',
  };
}
