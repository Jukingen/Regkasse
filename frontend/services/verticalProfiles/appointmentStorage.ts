import { secureStorage } from '../secureStorage';
import { tenantStorage } from '../tenant/tenantStorage';

export interface HairSalonAppointment {
  id: string;
  customerName: string;
  date: string;
  time: string;
  serviceProductId: string;
  serviceName: string;
  durationMinutes?: number | null;
  staffId?: string | null;
  createdAtUtc: string;
}

export interface SaveHairSalonAppointmentInput {
  customerName: string;
  date: string;
  time: string;
  serviceProductId: string;
  serviceName: string;
  durationMinutes?: number | null;
  staffId?: string | null;
}

const MAX_CACHED_APPOINTMENTS = 500;

function storageKey(tenantId: string): string {
  return `vertical_appointments_v1_${tenantId.replace(/[^a-zA-Z0-9_-]/g, '_')}`;
}

async function requireTenantId(): Promise<string> {
  const tenantId = await tenantStorage.getTenantId();
  if (!tenantId) throw new Error('TENANT_CONTEXT_REQUIRED');
  return tenantId;
}

export async function listHairSalonAppointments(): Promise<HairSalonAppointment[]> {
  const tenantId = await requireTenantId();
  const raw = await secureStorage.getItem(storageKey(tenantId));
  if (!raw) return [];
  try {
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? (parsed as HairSalonAppointment[]) : [];
  } catch {
    return [];
  }
}

export async function saveHairSalonAppointment(
  input: SaveHairSalonAppointmentInput
): Promise<HairSalonAppointment> {
  const tenantId = await requireTenantId();
  const key = storageKey(tenantId);
  const existing = await listHairSalonAppointments();
  const appointment: HairSalonAppointment = {
    ...input,
    id: `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`,
    createdAtUtc: new Date().toISOString(),
  };
  const next = [appointment, ...existing].slice(0, MAX_CACHED_APPOINTMENTS);
  await secureStorage.setItem(key, JSON.stringify(next));
  return appointment;
}
