import { customInstance } from '@/lib/axios';

export type GracePeriodRule = {
  actionKind: string;
  duration: string;
  durationSeconds: number;
  requiresApproval: boolean;
};

export type GracePeriodsConfig = {
  enabled: boolean;
  rules: GracePeriodRule[];
};

export type GracePeriodPending = {
  id: string;
  tenantId: string;
  actionKind: string;
  entityType: string;
  entityId: string;
  status: string;
  createdAt: string;
  expiresAt: string;
  canCancel: boolean;
  remainingSeconds: number;
  createdBy?: string | null;
  operationLogId?: string | null;
};

export type ScheduleGracePeriodResponse = {
  success: boolean;
  errorCode?: string | null;
  message?: string | null;
  pending?: GracePeriodPending | null;
};

export async function getGracePeriodsConfig(): Promise<GracePeriodsConfig> {
  return customInstance<GracePeriodsConfig>({
    url: '/api/admin/grace-periods/config',
    method: 'GET',
  });
}

export async function listActiveGracePeriods(): Promise<GracePeriodPending[]> {
  return customInstance<GracePeriodPending[]>({
    url: '/api/admin/grace-periods/active',
    method: 'GET',
  });
}

export async function scheduleGracePeriod(body: {
  actionKind: string;
  entityType: string;
  entityId: string;
  reason?: string;
  payload?: string;
}): Promise<ScheduleGracePeriodResponse> {
  return customInstance<ScheduleGracePeriodResponse>({
    url: '/api/admin/grace-periods',
    method: 'POST',
    data: body,
  });
}

export async function cancelGracePeriod(
  id: string,
  reason?: string
): Promise<ScheduleGracePeriodResponse> {
  return customInstance<ScheduleGracePeriodResponse>({
    url: `/api/admin/grace-periods/${id}/cancel`,
    method: 'POST',
    data: { reason },
  });
}
