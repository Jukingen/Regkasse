import { apiClient } from './config';

export interface SplitItemDto {
  id: string;
  productId: string;
  productName: string;
  sourceCartItemId?: string | null;
  quantity: number;
  price: number;
  lineTotal: number;
  customerName: string;
  seatNumber: number;
}

export interface SplitSessionDto {
  id: string;
  originalCartId: string;
  originalCartKey: string;
  tableNumber?: number | null;
  isCompleted: boolean;
  createdAt: string;
  items: SplitItemDto[];
  grandTotal: number;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readString(raw: unknown, fallback = ''): string {
  return typeof raw === 'string' ? raw : fallback;
}

function readNumber(raw: unknown, fallback = 0): number {
  if (typeof raw === 'number' && Number.isFinite(raw)) return raw;
  if (typeof raw === 'string' && raw.trim() !== '') {
    const n = Number(raw.replace(',', '.'));
    if (Number.isFinite(n)) return n;
  }
  return fallback;
}

export function parseSplitItemDto(raw: unknown): SplitItemDto | null {
  if (!isRecord(raw)) return null;
  const id = readString(raw.id ?? raw.Id);
  if (!id) return null;
  return {
    id,
    productId: readString(raw.productId ?? raw.ProductId),
    productName: readString(raw.productName ?? raw.ProductName, 'Unbekannt'),
    sourceCartItemId: readString(raw.sourceCartItemId ?? raw.SourceCartItemId) || null,
    quantity: readNumber(raw.quantity ?? raw.Quantity),
    price: readNumber(raw.price ?? raw.Price),
    lineTotal: readNumber(raw.lineTotal ?? raw.LineTotal),
    customerName: readString(raw.customerName ?? raw.CustomerName),
    seatNumber: readNumber(raw.seatNumber ?? raw.SeatNumber),
  };
}

export function parseSplitSessionDto(raw: unknown): SplitSessionDto | null {
  if (!isRecord(raw)) return null;
  const id = readString(raw.id ?? raw.Id);
  if (!id) return null;
  const itemsRaw = raw.items ?? raw.Items ?? raw.splitItems ?? raw.SplitItems;
  const items = Array.isArray(itemsRaw)
    ? itemsRaw.map(parseSplitItemDto).filter((i): i is SplitItemDto => i != null)
    : [];
  return {
    id,
    originalCartId: readString(raw.originalCartId ?? raw.OriginalCartId),
    originalCartKey: readString(raw.originalCartKey ?? raw.OriginalCartKey),
    tableNumber: (raw.tableNumber ?? raw.TableNumber ?? null) as number | null | undefined,
    isCompleted: Boolean(raw.isCompleted ?? raw.IsCompleted),
    createdAt: readString(raw.createdAt ?? raw.CreatedAt),
    items,
    grandTotal: readNumber(raw.grandTotal ?? raw.GrandTotal),
  };
}

function parseCartIdList(raw: unknown): string[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((v) => readString(v)).filter(Boolean);
}

class SplitService {
  async start(cartRowId: string): Promise<SplitSessionDto> {
    const res = await apiClient.post<unknown>('/pos/split/start', { cartId: cartRowId });
    const session = parseSplitSessionDto(res);
    if (!session) throw new Error('Invalid split session response');
    return session;
  }

  async assignItem(
    sessionId: string,
    itemId: string,
    customerName: string,
    seatNumber: number
  ): Promise<void> {
    await apiClient.post(`/pos/split/${sessionId}/assign`, {
      itemId,
      customerName,
      seatNumber,
    });
  }

  async complete(sessionId: string): Promise<string[]> {
    const res = await apiClient.post<unknown>(`/pos/split/${sessionId}/complete`);
    return parseCartIdList(res);
  }
}

export const splitService = new SplitService();
