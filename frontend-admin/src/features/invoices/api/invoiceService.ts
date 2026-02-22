import { customInstance } from '@/lib/axios';
import { InvoiceListParams, PagedResult } from '../types';
import type { InvoiceListItemDto } from '@/api/generated/model/invoiceListItemDto';
import type { DocumentType } from '@/api/generated/model/documentType';

// Extend Orval type with credit-note fields from backend
export interface ExtendedInvoiceListItem extends InvoiceListItemDto {
    documentType?: DocumentType;        // 0 = Invoice, 1 = CreditNote
    originalInvoiceId?: string;
}

// Raw PascalCase shapes from .NET backend
interface RawPagedResult {
    Items?: RawInvoiceItem[];
    Page?: number;
    PageSize?: number;
    TotalCount?: number;
    TotalPages?: number;
    // camelCase fallbacks
    items?: RawInvoiceItem[];
    page?: number;
    pageSize?: number;
    totalCount?: number;
    totalPages?: number;
}

interface RawInvoiceItem {
    Id?: string;
    InvoiceNumber?: string;
    InvoiceDate?: string;
    CustomerName?: string;
    CompanyName?: string;
    TotalAmount?: number;
    Status?: number;
    KassenId?: string;
    CashRegisterId?: string;
    TseSignature?: string;
    DocumentType?: number;
    OriginalInvoiceId?: string;

    // camelCase fallbacks
    id?: string;
    invoiceNumber?: string;
    invoiceDate?: string;
    customerName?: string;
    companyName?: string;
    totalAmount?: number;
    status?: number;
    kassenId?: string;
    cashRegisterId?: string;
    tseSignature?: string;
    documentType?: number;
    originalInvoiceId?: string;
}

export function normalizeId(id: string | null | undefined): string | undefined {
    if (!id || id.trim() === '') return undefined;
    if (id === '00000000-0000-0000-0000-000000000000') return undefined; // Filter out zero GUIDs
    return id;
}

function normalizeItem(raw: RawInvoiceItem): ExtendedInvoiceListItem {
    return {
        id: normalizeId(raw.id ?? raw.Id),
        invoiceNumber: raw.invoiceNumber ?? raw.InvoiceNumber,
        invoiceDate: raw.invoiceDate ?? raw.InvoiceDate,
        customerName: raw.customerName ?? raw.CustomerName,
        companyName: raw.companyName ?? raw.CompanyName,
        totalAmount: raw.totalAmount ?? raw.TotalAmount,
        status: (raw.status ?? raw.Status) as any,
        kassenId: normalizeId(raw.cashRegisterId ?? raw.CashRegisterId ?? raw.kassenId ?? raw.KassenId),
        tseSignature: raw.tseSignature ?? raw.TseSignature,
        documentType: (raw.documentType ?? raw.DocumentType) as DocumentType | undefined,
        originalInvoiceId: normalizeId(raw.originalInvoiceId ?? raw.OriginalInvoiceId),
    };
}

function normalizePagedResult(raw: RawPagedResult): PagedResult<ExtendedInvoiceListItem> {
    const rawItems = raw.items ?? raw.Items ?? [];
    return {
        items: rawItems.map(normalizeItem),
        page: raw.page ?? raw.Page,
        pageSize: raw.pageSize ?? raw.PageSize,
        totalCount: raw.totalCount ?? raw.TotalCount,
        totalPages: raw.totalPages ?? raw.TotalPages,
    };
}

export const getInvoicesList = async (params: InvoiceListParams): Promise<PagedResult<ExtendedInvoiceListItem>> => {
    const raw = await customInstance<RawPagedResult>({
        url: `/api/Invoice/list`,
        method: 'GET',
        params
    });
    return normalizePagedResult(raw);
};

export const exportInvoices = (params: Omit<InvoiceListParams, 'page' | 'pageSize'>) => {
    return customInstance<Blob>({
        url: `/api/Invoice/export`,
        method: 'GET',
        params,
        responseType: 'blob'
    });
};

export const getInvoicePdf = (id: string): Promise<Blob> => {
    return customInstance<Blob>({
        url: `/api/Invoice/${id}/pdf`,
        method: 'GET',
        responseType: 'blob',
    });
};

// Credit note / storno
export interface CreateCreditNoteBody {
    reasonCode: string;
    reasonText: string;
}

export const createCreditNote = (id: string, body: CreateCreditNoteBody) => {
    return customInstance<any>({
        url: `/api/Invoice/${id}/credit-note`,
        method: 'POST',
        data: body,
    });
};
