import { apiClient } from './config';

/** Europe/Vienna calendar year and month (matches server Monatsbeleg guard). */
export function getViennaYearMonth(now: Date = new Date()): { year: number; month: number } {
  const fmt = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Vienna', year: 'numeric', month: '2-digit' });
  const parts = fmt.formatToParts(now);
  const year = Number(parts.find((p) => p.type === 'year')?.value ?? '0');
  const month = Number(parts.find((p) => p.type === 'month')?.value ?? '0');
  return { year, month };
}

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

/** GET /api/rksv/monatsbeleg/status/{cashRegisterId} */
export type MonatsbelegStatusDto = {
  isRequired: boolean;
  daysUntilDeadline: number;
  lastMonatsbelegDate: string | null;
  warningLevel: string;
};

export async function getMonatsbelegStatus(cashRegisterId: string): Promise<MonatsbelegStatusDto> {
  return apiClient.get<MonatsbelegStatusDto>(`/rksv/monatsbeleg/status/${cashRegisterId}`);
}
