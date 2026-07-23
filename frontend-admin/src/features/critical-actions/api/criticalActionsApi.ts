import { customInstance } from '@/lib/axios';
import {
  CRITICAL_ACTION_APPROVAL_HEADER,
  type CriticalActionType,
} from '@/lib/criticalAction';

export type CriticalActionApprovalTokenResponse = {
  approvalToken?: string;
  headerName?: string;
  requestId?: string;
};

/** Exchange a TOTP / Dev bypass code for a single-use approval token. */
export async function issueCriticalActionApprovalWith2fa(params: {
  actionType: CriticalActionType;
  pathHint: string;
  twoFactorCode: string;
}): Promise<CriticalActionApprovalTokenResponse> {
  return customInstance<CriticalActionApprovalTokenResponse>({
    url: '/api/admin/critical-actions/approve-with-2fa',
    method: 'POST',
    data: {
      actionType: params.actionType,
      pathHint: params.pathHint,
      twoFactorCode: params.twoFactorCode,
    },
  });
}

/** Ask Super Admin to approve a critical action (async). */
export async function requestCriticalActionSuperAdminApproval(params: {
  actionType: CriticalActionType;
  pathHint: string;
  reason?: string;
}): Promise<CriticalActionApprovalTokenResponse> {
  return customInstance<CriticalActionApprovalTokenResponse>({
    url: '/api/admin/critical-actions/request-approval',
    method: 'POST',
    data: {
      actionType: params.actionType,
      pathHint: params.pathHint,
      reason: params.reason,
    },
  });
}

/** Headers to attach when calling a gated critical endpoint. */
export function criticalActionApprovalHeaders(approvalToken: string): Record<string, string> {
  return {
    [CRITICAL_ACTION_APPROVAL_HEADER]: approvalToken,
  };
}
