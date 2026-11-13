import { customInstance } from '@/lib/axios';

export type UserActivityActionSummary = {
  userCreates: number;
  userEdits: number;
  paymentsProcessed: number;
  stornos: number;
  refunds: number;
  exports: number;
};

export type UserActivityTimelineItem = {
  date: string;
  action: string;
  entityType: string;
  ipAddress?: string | null;
};

export type UserActivityReport = {
  userId: string;
  userName: string;
  email: string;
  role: string;
  tenantName: string;
  lastLoginAt?: string | null;
  lastLoginIp?: string | null;
  totalLogins: number;
  failedLoginAttempts: number;
  activeSessions: number;
  averageSessionDurationMinutes: number;
  lastSessionEndAt?: string | null;
  actionsPerformed: UserActivityActionSummary;
  activityTimeline: UserActivityTimelineItem[];
};

export type UserActivityReportParams = {
  fromDate?: string;
  toDate?: string;
  timelineLimit?: number;
};

export async function fetchUserActivityReport(
  userId: string,
  params?: UserActivityReportParams
): Promise<UserActivityReport> {
  return customInstance<UserActivityReport>({
    url: '/api/admin/reports/user-activity',
    method: 'GET',
    params: {
      userId,
      fromDate: params?.fromDate,
      toDate: params?.toDate,
      timelineLimit: params?.timelineLimit,
    },
  });
}
