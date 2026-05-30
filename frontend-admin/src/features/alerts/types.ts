export type SuspiciousAlertSeverity = 'Low' | 'Medium' | 'High' | 'Critical';

export type SuspiciousAlertType =
    | 'HighValue'
    | 'MultipleStornos'
    | 'MultipleRefunds'
    | 'UnusualTime'
    | 'SameCardMultiple'
    | 'RapidTransactions';

export type SuspiciousAlertStatus = 'Open' | 'Acknowledged' | 'Dismissed';

export type SuspiciousAlert = {
    id: string;
    type: SuspiciousAlertType;
    severity: SuspiciousAlertSeverity;
    status: SuspiciousAlertStatus;
    paymentId?: string | null;
    customerId?: string | null;
    userId?: string | null;
    message: string;
    suggestedAction?: string | null;
    details?: Record<string, unknown> | null;
    isRead: boolean;
    readAtUtc?: string | null;
    detectedAtUtc: string;
    createdAtUtc: string;
};

export type SuspiciousAlertsListResponse = {
    items: SuspiciousAlert[];
    total: number;
};
