import { AXIOS_INSTANCE } from '@/lib/axios';

export type DailyReconciliationReport = {
  businessDate: string;
  cashRegisterId?: string;
  registerNumber?: string;
  cashTotal: number;
  cardTotal: number;
  voucherTotal: number;
  otherTotal: number;
  openingBalance: number;
  expectedCash: number;
  actualCash?: number | null;
  cashDifference?: number | null;
  vouchersIssued: number;
  vouchersRedeemed: number;
  vouchersExpired: number;
  isReconciled: boolean;
  reconciledByUserId?: string | null;
  reconciledAtUtc?: string | null;
  notes?: string | null;
  disclaimerDe: string;
};

export type TseContinuityRegisterReport = {
  cashRegisterId: string;
  registerNumber?: string;
  periodStartLocal: string;
  periodEndLocal: string;
  firstSignatureAtUtc?: string | null;
  lastSignatureAtUtc?: string | null;
  signatureCount: number;
  gapsCount: number;
  duplicateCount: number;
  hasGaps: boolean;
  hasDuplicates: boolean;
  maxGapDurationSeconds: number;
  chainBreakCount: number;
  sequenceGapCount: number;
  missingSignatureCount: number;
  receiptsInRange: number;
  detailsExportPath: string;
  detailsExportJsonPath: string;
  breaks: {
    receiptId: string;
    receiptNumber: string;
    createdAtUtc: string;
  }[];
};

export type TseChainContinuityReport = {
  totalReceiptsChecked: number;
  totalSignatureCount: number;
  totalGapsCount: number;
  totalDuplicateCount: number;
  breakCount: number;
  operatorNoteDe: string;
  registers: TseContinuityRegisterReport[];
};

export function buildTseChainExportUrl(detailsPath: string, apiBaseUrl: string): string {
  const base = apiBaseUrl.replace(/\/$/, '');
  const path = detailsPath.startsWith('/') ? detailsPath : `/${detailsPath}`;
  return `${base}${path}`;
}

export type OfflineRecoveryRegisterBreakdown = {
  cashRegisterId: string;
  registerNumber: string;
  pendingCount: number;
  failedCount: number;
};

export type OfflineRecoveryReport = {
  periodStartLocal: string;
  periodEndLocal: string;
  pendingAtStart: number;
  pendingAtEnd: number;
  recoveredSuccessfully: number;
  recoveredWithRetry: number;
  permanentlyFailed: number;
  manuallyIntervened: number;
  averageRecoverySeconds: number;
  maxRecoverySeconds: number;
  byRegister: OfflineRecoveryRegisterBreakdown[];
  pendingCount: number;
  failedCount: number;
  completedCount: number;
  clockDriftWarningCount: number;
  sequenceGapCount: number;
  lastReplayAtUtc?: string | null;
  operatorNoteDe: string;
  recentRows: {
    id: string;
    cashRegisterId: string;
    status: string;
    serverReceivedAtUtc: string;
    lastError?: string | null;
    retryCount?: number;
  }[];
};

export type UserPerformanceRow = {
  userId: string;
  userName: string;
  role: string;
  transactionCount: number;
  totalAmount: number;
  averageTransactionValue: number;
  stornoCount: number;
  stornoRate: number;
  refundCount: number;
  refundRate: number;
  transactionsPerHour: number;
  averageProcessingSeconds: number;
  firstTransactionAtUtc?: string | null;
  lastTransactionAtUtc?: string | null;
  activeHours: number;
};

export type UserPerformanceReport = {
  periodStartLocal: string;
  periodEndLocal: string;
  perUser: UserPerformanceRow[];
  topPerformers: string[];
  highStornoRateWarning: string[];
  highStornoRateThreshold: number;
};

export type PeakHourSlot = { day: number; hour: number; transactionCount: number };

export type PeakHourHeatmapReport = {
  cells?: number[][];
  heatmap?: number[][];
  maxCellCount: number;
  dayTotals?: { dayOfWeek: number; count: number; amount: number }[];
  busiestHour?: PeakHourSlot | null;
  quietestHour?: PeakHourSlot | null;
  averageTransactionsPerHour?: number;
  recommendedStaffingLevels?: { hour: number; suggestedStaff: number }[];
};

export type ProductMovementItem = {
  productId: string;
  productName: string;
  quantitySold: number;
  revenue: number;
  velocityPerDay: number;
};

