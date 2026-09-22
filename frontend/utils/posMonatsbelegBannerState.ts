import type { PosCashRegisterContextDto } from './posCashRegisterReadinessParse';
import type { MonatsbelegStatusDto } from '../services/api/rksvSpecialReceiptsService';

export type MonatsbelegBannerTone = 'red' | 'yellow';
export type MonatsbelegBannerBodyKey = 'bodyStrict' | 'bodyGrace' | 'bodyWarning';

export type MonatsbelegBannerState = {
  visible: boolean;
  tone: MonatsbelegBannerTone;
  bodyKey: MonatsbelegBannerBodyKey;
  isHardBlocked: boolean;
  canDismiss: boolean;
  showCreate: boolean;
};

function normalizeMode(raw?: string | null): string {
  return (raw ?? '').trim().toLowerCase();
}

function isMissingPreviousMonth(
  readiness: PosCashRegisterContextDto | null | undefined,
  status: MonatsbelegStatusDto | null | undefined
): boolean {
  if (readiness?.monatsbelegSalesBlocked === true) return true;
  if ((readiness?.nextAction ?? '').trim() === 'monatsbeleg_required') return true;
  if (status?.lastMonthMissing === true) return true;
  if (status?.salesBlocked === true) return true;
  const level = readiness?.monatsbelegWarningLevel ?? status?.warningLevel ?? '';
  if (level === 'yellow' || level === 'red') {
    return (
      readiness?.monatsbelegCanContinueWithWarning === true ||
      status?.canContinueWithWarning === true ||
      status?.requiresAttention === true
    );
  }
  return false;
}

/**
 * Dashboard banner + hard-block decision for previous-month Monatsbeleg.
 * Sales-block flags come from ensure-ready; mode is tenant policy (default Strict).
 */
export function resolveMonatsbelegBannerState(input: {
  readiness: PosCashRegisterContextDto | null | undefined;
  status?: MonatsbelegStatusDto | null;
  canCreate: boolean;
  dismissed: boolean;
}): MonatsbelegBannerState {
  const { readiness, status, canCreate, dismissed } = input;
  const isHardBlocked =
    readiness?.monatsbelegSalesBlocked === true ||
    (readiness?.nextAction ?? '').trim() === 'monatsbeleg_required' ||
    status?.salesBlocked === true;

  const missing = isMissingPreviousMonth(readiness, status);
  if (!missing) {
    return {
      visible: false,
      tone: 'yellow',
      bodyKey: 'bodyWarning',
      isHardBlocked: false,
      canDismiss: false,
      showCreate: false,
    };
  }

  const mode = normalizeMode(readiness?.monatsbelegBlockingMode ?? status?.blockingMode);
  const level = (readiness?.monatsbelegWarningLevel ?? status?.warningLevel ?? '').trim();

  let bodyKey: MonatsbelegBannerBodyKey = 'bodyStrict';
  let tone: MonatsbelegBannerTone = 'red';

  if (isHardBlocked || mode === 'strict' || mode === '') {
    bodyKey = 'bodyStrict';
    tone = 'red';
  } else if (mode === 'graceperiod') {
    bodyKey = 'bodyGrace';
    tone = level === 'red' ? 'red' : 'yellow';
  } else if (mode === 'warningonly') {
    bodyKey = 'bodyWarning';
    tone = 'yellow';
  } else {
    bodyKey = 'bodyStrict';
    tone = isHardBlocked ? 'red' : level === 'red' ? 'red' : 'yellow';
  }

  const canDismiss = !isHardBlocked;
  const visible = canDismiss ? !dismissed : true;

  return {
    visible,
    tone,
    bodyKey,
    isHardBlocked,
    canDismiss,
    showCreate: canCreate,
  };
}
