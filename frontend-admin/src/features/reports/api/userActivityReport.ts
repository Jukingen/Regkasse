import { customInstance } from '@/lib/axios';

export type UserActivityActionSummary = {
    userCreates: number;
    userEdits: number;
    paymentsProcessed: number;
    stornos: number;
    refunds: number;
    exports: number;
};

export type UserActivityDailyCount = {
    date: string;
    count: number;
};

export type UserActivityRanking = {
    userId: string;
    userName: string;
    role: string;
    actionCount: number;
};

export type UserActivityTimelineItem = {
    date: string;
    action: string;
    entityType: string;
    entityId?: string | null;
    ipAddress?: string | null;
    status: string;
    sessionId?: string | null;
    correlationId?: string | null;
    description?: string | null;
    tseSignature?: string | null;
};

export type UserActivityReport = {
    userId: string;
    userName: string;
    email: string;
    role: string;
    tenantName: string;
    fromDateUtc: string;
    toDateUtc: string;
    lastLoginAt?: string | null;
    lastLoginIp?: string | null;
    totalLogins: number;
    failedLoginAttempts: number;
    activeSessions: number;
    averageSessionDurationMinutes: number;
    lastSessionEndAt?: string | null;
    totalActions: number;
    actionsPerformed: UserActivityActionSummary;
    dailyActivity: UserActivityDailyCount[];
    topActiveUsers: UserActivityRanking[];
    activityTimeline: UserActivityTimelineItem[];
};

export type UserActivityReportParams = {
    userId: string;
    fromDate?: string;
    toDate?: string;
    actionType?: string;
    includeTimeline?: boolean;
    includeTopUsers?: boolean;
};

export async function fetchUserActivityReport(
    params: UserActivityReportParams,
): Promise<UserActivityReport> {
    return customInstance<UserActivityReport>({
        url: '/api/admin/reports/user-activity',
        method: 'GET',
        params,
    });
}

export async function exportUserActivityReportBlob(
    params: UserActivityReportParams & { format?: string },
): Promise<Blob> {
    return customInstance<Blob>({
        url: '/api/admin/reports/user-activity/export',
        method: 'GET',
        params: {
            userId: params.userId,
            format: params.format ?? 'csv',
            fromDate: params.fromDate,
            toDate: params.toDate,
            actionType: params.actionType,
        },
        responseType: 'blob',
    });
}

export type ScheduleUserActivityReportRequest = {
    userId: string;
    name: string;
    schedule: 'weekly' | 'monthly';
    recipients: string[];
    format?: string;
    fromDate?: string;
    toDate?: string;
    actionType?: string;
};

export async function scheduleUserActivityReport(
    body: ScheduleUserActivityReportRequest,
): Promise<{ scheduleId: string; name: string; nextRunUtc?: string | null }> {
    return customInstance({
        url: '/api/admin/reports/user-activity/schedule',
        method: 'POST',
        data: body,
    });
}
