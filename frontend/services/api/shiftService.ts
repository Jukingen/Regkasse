import { apiClient, API_BASE_URL, resolveTenantFetchHeaders } from './config';
import { sessionManager } from '../session/sessionManager';
import { isRecord, unwrapApiResponseLayer } from './normalizePosPaymentMethods';

export interface CashierShiftDto {
  id: string;
  tenantId: string;
  cashRegisterId: string;
  cashierId: string;
  cashierName: string;
  startBalance: number;
  endBalance: number;
  totalSales: number;
  totalCash: number;
  totalCard: number;
  difference: number;
  startedAt: string;
  endedAt?: string | null;
  status: string;
  notes?: string | null;
}

export interface CurrentShiftResponse {
  hasActiveShift: boolean;
  shift?: CashierShiftDto | null;
}

export interface ShiftClosingReceiptDto {
  shiftId: string;
  cashierName: string;
  registerNumber?: string | null;
  startedAt: string;
  endedAt: string;
  startBalance: number;
  endBalance: number;
  totalSales: number;
  totalCash: number;
  totalCard: number;
  difference: number;
  status: string;
  notes?: string | null;
}

export interface EndShiftResponse {
  shift: CashierShiftDto;
  receipt: ShiftClosingReceiptDto;
}

function readNumber(raw: unknown, fallback = 0): number {
  if (typeof raw === 'number' && Number.isFinite(raw)) return raw;
  if (typeof raw === 'string' && raw.trim() !== '') {
    const n = Number(raw.replace(',', '.'));
    if (Number.isFinite(n)) return n;
  }
  return fallback;
}

function readString(raw: unknown, fallback = ''): string {
  return typeof raw === 'string' ? raw : fallback;
}

export function parseCashierShiftDto(raw: unknown): CashierShiftDto | null {
  if (!isRecord(raw)) return null;
  const id = readString(raw.id ?? raw.Id);
  if (!id) return null;
  return {
    id,
    tenantId: readString(raw.tenantId ?? raw.TenantId),
    cashRegisterId: readString(raw.cashRegisterId ?? raw.CashRegisterId),
    cashierId: readString(raw.cashierId ?? raw.CashierId),
    cashierName: readString(raw.cashierName ?? raw.CashierName),
    startBalance: readNumber(raw.startBalance ?? raw.StartBalance),
    endBalance: readNumber(raw.endBalance ?? raw.EndBalance),
    totalSales: readNumber(raw.totalSales ?? raw.TotalSales),
    totalCash: readNumber(raw.totalCash ?? raw.TotalCash),
    totalCard: readNumber(raw.totalCard ?? raw.TotalCard),
    difference: readNumber(raw.difference ?? raw.Difference),
    startedAt: readString(raw.startedAt ?? raw.StartedAt),
    endedAt: (raw.endedAt ?? raw.EndedAt ?? null) as string | null | undefined,
    status: readString(raw.status ?? raw.Status),
    notes: (raw.notes ?? raw.Notes ?? null) as string | null | undefined,
  };
}

export function parseCurrentShiftResponse(raw: unknown): CurrentShiftResponse {
  const layer = unwrapApiResponseLayer(raw);
  if (!isRecord(layer)) {
    return { hasActiveShift: false };
  }
  const hasActiveShift = layer.hasActiveShift === true || layer.HasActiveShift === true;
  const shiftRaw = layer.shift ?? layer.Shift;
  const shift = shiftRaw != null ? parseCashierShiftDto(shiftRaw) : null;
  return {
    hasActiveShift: hasActiveShift || shift != null,
    shift,
  };
}

