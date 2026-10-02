import axios from 'axios';

import { apiClient } from './config';

export interface TicketValidation {
  displayCode: string;
  status: string;
  validFromUtc?: string | null;
  validUntilUtc?: string | null;
  isValid: boolean;
  canRedeem: boolean;
}

export interface TicketError {
  code: string;
  message: string;
}

function asRecord(value: unknown): Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function mapTicket(value: unknown): TicketValidation {
  const raw = asRecord(value);
  return {
    displayCode: String(raw.displayCode ?? raw.DisplayCode ?? ''),
    status: String(raw.status ?? raw.Status ?? ''),
    validFromUtc: (raw.validFromUtc ?? raw.ValidFromUtc ?? null) as string | null,
    validUntilUtc: (raw.validUntilUtc ?? raw.ValidUntilUtc ?? null) as string | null,
    isValid: Boolean(raw.isValid ?? raw.IsValid),
    canRedeem: Boolean(raw.canRedeem ?? raw.CanRedeem),
  };
}

function ticketErrorFromUnknown(error: unknown): TicketError {
  if (axios.isAxiosError(error)) {
    const data = asRecord(error.response?.data);
    const nested = asRecord(data.error);
    const code = String(data.code ?? data.Code ?? nested.code ?? nested.Code ?? '');
    const message = String(data.message ?? data.Message ?? nested.message ?? error.message ?? '');
    return { code, message };
  }
  return { code: '', message: error instanceof Error ? error.message : String(error) };
}

export async function validatePosTicket(code: string): Promise<TicketValidation | null> {
  const trimmed = code.trim();
  if (!trimmed) return null;
  try {
    const response = await apiClient.post<unknown>(
      `/pos/tickets/${encodeURIComponent(trimmed)}/validate`
    );
    return mapTicket(response);
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 404) {
      return null;
    }
    throw error;
  }
}

export async function redeemPosTicket(code: string): Promise<TicketValidation> {
  const trimmed = code.trim();
  try {
    const response = await apiClient.post<unknown>(
      `/pos/tickets/${encodeURIComponent(trimmed)}/redeem`
    );
    return mapTicket(response);
  } catch (error) {
    const mapped = ticketErrorFromUnknown(error);
    const err = new Error(mapped.message || mapped.code || 'TICKET_INVALID') as Error & TicketError;
    err.code = mapped.code;
    err.message = mapped.message || mapped.code;
    throw err;
  }
}
