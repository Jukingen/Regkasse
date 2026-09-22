import { apiClient } from './config';
import { getViennaYearMonth } from '../../utils/resolvePosMonatsbelegTarget';

export { getViennaYearMonth };

/** POST /api/rksv/special-receipts/startbeleg */
export type CreateStartbelegRequest = {
  cashRegisterId: string;
  correlationId?: string | null;
  reason?: string | null;
};

export type CreateStartbelegResponse = {
  paymentId: string;
  invoiceId: string;
  receiptId: string;
  receiptNumber: string;
  qrData: string;
};

export async function postCreateStartbeleg(
  body: CreateStartbelegRequest
): Promise<CreateStartbelegResponse> {
  return await apiClient.post<CreateStartbelegResponse>('/rksv/special-receipts/startbeleg', body);
}

/** POST /api/rksv/special-receipts/monatsbeleg */
export type CreateMonatsbelegRequest = {
  cashRegisterId: string;
  year: number;
  month: number;
  reason?: string | null;
  /** Query only — stripped from JSON body. Past Vienna months require `force=true`. */
  force?: boolean;
};

export type CreateMonatsbelegResponse = {
  paymentId: string;
  invoiceId: string;
  receiptId: string;
  receiptNumber: string;
  qrData: string;
};

export async function postCreateMonatsbeleg(
  body: CreateMonatsbelegRequest
): Promise<CreateMonatsbelegResponse> {
  const { force, ...payload } = body;
  const path =
    force === true
      ? '/rksv/special-receipts/monatsbeleg?force=true'
      : '/rksv/special-receipts/monatsbeleg';
  return await apiClient.post<CreateMonatsbelegResponse>(path, payload);
}

/** POST /api/rksv/special-receipts/jahresbeleg */
export type CreateJahresbelegRequest = {
  cashRegisterId: string;
  year: number;
  reason?: string | null;
  earlyReason?: string | null;
};

export type CreateJahresbelegResponse = {
  paymentId: string;
  invoiceId: string;
  receiptId: string;
  receiptNumber: string;
  qrData: string;
};

export async function postCreateJahresbeleg(
  body: CreateJahresbelegRequest
): Promise<CreateJahresbelegResponse> {
  return await apiClient.post<CreateJahresbelegResponse>(
    '/rksv/special-receipts/jahresbeleg',
    body
  );
}

/** POST /api/rksv/special-receipts/nullbeleg */
export type CreateNullbelegRequest = {
  cashRegisterId: string;
  year?: number | null;
  month?: number | null;
  reason?: string | null;
};

export type CreateNullbelegResponse = {
  paymentId: string;
  invoiceId: string;
  receiptId: string;
  receiptNumber: string;
};

export async function postCreateNullbeleg(
  body: CreateNullbelegRequest
): Promise<CreateNullbelegResponse> {
  return await apiClient.post<CreateNullbelegResponse>('/rksv/special-receipts/nullbeleg', body);
}

/** POST /api/rksv/special-receipts/schlussbeleg */
export type CreateSchlussbelegRequest = {
  cashRegisterId: string;
  reason?: string | null;
};

export type CreateSchlussbelegResponse = {
  paymentId: string;
  invoiceId: string;
  receiptId: string;
  receiptNumber: string;
  qrData: string;
};

export async function postCreateSchlussbeleg(
  body: CreateSchlussbelegRequest
): Promise<CreateSchlussbelegResponse> {
  return await apiClient.post<CreateSchlussbelegResponse>(
    '/rksv/special-receipts/schlussbeleg',
    body
  );
}

/** GET /api/rksv/monatsbeleg/status/{cashRegisterId} — matches backend `MonatsbelegStatusDto` (camelCase JSON). */
export type MissingMonthDto = {
  year: number;
  month: number;
  isOverdue: boolean;
  /** ISO date string (Vienna legal deadline). */
  deadline: string;
};

export type MonatsbelegStatusDto = {
  lastCompletedMonth: string | null;
  nextRequiredMonth: string | null;
  missingMonths: MissingMonthDto[];
  requiresAttention: boolean;
  totalMissingCount: number;
  isRequired: boolean;
  daysUntilDeadline: number;
  lastMonatsbelegDate: string | null;
  warningLevel: string;
  currentMonthExists: boolean;
  lastMonthExists: boolean;
  currentMonthOverdue: boolean;
  lastMonthMissing: boolean;
  warningMessage: string | null;
  blockingMode?: string;
  salesBlocked?: boolean;
  canContinueWithWarning?: boolean;
};

export async function getMonatsbelegStatus(cashRegisterId: string): Promise<MonatsbelegStatusDto> {
  return await apiClient.get<MonatsbelegStatusDto>(`/rksv/monatsbeleg/status/${cashRegisterId}`);
}
