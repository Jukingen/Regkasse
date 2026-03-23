/**
 * @deprecated Import from `posRegisterGateCopy` — kept for short-term re-exports.
 */
import type { PosSelectableEmptyReason } from '../services/api/cashRegisterService';
import type { RegisterListFailureKind } from './registerListError';
import {
  type PosRegisterGateContext,
  type RegisterGateReadinessInput,
  buildPosRegisterGateContext,
  registerGateAlertMessage as alertFromCtx,
  registerGateBannerDetail as detailFromCtx,
  registerGateBannerTitle as titleFromCtx,
  registerGateFooterHint as footerFromCtx,
} from './posRegisterGateCopy';

export type { RegisterGateReadinessInput, PosRegisterGateContext };
export { buildPosRegisterGateContext };

function toCtx(
  failureKind: RegisterListFailureKind | null,
  listLoading: boolean,
  picklistCount: number,
  settingsLoadFailed: boolean,
  readiness?: RegisterGateReadinessInput | null,
  registerListEmptyReason: PosSelectableEmptyReason | null = null
): PosRegisterGateContext {
  return buildPosRegisterGateContext({
    settingsLoadFailed,
    registerListFailureKind: failureKind,
    registerListLoading: listLoading,
    registerPicklistCount: picklistCount,
    registerListEmptyReason,
    readiness: readiness ?? undefined,
  });
}

/** @param settingsLoadFailed pass from usePosCashRegisterAssignment */
export function registerGateBannerTitle(
  failureKind: RegisterListFailureKind | null,
  listLoading: boolean,
  picklistCount: number,
  settingsLoadFailed = false,
  readiness: RegisterGateReadinessInput | null = null,
  registerListEmptyReason: PosSelectableEmptyReason | null = null
): string {
  return titleFromCtx(
    toCtx(failureKind, listLoading, picklistCount, settingsLoadFailed, readiness, registerListEmptyReason)
  );
}

export function registerGateBannerDetail(
  failureKind: RegisterListFailureKind | null,
  listLoading: boolean,
  picklistCount: number,
  settingsLoadFailed = false,
  readiness: RegisterGateReadinessInput | null = null,
  registerListEmptyReason: PosSelectableEmptyReason | null = null
): string {
  return detailFromCtx(
    toCtx(failureKind, listLoading, picklistCount, settingsLoadFailed, readiness, registerListEmptyReason)
  );
}

export function registerGateFooterHint(
  failureKind: RegisterListFailureKind | null,
  listLoading: boolean,
  picklistCount: number,
  settingsLoadFailed = false,
  readiness: RegisterGateReadinessInput | null = null,
  registerListEmptyReason: PosSelectableEmptyReason | null = null
): string {
  return footerFromCtx(
    toCtx(failureKind, listLoading, picklistCount, settingsLoadFailed, readiness, registerListEmptyReason)
  );
}

export function registerGateAlertMessage(
  failureKind: RegisterListFailureKind | null,
  picklistCount: number,
  settingsLoadFailed = false,
  readiness: RegisterGateReadinessInput | null = null,
  registerListEmptyReason: PosSelectableEmptyReason | null = null
): string {
  return alertFromCtx(
    toCtx(failureKind, false, picklistCount, settingsLoadFailed, readiness, registerListEmptyReason)
  );
}