export type ProductMovementReport = {
  periodStartLocal?: string;
  periodEndLocal?: string;
  topSellingByQuantity?: ProductMovementItem[];
  topSellingByRevenue?: ProductMovementItem[];
  slowMovers?: ProductMovementItem[];
  stockTurnoverRate?: number;
  daysOfInventoryOnHand?: number;
  seasonalTrends?: {
    productId: string;
    productName: string;
    monthlySales: { month: string; quantity: number }[];
  }[];
  lines: { productId: string; productName: string; quantitySold: number; revenue: number }[];
  stockMovements: {
    productId: string;
    productName: string;
    transactionType: string;
    quantity: number;
    transactionDateUtc: string;
  }[];
};

export type AdminReportType =
  | 'DailyReconciliation'
  | 'TseContinuity'
  | 'OfflineRecovery'
  | 'UserPerformance'
  | 'PeakHours'
  | 'ProductMovement';

const adminBase = '/api/admin/reports';
const legacyBase = '/api/Reports/operational';

function normalizePeak(data: PeakHourHeatmapReport): PeakHourHeatmapReport {
  const cells = data.heatmap ?? data.cells ?? [];
  return { ...data, cells, heatmap: cells };
}

export async function fetchDailyReconciliation(params: {
  businessDate?: string;
  cashRegisterId?: string;
}): Promise<DailyReconciliationReport> {
  const res = await AXIOS_INSTANCE.get<DailyReconciliationReport>(
    `${adminBase}/daily-reconciliation`,
    { params }
  );
  return res.data;
}

export async function fetchTseChainContinuity(params: {
  startDate?: string;
  endDate?: string;
  cashRegisterId?: string;
}): Promise<TseChainContinuityReport> {
  const res = await AXIOS_INSTANCE.get<TseChainContinuityReport>(`${adminBase}/tse-continuity`, {
    params,
  });
  return res.data;
}

export async function downloadTseChainExport(
  detailsPath: string,
  format: 'csv' | 'json' = 'csv'
): Promise<Blob> {
  const path = detailsPath.includes('format=')
    ? detailsPath
    : `${detailsPath}${detailsPath.includes('?') ? '&' : '?'}format=${format}`;
  const res = await AXIOS_INSTANCE.get(path, { responseType: 'blob' });
  return res.data as Blob;
}

export async function fetchOfflineRecovery(params: {
  startDate?: string;
  endDate?: string;
  cashRegisterId?: string;
}): Promise<OfflineRecoveryReport> {
  const res = await AXIOS_INSTANCE.get<OfflineRecoveryReport>(`${adminBase}/offline-recovery`, {
    params,
  });
  return res.data;
}

export async function fetchPeakHours(params: {
  startDate?: string;
  endDate?: string;
  cashRegisterId?: string;
}): Promise<PeakHourHeatmapReport> {
  const res = await AXIOS_INSTANCE.get<PeakHourHeatmapReport>(`${adminBase}/peak-hours`, {
    params,
  });
  return normalizePeak(res.data);
}

export async function fetchProductMovement(params: {
  startDate?: string;
  endDate?: string;
}): Promise<ProductMovementReport> {
  const res = await AXIOS_INSTANCE.get<ProductMovementReport>(`${adminBase}/product-movement`, {
    params,
  });
  return res.data;
}

export async function fetchUserPerformance(params: {
  startDate?: string;
  endDate?: string;
  cashRegisterId?: string;
  cashierId?: string;
  paymentMethod?: number;
  activeOnly?: boolean;
}): Promise<UserPerformanceReport> {
  const res = await AXIOS_INSTANCE.get<UserPerformanceReport>(`${adminBase}/user-performance`, {
    params,
  });
  return res.data;
}

const exportRouteByType: Record<AdminReportType, string> = {
  DailyReconciliation: 'daily-reconciliation',
  TseContinuity: 'tse-continuity',
  OfflineRecovery: 'offline-recovery',
  UserPerformance: 'user-performance',
  PeakHours: 'peak-hours',
  ProductMovement: 'product-movement',
};

export async function downloadAdminReportExport(
  reportType: AdminReportType,
  format: 'csv' | 'pdf' | 'json' | 'excel',
  params: {
    startDate?: string;
    endDate?: string;
    businessDate?: string;
    cashRegisterId?: string;
  }
): Promise<Blob> {
  const route = exportRouteByType[reportType];
  const res = await AXIOS_INSTANCE.get(`${adminBase}/${route}/export`, {
    params: { format, ...params },
    responseType: 'blob',
  });
  return res.data as Blob;
}

export async function scheduleOperationalReport(body: {
  reportType: AdminReportType;
  schedule: string;
  recipients: string[];
  format: string;
  filters?: {
    startDate?: string;
    endDate?: string;
    businessDate?: string;
    cashRegisterId?: string;
  };
}): Promise<void> {
  await AXIOS_INSTANCE.post(`${adminBase}/schedule`, body);
}

/** TSE per-register export still uses operational path from API response. */
export { legacyBase };
