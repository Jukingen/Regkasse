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

export async function postCreateStartbeleg(body: CreateStartbelegRequest): Promise<CreateStartbelegResponse> {
  return apiClient.post<CreateStartbelegResponse>('/rksv/special-receipts/startbeleg', body);
}

/** POST /api/rksv/special-receipts/monatsbeleg */
export type CreateMonatsbelegRequest = {
  cashRegisterId: string;
  year: number;
  month: number;
  reason?: string | null;
};

export type CreateMonatsbelegResponse = {
  paymentId: string;
  invoiceId: string;
  receiptId: string;
  receiptNumber: string;
  qrData: string;
};

export async function postCreateMonatsbeleg(body: CreateMonatsbelegRequest): Promise<CreateMonatsbelegResponse> {
  return apiClient.post<CreateMonatsbelegResponse>('/rksv/special-receipts/monatsbeleg', body);
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

export async function postCreateJahresbeleg(body: CreateJahresbelegRequest): Promise<CreateJahresbelegResponse> {
  return apiClient.post<CreateJahresbelegResponse>('/rksv/special-receipts/jahresbeleg', body);
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
};

export async function getMonatsbelegStatus(cashRegisterId: string): Promise<MonatsbelegStatusDto> {
  return apiClient.get<MonatsbelegStatusDto>(`/rksv/monatsbeleg/status/${cashRegisterId}`);
}
