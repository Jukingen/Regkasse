import { apiClient, API_BASE_URL, resolveTenantFetchHeaders } from './config';
import { isRecord, unwrapApiResponseLayer } from './normalizePosPaymentMethods';
import { classifyDailyClosingError } from '../../utils/errorMessages';
import { sessionManager } from '../session/sessionManager';

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
      registerNumber: (receiptRaw.registerNumber ?? receiptRaw.RegisterNumber ?? null) as
        string | null,
      startedAt: readString(receiptRaw.startedAt ?? receiptRaw.StartedAt, shift.startedAt),
      endedAt: readString(receiptRaw.endedAt ?? receiptRaw.EndedAt),
      startBalance: readNumber(
        receiptRaw.startBalance ?? receiptRaw.StartBalance,
        shift.startBalance
      ),
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

export async function startShiftApi(
  cashRegisterId: string,
  startBalance: number
): Promise<CashierShiftDto> {
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

/** Auto-open CashierShift for the assigned register (idempotent). Non-blocking callers should catch. */
export async function autoOpenShiftApi(cashRegisterId: string): Promise<CashierShiftDto> {
  const raw = await apiClient.post<unknown>('/pos/shift/auto-open', {
    cashRegisterId,
  });
  const shift = parseCashierShiftDto(unwrapApiResponseLayer(raw));
  if (!shift) {
    throw new Error('Invalid shift auto-open response');
  }
  return shift;
}

/** Soft-close active CashierShift without closing the register (idempotent). */
export async function autoCloseShiftApi(): Promise<CashierShiftDto | null> {
  const raw = await apiClient.post<unknown>('/pos/shift/auto-close', {});
  if (raw == null || raw === '') return null;
  return parseCashierShiftDto(unwrapApiResponseLayer(raw));
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

export interface TransactionBreakdown {
  cash: number;
  card: number;
  voucher: number;
  cancellations: number;
  total: number;
}

export interface DailyClosingTaxBreakdown {
  grossAt20: number;
  taxAt20: number;
  grossAt10: number;
  taxAt10: number;
  grossAt0: number;
  grossAt13?: number;
  taxAt13?: number;
}

export interface PaymentBreakdown {
  cash: number;
  card: number;
  voucher: number;
  other: number;
  total: number;
}

export interface PosDailyClosingReportDto {
  businessDate: string;
  cashRegisterId?: string | null;
  registerNumber?: string | null;
  companyName?: string | null;
  companyAddress?: string | null;
  companyVatId?: string | null;
  periodStartUtc?: string | null;
  periodEndUtc?: string | null;
  tseProviderLabel?: string | null;
  depExportStatusLabel?: string | null;
  tseSignatureVerified?: boolean;
  hasStartbeleg?: boolean;
  hasMonatsbeleg?: boolean;
  hasJahresbeleg?: boolean;
  cashierName?: string | null;
  totalSales: number;
  totalCash: number;
  totalCard: number;
  totalVoucherRedemptions: number;
  totalOtherPaymentMethods: number;
  cashCount: number;
  difference: number;
  fiscalTotalAmount: number;
  fiscalTotalTaxAmount: number;
  fiscalTotalNetAmount?: number;
  fiscalTransactionCount: number;
  tseSignature?: string | null;
  previousClosingSignature?: string | null;
  taxBreakdown?: DailyClosingTaxBreakdown;
  paymentBreakdown?: PaymentBreakdown;
  isDemoFiscal?: boolean;
  fiscalEnvironment?: string;
  tseStatusLabel?: string;
  tseStatusBadge?: string;
  rksvFooterLabel?: string;
  qrPayload?: string | null;
  snapshotDisclaimerDe?: string;
  salesFiscalReconciliationNote?: string | null;
  differenceScopeNote?: string | null;
  transactionBreakdown?: TransactionBreakdown;
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
  blockReason?: string | null;
  lastClosingDate?: string | null;
  lastClosingPerformedAt?: string | null;
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
  /** Stable POS-facing code for i18n / errorMessages mapper. */
  code: string;
  paymentsWithoutInvoiceCount?: number;
  httpStatus?: number;

  constructor(
    message: string,
    options?: {
      code?: string;
      paymentsWithoutInvoiceCount?: number;
      httpStatus?: number;
    }
  ) {
    super(message);
    this.name = 'DailyClosingApiError';
    this.code = options?.code ?? 'UNKNOWN';
    this.paymentsWithoutInvoiceCount = options?.paymentsWithoutInvoiceCount;
    this.httpStatus = options?.httpStatus;
  }
}

function parseTransactionBreakdown(raw: unknown): TransactionBreakdown {
  if (!isRecord(raw)) {
    return { cash: 0, card: 0, voucher: 0, cancellations: 0, total: 0 };
  }
  return {
    cash: readNumber(raw.cash ?? raw.Cash, 0),
    card: readNumber(raw.card ?? raw.Card, 0),
    voucher: readNumber(raw.voucher ?? raw.Voucher, 0),
    cancellations: readNumber(raw.cancellations ?? raw.Cancellations, 0),
    total: readNumber(raw.total ?? raw.Total, 0),
  };
}

function parseTaxBreakdown(raw: unknown): DailyClosingTaxBreakdown | undefined {
  if (!isRecord(raw)) return undefined;
  return {
    grossAt20: readNumber(raw.grossAt20 ?? raw.GrossAt20, 0),
    taxAt20: readNumber(raw.taxAt20 ?? raw.TaxAt20, 0),
    grossAt10: readNumber(raw.grossAt10 ?? raw.GrossAt10, 0),
    taxAt10: readNumber(raw.taxAt10 ?? raw.TaxAt10, 0),
    grossAt0: readNumber(raw.grossAt0 ?? raw.GrossAt0, 0),
    grossAt13: readNumber(raw.grossAt13 ?? raw.GrossAt13, 0),
    taxAt13: readNumber(raw.taxAt13 ?? raw.TaxAt13, 0),
  };
}

function parsePaymentBreakdown(raw: unknown): PaymentBreakdown | undefined {
  if (!isRecord(raw)) return undefined;
  return {
    cash: readNumber(raw.cash ?? raw.Cash, 0),
    card: readNumber(raw.card ?? raw.Card, 0),
    voucher: readNumber(raw.voucher ?? raw.Voucher, 0),
    other: readNumber(raw.other ?? raw.Other, 0),
    total: readNumber(raw.total ?? raw.Total, 0),
  };
}

function parsePosDailyClosingReportDto(raw: unknown): PosDailyClosingReportDto | null {
  if (!isRecord(raw)) return null;
  return {
    businessDate: readString(raw.businessDate ?? raw.BusinessDate),
    cashRegisterId: readString(raw.cashRegisterId ?? raw.CashRegisterId) || null,
    registerNumber: (raw.registerNumber ?? raw.RegisterNumber ?? null) as string | null,
    companyName: readString(raw.companyName ?? raw.CompanyName) || null,
    companyAddress: readString(raw.companyAddress ?? raw.CompanyAddress) || null,
    companyVatId: readString(raw.companyVatId ?? raw.CompanyVatId) || null,
    periodStartUtc: (raw.periodStartUtc ?? raw.PeriodStartUtc ?? null) as string | null,
    periodEndUtc: (raw.periodEndUtc ?? raw.PeriodEndUtc ?? null) as string | null,
    tseProviderLabel: readString(raw.tseProviderLabel ?? raw.TseProviderLabel) || null,
    depExportStatusLabel: readString(raw.depExportStatusLabel ?? raw.DepExportStatusLabel) || null,
    tseSignatureVerified: raw.tseSignatureVerified === true || raw.TseSignatureVerified === true,
    hasStartbeleg: raw.hasStartbeleg === true || raw.HasStartbeleg === true,
    hasMonatsbeleg: raw.hasMonatsbeleg === true || raw.HasMonatsbeleg === true,
    hasJahresbeleg: raw.hasJahresbeleg === true || raw.HasJahresbeleg === true,
    cashierName: readString(raw.cashierName ?? raw.CashierName) || null,
    totalSales: readNumber(raw.totalSales ?? raw.TotalSales),
    totalCash: readNumber(raw.totalCash ?? raw.TotalCash),
    totalCard: readNumber(raw.totalCard ?? raw.TotalCard),
    totalVoucherRedemptions: readNumber(
      raw.totalVoucherRedemptions ?? raw.TotalVoucherRedemptions,
      0
    ),
    totalOtherPaymentMethods: readNumber(
      raw.totalOtherPaymentMethods ?? raw.TotalOtherPaymentMethods,
      0
    ),
    cashCount: readNumber(raw.cashCount ?? raw.CashCount),
    difference: readNumber(raw.difference ?? raw.Difference),
    fiscalTotalAmount: readNumber(raw.fiscalTotalAmount ?? raw.FiscalTotalAmount),
    fiscalTotalTaxAmount: readNumber(raw.fiscalTotalTaxAmount ?? raw.FiscalTotalTaxAmount),
    fiscalTotalNetAmount: readNumber(raw.fiscalTotalNetAmount ?? raw.FiscalTotalNetAmount, 0),
    fiscalTransactionCount: readNumber(raw.fiscalTransactionCount ?? raw.FiscalTransactionCount),
    tseSignature: (raw.tseSignature ?? raw.TseSignature ?? null) as string | null,
    previousClosingSignature: (raw.previousClosingSignature ??
      raw.PreviousClosingSignature ??
      null) as string | null,
    taxBreakdown: parseTaxBreakdown(raw.taxBreakdown ?? raw.TaxBreakdown),
    paymentBreakdown: parsePaymentBreakdown(raw.paymentBreakdown ?? raw.PaymentBreakdown),
    isDemoFiscal: raw.isDemoFiscal === true || raw.IsDemoFiscal === true,
    fiscalEnvironment: readString(raw.fiscalEnvironment ?? raw.FiscalEnvironment) || undefined,
    tseStatusLabel: readString(raw.tseStatusLabel ?? raw.TseStatusLabel) || undefined,
    tseStatusBadge: readString(raw.tseStatusBadge ?? raw.TseStatusBadge) || undefined,
    rksvFooterLabel: readString(raw.rksvFooterLabel ?? raw.RksvFooterLabel) || undefined,
    qrPayload: (raw.qrPayload ?? raw.QrPayload ?? null) as string | null,
    snapshotDisclaimerDe: readString(raw.snapshotDisclaimerDe ?? raw.SnapshotDisclaimerDe, ''),
    salesFiscalReconciliationNote: (raw.salesFiscalReconciliationNote ??
      raw.SalesFiscalReconciliationNote ??
      null) as string | null,
    differenceScopeNote: (raw.differenceScopeNote ?? raw.DifferenceScopeNote ?? null) as
      string | null,
    transactionBreakdown: parseTransactionBreakdown(
      raw.transactionBreakdown ?? raw.TransactionBreakdown
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
    errorMessage: readString(
      layer.errorMessage ?? layer.ErrorMessage ?? layer.error ?? layer.Error,
      ''
    ),
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
    blockReason: readString(layer.blockReason ?? layer.BlockReason) || null,
    lastClosingDate: (layer.lastClosingDate ?? layer.LastClosingDate ?? null) as string | null,
    lastClosingPerformedAt: (layer.lastClosingPerformedAt ??
      layer.LastClosingPerformedAt ??
      null) as string | null,
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
      headers: await resolveTenantFetchHeaders(token ? { Authorization: `Bearer ${token}` } : {}),
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
      const technical = result.errorMessage || 'Daily closing failed';
      const code = classifyDailyClosingError({
        message: technical,
        paymentsWithoutInvoiceCount: result.paymentsWithoutInvoiceCount,
        httpStatus: 400,
      });
      throw new DailyClosingApiError(technical, {
        code,
        paymentsWithoutInvoiceCount: result.paymentsWithoutInvoiceCount,
        httpStatus: 400,
      });
    }
    return result;
  } catch (error: unknown) {
    if (error instanceof DailyClosingApiError) throw error;
    const e = error as {
      response?: { data?: unknown; status?: number };
      data?: unknown;
      status?: number;
      message?: string;
      code?: string;
    } | null;
    const data = (e?.response?.data ?? e?.data) as Record<string, unknown> | undefined;
    const msg =
      (typeof data?.error === 'string' && data.error) ||
      (typeof data?.message === 'string' && data.message) ||
      (typeof e?.message === 'string' && e.message) ||
      'Daily closing failed';
    const count =
      typeof data?.paymentsWithoutInvoiceCount === 'number'
        ? data.paymentsWithoutInvoiceCount
        : undefined;
    const httpStatus = e?.response?.status ?? e?.status;
    const code = classifyDailyClosingError({
      message: msg,
      httpStatus,
      axiosCode: typeof e?.code === 'string' ? e.code : null,
      paymentsWithoutInvoiceCount: count,
      code: typeof data?.code === 'string' ? data.code : null,
      blockReason: typeof data?.blockReason === 'string' ? data.blockReason : null,
    });
    throw new DailyClosingApiError(msg, {
      code,
      paymentsWithoutInvoiceCount: count,
      httpStatus,
    });
  }
}
