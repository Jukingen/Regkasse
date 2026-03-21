/**
 * Classifies GET /api/pos/cash-register/selectable failures for POS register-gate UX (auth / network).
 */

export type RegisterListFailureKind = 'forbidden' | 'unauthorized' | 'network' | 'unknown';

export function classifyRegisterListError(error: unknown): RegisterListFailureKind {
  const e = error as { status?: number; message?: string; code?: string };
  if (e?.status === 403) return 'forbidden';
  if (e?.status === 401) return 'unauthorized';
  const msg = typeof e?.message === 'string' ? e.message : '';
  if (
    e?.code === 'ECONNABORTED' ||
    e?.code === 'ERR_NETWORK' ||
    /network|timeout|aborted|Network Error/i.test(msg)
  ) {
    return 'network';
  }
  return 'unknown';
}
