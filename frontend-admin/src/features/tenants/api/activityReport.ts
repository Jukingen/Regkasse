import { AXIOS_INSTANCE } from '@/lib/axios';

export type ActivityReportDateRange = {
  fromUtc: string;
  toUtc: string;
};

export type ActivitySummary = {
  operationType: string;
  count: number;
  users: number;
  firstOccurrence: string;
  lastOccurrence: string;
};

export type ActivityAnomaly = {
  code: string;
  description: string;
  recommendation: string;
  operationType?: string | null;
  severity: string;
};

export type ActivityReport = {
  tenantId: string;
  period: ActivityReportDateRange;
  totalActivities: number;
  uniqueUsers: number;
  activitySummary: ActivitySummary[];
  anomalies: ActivityAnomaly[];
  recommendations: string[];
};

export async function getWeeklyTenantActivityReport(
  tenantId: string
): Promise<ActivityReport> {
  const { data } = await AXIOS_INSTANCE.get<ActivityReport>(
    `/api/admin/tenants/${tenantId}/activity-report/weekly`
  );
  return data;
}
