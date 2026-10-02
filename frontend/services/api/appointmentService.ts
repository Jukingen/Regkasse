import axios from 'axios';

import { apiClient } from './config';

export type AppointmentStatus =
  | 'Booked'
  | 'Confirmed'
  | 'Completed'
  | 'Cancelled'
  | 'NoShow';

export interface AppointmentDto {
  id: string;
  tenantId: string;
  customerId?: string | null;
  serviceProductId?: string | null;
  staffId?: string | null;
  startUtc: string;
  endUtc: string;
  status: AppointmentStatus;
  notes?: string | null;
  version: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  createdByUserId?: string | null;
}

export interface CreateAppointmentRequest {
  customerId?: string | null;
  customerName?: string | null;
  serviceProductId?: string | null;
  staffId?: string | null;
  startUtc: string;
  endUtc?: string | null;
  notes?: string | null;
  expectedVersion?: number | null;
}

export interface ListAppointmentsQuery {
  from?: string;
  to?: string;
  staffId?: string;
}

export class AppointmentConflictError extends Error {
  readonly code = 'APPOINTMENT_CONFLICT';

  constructor(readonly appointment: AppointmentDto) {
    super('APPOINTMENT_CONFLICT');
    this.name = 'AppointmentConflictError';
  }
}

interface AppointmentConflictBody {
  code?: string;
  appointment?: AppointmentDto;
}

export async function listAppointments(
  query: ListAppointmentsQuery = {}
): Promise<AppointmentDto[]> {
  const data = await apiClient.get<AppointmentDto[]>('/pos/appointments', {
    params: {
      from: query.from,
      to: query.to,
      staffId: query.staffId,
    },
  });
  return Array.isArray(data) ? data : [];
}

export async function createAppointment(
  request: CreateAppointmentRequest
): Promise<AppointmentDto> {
  try {
    return await apiClient.post<AppointmentDto>('/pos/appointments', request);
  } catch (error) {
    throw toAppointmentError(error);
  }
}

export function toAppointmentError(error: unknown): Error {
  if (axios.isAxiosError(error) && error.response?.status === 409) {
    const body = error.response.data as AppointmentConflictBody | undefined;
    if (body?.code === 'APPOINTMENT_CONFLICT' && body.appointment) {
      return new AppointmentConflictError(body.appointment);
    }
  }
  return error instanceof Error ? error : new Error('APPOINTMENT_SAVE_FAILED');
}

export function combineLocalDateTime(date: string, time: string): Date {
  const isoDate = toIsoDate(date.trim());
  const normalizedTime = time.trim().length === 5 ? `${time.trim()}:00` : time.trim();
  const value = new Date(`${isoDate}T${normalizedTime}`);
  if (Number.isNaN(value.getTime())) {
    throw new Error('INVALID_DATETIME');
  }
  return value;
}

function toIsoDate(value: string): string {
  const dmy = /^(\d{1,2})\.(\d{1,2})\.(\d{4})$/.exec(value);
  if (!dmy) return value;
  const day = dmy[1].padStart(2, '0');
  const month = dmy[2].padStart(2, '0');
  return `${dmy[3]}-${month}-${day}`;
}