export function parseEndShiftResponse(raw: unknown): EndShiftResponse | null {
  const layer = unwrapApiResponseLayer(raw);
  if (!isRecord(layer)) return null;
  const shift = parseCashierShiftDto(layer.shift ?? layer.Shift);
  const receiptRaw = layer.receipt ?? layer.Receipt;
  if (!shift || !isRecord(receiptRaw)) return null;
  return {
    shift,
    receipt: {
      shiftId: readString(receiptRaw.shiftId ?? receiptRaw.ShiftId, shift.id),
      cashierName: readString(receiptRaw.cashierName ?? receiptRaw.CashierName, shift.cashierName),
      registerNumber: (receiptRaw.registerNumber ?? receiptRaw.RegisterNumber ?? null) as string | null,
      startedAt: readString(receiptRaw.startedAt ?? receiptRaw.StartedAt, shift.startedAt),
      endedAt: readString(receiptRaw.endedAt ?? receiptRaw.EndedAt),
      startBalance: readNumber(receiptRaw.startBalance ?? receiptRaw.StartBalance, shift.startBalance),
      endBalance: readNumber(receiptRaw.endBalance ?? receiptRaw.EndBalance, shift.endBalance),
      totalSales: readNumber(receiptRaw.totalSales ?? receiptRaw.TotalSales, shift.totalSales),
      totalCash: readNumber(receiptRaw.totalCash ?? receiptRaw.TotalCash, shift.totalCash),
      totalCard: readNumber(receiptRaw.totalCard ?? receiptRaw.TotalCard, shift.totalCard),
      difference: readNumber(receiptRaw.difference ?? receiptRaw.Difference, shift.difference),
      status: readString(receiptRaw.status ?? receiptRaw.Status, shift.status),
      notes: (receiptRaw.notes ?? receiptRaw.Notes ?? null) as string | null | undefined,
    },
  };
}

export async function fetchCurrentShift(): Promise<CurrentShiftResponse> {
  const raw = await apiClient.get<unknown>('/pos/shift/current');
  return parseCurrentShiftResponse(raw);
}

export async function startShiftApi(cashRegisterId: string, startBalance: number): Promise<CashierShiftDto> {
  const raw = await apiClient.post<unknown>('/pos/shift/start', {
    cashRegisterId,
    startBalance,
  });
  const shift = parseCashierShiftDto(unwrapApiResponseLayer(raw));
  if (!shift) {
    throw new Error('Invalid shift start response');
  }
  return shift;
}

export async function endShiftApi(endBalance: number, notes?: string): Promise<EndShiftResponse> {
  const raw = await apiClient.post<unknown>('/pos/shift/end', {
    endBalance,
    notes: notes?.trim() || undefined,
  });
  const parsed = parseEndShiftResponse(raw);
  if (!parsed) {
    throw new Error('Invalid shift end response');
  }
  return parsed;
}

export interface PosDailyClosingReportDto {
  businessDate: string;
  registerNumber?: string | null;
  totalSales: number;
  totalCash: number;
  totalCard: number;
  cashCount: number;
  difference: number;
  fiscalTotalAmount: number;
  fiscalTotalTaxAmount: number;
  fiscalTransactionCount: number;
  tseSignature?: string | null;
  snapshotDisclaimerDe?: string;
}

export interface PosDailyClosingResult {
  success: boolean;
  errorMessage?: string;
  paymentsWithoutInvoiceCount?: number;
  shift?: CashierShiftDto | null;
  dailyClosingId?: string | null;
  report?: PosDailyClosingReportDto | null;
}

export interface PosDailyClosingStatusDto {
  canClose: boolean;
  hasActiveShift: boolean;
  message: string;
  lastClosingDate?: string | null;
  paymentsWithoutInvoiceCount: number;
}

export class DailyClosingReportPdfError extends Error {
  readonly status: number;

  constructor(status: number, message?: string) {
    super(message ?? `Daily closing PDF HTTP ${status}`);
    this.name = 'DailyClosingReportPdfError';
    this.status = status;
  }
}

export class DailyClosingApiError extends Error {
  paymentsWithoutInvoiceCount?: number;

  constructor(message: string, paymentsWithoutInvoiceCount?: number) {
    super(message);
    this.name = 'DailyClosingApiError';
    this.paymentsWithoutInvoiceCount = paymentsWithoutInvoiceCount;
  }
}

function parsePosDailyClosingReportDto(raw: unknown): PosDailyClosingReportDto | null {
  if (!isRecord(raw)) return null;
  return {
    businessDate: readString(raw.businessDate ?? raw.BusinessDate),
    registerNumber: (raw.registerNumber ?? raw.RegisterNumber ?? null) as string | null,
    totalSales: readNumber(raw.totalSales ?? raw.TotalSales),
    totalCash: readNumber(raw.totalCash ?? raw.TotalCash),
    totalCard: readNumber(raw.totalCard ?? raw.TotalCard),
    cashCount: readNumber(raw.cashCount ?? raw.CashCount),
    difference: readNumber(raw.difference ?? raw.Difference),
    fiscalTotalAmount: readNumber(raw.fiscalTotalAmount ?? raw.FiscalTotalAmount),
    fiscalTotalTaxAmount: readNumber(raw.fiscalTotalTaxAmount ?? raw.FiscalTotalTaxAmount),
    fiscalTransactionCount: readNumber(raw.fiscalTransactionCount ?? raw.FiscalTransactionCount),
    tseSignature: (raw.tseSignature ?? raw.TseSignature ?? null) as string | null,
    snapshotDisclaimerDe: readString(
      raw.snapshotDisclaimerDe ?? raw.SnapshotDisclaimerDe,
      ''
    ),
  };
}

