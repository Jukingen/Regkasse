import { customInstance } from '@/lib/axios';
import { InvoiceListItemDto, InvoiceListParams, PagedResult } from '../types';

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
}

function normalizeItem(raw: RawInvoiceItem): InvoiceListItemDto {
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
    };
}

function normalizePagedResult(raw: RawPagedResult): PagedResult<InvoiceListItemDto> {
    return {
        items: (raw.Items ?? []).map(normalizeItem),
        page: raw.Page,
        pageSize: raw.PageSize,
        totalCount: raw.TotalCount,
        totalPages: raw.TotalPages,
    };
}

export const getInvoicesList = async (params: InvoiceListParams): Promise<PagedResult<InvoiceListItemDto>> => {
    const raw = await customInstance<RawPagedResult>({
        url: `/api/Invoice/pos-list`,
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
