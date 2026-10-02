import { apiClient } from './config';
import { staffInitials } from '../../utils/staffInitials';

export { staffInitials };

export interface PosStaffMember {
  id: string;
  name: string;
  role: string;
}

const CACHE_TTL_MS = 5 * 60 * 1000;

let cachedStaff: { data: PosStaffMember[]; ts: number } | null = null;

export function clearPosStaffCache(): void {
  cachedStaff = null;
}

export async function listPosStaff(): Promise<PosStaffMember[]> {
  if (cachedStaff && Date.now() - cachedStaff.ts < CACHE_TTL_MS) {
    return cachedStaff.data;
  }

  const data = await apiClient.get<PosStaffMember[] | { data?: PosStaffMember[] }>('/pos/staff');
  const rows = Array.isArray(data) ? data : Array.isArray(data.data) ? data.data : [];
  const members = rows
    .map((row) => ({
      id: String(row.id ?? ''),
      name: String(row.name ?? '').trim(),
      role: String(row.role ?? ''),
    }))
    .filter((row) => row.id.length > 0);
  cachedStaff = { data: members, ts: Date.now() };
  return members;
}
