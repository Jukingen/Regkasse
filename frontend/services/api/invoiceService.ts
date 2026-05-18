import { apiClient, API_BASE_URL, resolveTenantFetchHeaders } from './config';
import { unwrapApiResponseLayer } from './normalizePosPaymentMethods';
import { sessionManager } from '../session/sessionManager';

/** Line item shape used by the POS invoices screen (mapped from Invoice.invoiceItems / PaymentItems JSON). */
export interface PosInvoiceLine {
    productName: string;
    quantity: number;
    unitPrice: number;
    totalAmount: number;
}

/** View model for POS Beleg / payment-backed rows (api/Invoice/pos-list + api/Invoice/{id}). */
export interface PosInvoiceView {
    id: string;
    receiptNumber: string;
    invoiceDate: string;
    status: string;
    totalAmount: number;
    taxAmount: number;
    customer?: { firstName?: string; lastName?: string };
    paymentMethod?: string;
    tseSignature?: string;
    kassenId?: string;
    taxNumber?: string;
    companyName?: string;
    companyAddress?: string;
    companyEmail?: string;
    companyPhone?: string;
    isPrinted?: boolean;
    items: PosInvoiceLine[];
}

interface PagedResult<T> {
    items?: T[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
}

interface InvoiceListItemRow {
    id: string;
    invoiceNumber: string;
    invoiceDate: string;
    customerName?: string | null;
    companyName?: string;
    totalAmount: number;
    status: string;
    kassenId?: string;
    tseSignature?: string | null;
}

function splitCustomerName(name?: string | null): { firstName?: string; lastName?: string } {
    if (!name?.trim()) return {};
    const parts = name.trim().split(/\s+/);
    if (parts.length === 1) return { firstName: parts[0] };
    return { firstName: parts[0], lastName: parts.slice(1).join(' ') };
}

function mapListRow(row: InvoiceListItemRow): PosInvoiceView {
    return {
        id: row.id,
        receiptNumber: row.invoiceNumber,
        invoiceDate: row.invoiceDate,
        status: String(row.status),
        totalAmount: Number(row.totalAmount),
        taxAmount: 0,
        customer: splitCustomerName(row.customerName),
        tseSignature: row.tseSignature ?? '',
        kassenId: row.kassenId,
        companyName: row.companyName,
        items: [],
    };
}

function parseItems(raw: unknown): PosInvoiceLine[] {
    if (!Array.isArray(raw)) return [];
    return raw.map((it: Record<string, unknown>) => ({
        productName: String(it.productName ?? ''),
        quantity: Number(it.quantity ?? 0),
        unitPrice: Number(it.unitPrice ?? 0),
        totalAmount: Number(it.totalPrice ?? it.totalAmount ?? 0),
    }));
}

function mapDetail(raw: Record<string, unknown>): PosInvoiceView {
    const cust = splitCustomerName(raw.customerName as string | null | undefined);
    return {
        id: String(raw.id ?? ''),
        receiptNumber: String(raw.invoiceNumber ?? ''),
        invoiceDate: String(raw.invoiceDate ?? raw.createdAt ?? ''),
        status: String(raw.status ?? ''),
        totalAmount: Number(raw.totalAmount ?? 0),
        taxAmount: Number(raw.taxAmount ?? 0),
        customer: cust,
        paymentMethod: raw.paymentMethod != null ? String(raw.paymentMethod) : undefined,
        tseSignature: raw.tseSignature != null ? String(raw.tseSignature) : '',
        kassenId: raw.kassenId != null ? String(raw.kassenId) : undefined,
        taxNumber: raw.customerTaxNumber != null ? String(raw.customerTaxNumber) : undefined,
        companyName: raw.companyName != null ? String(raw.companyName) : undefined,
        companyAddress: raw.companyAddress != null ? String(raw.companyAddress) : undefined,
        companyEmail: raw.companyEmail != null ? String(raw.companyEmail) : undefined,
        companyPhone: raw.companyPhone != null ? String(raw.companyPhone) : undefined,
        isPrinted: false,
        items: parseItems(raw.invoiceItems),
    };
}

/**
 * POS-facing list: payment-backed receipts (api/Invoice/pos-list).
 */
export async function getPosInvoices(page = 1, pageSize = 50): Promise<PosInvoiceView[]> {
    const raw = await apiClient.get<unknown>(`/Invoice/pos-list?page=${page}&pageSize=${pageSize}`);
    const res = unwrapApiResponseLayer(raw) as PagedResult<InvoiceListItemRow> & { Items?: InvoiceListItemRow[] };
    const items = res.items ?? res.Items ?? [];
    return items.map(mapListRow);
}

/**
 * Full detail for a payment id or invoice id (api/Invoice/{id}).
 */
export async function getPosInvoiceDetail(id: string): Promise<PosInvoiceView> {
    const raw = await apiClient.get<unknown>(`/Invoice/${id}`);
    const flat = unwrapApiResponseLayer(raw) as Record<string, unknown>;
    return mapDetail(flat);
}

/** Thrown when GET /api/Invoice/{id}/pdf fails with a known HTTP status (auth, permission, not found). */
export class InvoicePdfHttpError extends Error {
  readonly status: number;

  constructor(status: number, message?: string) {
    super(message ?? `PDF HTTP ${status}`);
    this.name = 'InvoicePdfHttpError';
    this.status = status;
  }
}

/**
 * PDF for api/Invoice/{id}/pdf (supports POS payment id projection).
 */
export async function downloadInvoicePdf(id: string): Promise<Blob> {
    const token = await sessionManager.getAccessToken();
    const response = await fetch(`${API_BASE_URL}/Invoice/${encodeURIComponent(id)}/pdf`, {
        method: 'GET',
        headers: await resolveTenantFetchHeaders(
            token ? { Authorization: `Bearer ${token}` } : {},
        ),
    });
    if (!response.ok) {
        throw new InvoicePdfHttpError(response.status, `PDF download failed: ${response.status}`);
    }
    return await response.blob();
}
