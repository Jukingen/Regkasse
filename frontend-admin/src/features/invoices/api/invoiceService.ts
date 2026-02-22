import { customInstance } from '@/lib/axios';
import { InvoiceListParams, PagedResult } from '../types';
import type { InvoiceListItemDto } from '@/api/generated/model/invoiceListItemDto';

// Extend Orval type with credit-note fields from backend
export interface ExtendedInvoiceListItem extends InvoiceListItemDto {
    documentType?: number;        // 0 = Invoice, 1 = CreditNote
    originalInvoiceId?: string;
}

// Raw PascalCase shapes from .NET backend
interface RawPagedResult {
    Items?: RawInvoiceItem[];
    Page?: number;
    PageSize?: number;
    TotalCount?: number;
    TotalPages?: number;
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
    TseSignature?: string;
    DocumentType?: number;
    OriginalInvoiceId?: string;
}

function normalizeItem(raw: RawInvoiceItem): ExtendedInvoiceListItem {
    return {
        id: raw.Id,
        invoiceNumber: raw.InvoiceNumber,
        invoiceDate: raw.InvoiceDate,
        customerName: raw.CustomerName,
        companyName: raw.CompanyName,
        totalAmount: raw.TotalAmount,
        status: raw.Status as any,
        kassenId: raw.KassenId,
        tseSignature: raw.TseSignature,
        documentType: raw.DocumentType,
        originalInvoiceId: raw.OriginalInvoiceId,
    };
}

function normalizePagedResult(raw: RawPagedResult): PagedResult<ExtendedInvoiceListItem> {
    return {
        items: (raw.Items ?? []).map(normalizeItem),
        page: raw.Page,
        pageSize: raw.PageSize,
        totalCount: raw.TotalCount,
        totalPages: raw.TotalPages,
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
