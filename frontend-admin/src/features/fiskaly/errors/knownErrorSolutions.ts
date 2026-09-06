export const KNOWN_FISKALY_ERROR_CODES = [
  'E_CLIENT_ERROR',
  'E_SCU_NOT_FOUND',
  'E_RECEIPT_NOT_FOUND',
  'E_UNAUTHORIZED',
] as const;

export type KnownFiskalyErrorCode = (typeof KNOWN_FISKALY_ERROR_CODES)[number];

const SOLUTION_KEYS = {
  E_CLIENT_ERROR: 'tseFiskaly.errors.solutions.E_CLIENT_ERROR',
  E_SCU_NOT_FOUND: 'tseFiskaly.errors.solutions.E_SCU_NOT_FOUND',
  E_RECEIPT_NOT_FOUND: 'tseFiskaly.errors.solutions.E_RECEIPT_NOT_FOUND',
  E_UNAUTHORIZED: 'tseFiskaly.errors.solutions.E_UNAUTHORIZED',
} as const satisfies Record<KnownFiskalyErrorCode, string>;

const TITLE_KEYS = {
  E_CLIENT_ERROR: 'tseFiskaly.errors.solutionTitles.E_CLIENT_ERROR',
  E_SCU_NOT_FOUND: 'tseFiskaly.errors.solutionTitles.E_SCU_NOT_FOUND',
  E_RECEIPT_NOT_FOUND: 'tseFiskaly.errors.solutionTitles.E_RECEIPT_NOT_FOUND',
  E_UNAUTHORIZED: 'tseFiskaly.errors.solutionTitles.E_UNAUTHORIZED',
} as const satisfies Record<KnownFiskalyErrorCode, string>;

export function normalizeFiskalyErrorCode(code?: string | null): KnownFiskalyErrorCode | null {
  if (!code) return null;
  const key = code.trim().toUpperCase().replace(/-/g, '_');
  const prefixed = key.startsWith('E_') ? key : `E_${key}`;
  return (KNOWN_FISKALY_ERROR_CODES as readonly string[]).includes(prefixed)
    ? (prefixed as KnownFiskalyErrorCode)
    : null;
}

export function knownErrorSolutionKey(code?: string | null): (typeof SOLUTION_KEYS)[KnownFiskalyErrorCode] | null {
  const normalized = normalizeFiskalyErrorCode(code);
  return normalized ? SOLUTION_KEYS[normalized] : null;
}

export function knownErrorSolutionTitleKey(code?: string | null): (typeof TITLE_KEYS)[KnownFiskalyErrorCode] | null {
  const normalized = normalizeFiskalyErrorCode(code);
  return normalized ? TITLE_KEYS[normalized] : null;
}
