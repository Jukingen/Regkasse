import { apiClient } from './config';
import { API_PATHS } from './apiPaths';

export type RoomStatusName = 'Available' | 'Occupied' | 'Cleaning' | 'Maintenance';

export type RoomDto = {
  id: string;
  number: string;
  type: string;
  capacity: number;
  status: RoomStatusName;
  isActive: boolean;
  occupied: boolean;
};

export type GuestFolioDto = {
  id: string;
  customerId: string;
  customerName?: string | null;
  roomId: string;
  roomNumber?: string | null;
  checkIn: string;
  checkOut?: string | null;
  status: string;
  balance: number;
  notes?: string | null;
  isOpen: boolean;
};

export type GuestFolioItemDto = {
  id: string;
  folioId: string;
  paymentDetailId?: string | null;
  description: string;
  amount: number;
  createdAtUtc: string;
};

export type CreateGuestFolioRequest = {
  customerId: string;
  roomId: string;
  checkIn?: string;
  checkOut?: string;
  balance?: number;
};

function asRecord(value: unknown): Record<string, unknown> {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function mapRoom(raw: Record<string, unknown>): RoomDto {
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    number: String(raw.number ?? raw.Number ?? ''),
    type: String(raw.type ?? raw.Type ?? ''),
    capacity: Number(raw.capacity ?? raw.Capacity ?? 1),
    status: (String(raw.status ?? raw.Status ?? (raw.occupied || raw.Occupied ? 'Occupied' : 'Available')) ||
      'Available') as RoomDto['status'],
    isActive: Boolean(raw.isActive ?? raw.IsActive ?? true),
    occupied: Boolean(raw.occupied ?? raw.Occupied),
  };
}

function mapFolio(raw: Record<string, unknown>): GuestFolioDto {
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    customerId: String(raw.customerId ?? raw.CustomerId ?? ''),
    customerName: (raw.customerName ?? raw.CustomerName ?? null) as string | null,
    roomId: String(raw.roomId ?? raw.RoomId ?? ''),
    roomNumber: (raw.roomNumber ?? raw.RoomNumber ?? null) as string | null,
    checkIn: String(raw.checkIn ?? raw.CheckIn ?? ''),
    checkOut: (raw.checkOut ?? raw.CheckOut ?? null) as string | null,
    status: String(raw.status ?? raw.Status ?? (raw.isOpen || raw.IsOpen ? 'Open' : 'Closed')),
    balance: Number(raw.balance ?? raw.Balance ?? 0),
    notes: (raw.notes ?? raw.Notes ?? null) as string | null,
    isOpen: Boolean(raw.isOpen ?? raw.IsOpen),
  };
}

function mapItem(raw: Record<string, unknown>): GuestFolioItemDto {
  return {
    id: String(raw.id ?? raw.Id ?? ''),
    folioId: String(raw.folioId ?? raw.FolioId ?? ''),
    paymentDetailId: (raw.paymentDetailId ?? raw.PaymentDetailId ?? null) as string | null,
    description: String(raw.description ?? raw.Description ?? ''),
    amount: Number(raw.amount ?? raw.Amount ?? 0),
    createdAtUtc: String(raw.createdAtUtc ?? raw.CreatedAtUtc ?? ''),
  };
}

function asRows(payload: unknown): Record<string, unknown>[] {
  if (Array.isArray(payload)) return payload as Record<string, unknown>[];
  const wrapped = asRecord(payload);
  return Array.isArray(wrapped.data) ? (wrapped.data as Record<string, unknown>[]) : [];
}

function unwrapRecord(payload: unknown): Record<string, unknown> {
  const record = asRecord(payload);
  return asRecord(record.data ?? payload);
}

export async function listRooms(): Promise<RoomDto[]> {
  const payload = await apiClient.get<unknown>(API_PATHS.PRODUCT.ROOMS);
  return asRows(payload).map((row) => mapRoom(row));
}

export async function listFolios(roomId?: string, openOnly = true): Promise<GuestFolioDto[]> {
  const payload = await apiClient.get<unknown>(API_PATHS.PRODUCT.FOLIOS, {
    params: { roomId, openOnly },
  });
  return asRows(payload).map((row) => mapFolio(row));
}

export async function createFolio(body: CreateGuestFolioRequest): Promise<GuestFolioDto> {
  const response = await apiClient.post<unknown>(API_PATHS.PRODUCT.FOLIOS, body);
  return mapFolio(unwrapRecord(response));
}

export async function updateFolio(
  folioId: string,
  body: { checkOut?: string; notes?: string; status?: string }
): Promise<GuestFolioDto> {
  const response = await apiClient.patch<unknown>(API_PATHS.PRODUCT.FOLIO(folioId), body);
  return mapFolio(unwrapRecord(response));
}

export async function chargeFolio(
  folioId: string,
  body: { description: string; amount: number }
): Promise<GuestFolioDto> {
  const response = await apiClient.post<unknown>(API_PATHS.PRODUCT.FOLIO_CHARGE(folioId), body);
  return mapFolio(unwrapRecord(response));
}

export async function listFolioItems(folioId: string): Promise<GuestFolioItemDto[]> {
  const payload = await apiClient.get<unknown>(API_PATHS.PRODUCT.FOLIO_ITEMS(folioId));
  return asRows(payload).map((row) => mapItem(row));
}
