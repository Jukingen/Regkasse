import { customInstance } from '@/lib/axios';

export type ApprovalRequestDto = {
  id: string;
  tenantId?: string | null;
  tenantName?: string | null;
  tenantSlug?: string | null;
  requestedBy: string;
  requestedByEmail?: string | null;
  requestedByDisplayName?: string | null;
  approvedBy?: string | null;
  approvedByEmail?: string | null;
  approvedByDisplayName?: string | null;
  actionType: string;
  payload?: string | null;
  status: string;
  requestedAt: string;
  approvedAt?: string | null;
  expiresAt: string;
  reason?: string | null;
  notes?: string | null;
  pathHint?: string | null;
  timeToDecisionMinutes?: number | null;
};

export type ApprovalActionTypeCount = {
  actionType: string;
  count: number;
  approvedCount: number;
  rejectedCount: number;
};

export type ApprovalHistoryReport = {
  fromUtc: string;
  toUtc: string;
  totalRequests: number;
  pendingCount: number;
  approvedCount: number;
  rejectedCount: number;
  expiredCount: number;
  consumedCount: number;
  averageTimeToApprovalMinutes?: number | null;
  medianTimeToApprovalMinutes?: number | null;
  byActionType: ApprovalActionTypeCount[];
  recent: ApprovalRequestDto[];
};

export type ApprovalMutationResult = {
  succeeded: boolean;
  requestId?: string | null;
  approvalToken?: string | null;
  headerName?: string;
  errorCode?: string | null;
  message?: string | null;
};

export async function getPendingApprovals(): Promise<ApprovalRequestDto[]> {
  return customInstance<ApprovalRequestDto[]>({
    url: '/api/admin/approvals/pending',
    method: 'GET',
  });
}

export async function getApprovalHistory(params?: {
  tenantId?: string;
  status?: string;
  actionType?: string;
  fromUtc?: string;
  toUtc?: string;
  limit?: number;
  offset?: number;
}): Promise<ApprovalRequestDto[]> {
  return customInstance<ApprovalRequestDto[]>({
    url: '/api/admin/approvals/history',
    method: 'GET',
    params,
  });
}

export async function getApprovalHistoryReport(params?: {
  tenantId?: string;
  fromUtc?: string;
  toUtc?: string;
}): Promise<ApprovalHistoryReport> {
  return customInstance<ApprovalHistoryReport>({
    url: '/api/admin/approvals/history/report',
    method: 'GET',
    params,
  });
}

export async function getApproval(requestId: string): Promise<ApprovalRequestDto> {
  return customInstance<ApprovalRequestDto>({
    url: `/api/admin/approvals/${requestId}`,
    method: 'GET',
  });
}

export async function approveAction(
  requestId: string,
  notes?: string
): Promise<ApprovalMutationResult> {
  return customInstance<ApprovalMutationResult>({
    url: `/api/admin/approvals/${requestId}/approve`,
    method: 'POST',
    data: { notes },
  });
}

export async function rejectAction(
  requestId: string,
  notes?: string
): Promise<ApprovalMutationResult> {
  return customInstance<ApprovalMutationResult>({
    url: `/api/admin/approvals/${requestId}/reject`,
    method: 'POST',
    data: { notes },
  });
}

export async function requestCriticalApproval(body: {
  actionType: string;
  tenantId?: string | null;
  pathHint?: string;
  payload?: string;
  reason?: string;
}): Promise<ApprovalMutationResult> {
  return customInstance<ApprovalMutationResult>({
    url: '/api/admin/approvals',
    method: 'POST',
    data: body,
  });
}

export async function claimApprovalToken(requestId: string): Promise<ApprovalMutationResult> {
  return customInstance<ApprovalMutationResult>({
    url: `/api/admin/approvals/${requestId}/claim`,
    method: 'POST',
  });
}