export function parsePosDailyClosingResult(raw: unknown): PosDailyClosingResult {
  const layer = unwrapApiResponseLayer(raw);
  if (!isRecord(layer)) {
    return { success: false, errorMessage: 'Invalid response' };
  }
  const success = layer.success === true || layer.Success === true;
  const shiftRaw = layer.shift ?? layer.Shift;
  const reportRaw = layer.report ?? layer.Report;
  return {
    success,
    errorMessage: readString(layer.errorMessage ?? layer.ErrorMessage ?? layer.error ?? layer.Error, ''),
    paymentsWithoutInvoiceCount: readNumber(
      layer.paymentsWithoutInvoiceCount ?? layer.PaymentsWithoutInvoiceCount,
      0
    ),
    shift: shiftRaw != null ? parseCashierShiftDto(shiftRaw) : null,
    dailyClosingId: readString(layer.dailyClosingId ?? layer.DailyClosingId) || null,
    report: reportRaw != null ? parsePosDailyClosingReportDto(reportRaw) : null,
  };
}

export function parsePosDailyClosingStatus(raw: unknown): PosDailyClosingStatusDto {
  const layer = unwrapApiResponseLayer(raw);
  if (!isRecord(layer)) {
    return {
      canClose: false,
      hasActiveShift: false,
      message: '',
      paymentsWithoutInvoiceCount: 0,
    };
  }
  return {
    canClose: layer.canClose === true || layer.CanClose === true,
    hasActiveShift: layer.hasActiveShift === true || layer.HasActiveShift === true,
    message: readString(layer.message ?? layer.Message),
    lastClosingDate: (layer.lastClosingDate ?? layer.LastClosingDate ?? null) as string | null,
    paymentsWithoutInvoiceCount: readNumber(
      layer.paymentsWithoutInvoiceCount ?? layer.PaymentsWithoutInvoiceCount,
      0
    ),
  };
}

export async function fetchDailyClosingStatus(): Promise<PosDailyClosingStatusDto> {
  const raw = await apiClient.get<unknown>('/pos/shift/daily-closing/status');
  return parsePosDailyClosingStatus(raw);
}

export async function downloadDailyClosingReportPdf(
  dailyClosingId: string,
  language: string
): Promise<Blob> {
  const token = await sessionManager.getAccessToken();
  const lang = encodeURIComponent(language.split('-')[0] || 'de');
  const response = await fetch(
    `${API_BASE_URL}/pos/shift/daily-closing/${encodeURIComponent(dailyClosingId)}/report.pdf?language=${lang}`,
    {
      method: 'GET',
      headers: await resolveTenantFetchHeaders(
        token ? { Authorization: `Bearer ${token}` } : {}
      ),
    }
  );
  if (!response.ok) {
    throw new DailyClosingReportPdfError(
      response.status,
      `Daily closing PDF download failed: ${response.status}`
    );
  }
  return await response.blob();
}

export async function performDailyClosingApi(
  cashCount: number,
  notes?: string
): Promise<PosDailyClosingResult> {
  try {
    const raw = await apiClient.post<unknown>('/pos/shift/daily-closing', {
      cashCount,
      notes: notes?.trim() || undefined,
    });
    const result = parsePosDailyClosingResult(raw);
    if (!result.success) {
      throw new DailyClosingApiError(
        result.errorMessage || 'Daily closing failed',
        result.paymentsWithoutInvoiceCount
      );
    }
    return result;
  } catch (error: unknown) {
    if (error instanceof DailyClosingApiError) throw error;
    const e = error as { response?: { data?: unknown }; data?: unknown } | null;
    const data = (e?.response?.data ?? e?.data) as Record<string, unknown> | undefined;
    const msg = typeof data?.error === 'string' ? data.error : 'Daily closing failed';
    const count =
      typeof data?.paymentsWithoutInvoiceCount === 'number'
        ? data.paymentsWithoutInvoiceCount
        : undefined;
    throw new DailyClosingApiError(msg, count);
  }
}
