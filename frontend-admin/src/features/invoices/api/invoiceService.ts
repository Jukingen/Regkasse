import { customInstance } from '@/lib/axios';
import { getApiInvoiceList } from '@/api/generated/invoice/invoice';
import type { GetApiInvoiceListParams } from '@/api/generated/model/getApiInvoiceListParams';
import type { InvoiceListItemDtoPagedResult } from '@/api/generated/model/invoiceListItemDtoPagedResult';
import type { InvoiceListParams } from '../types';

function toListParams(params: InvoiceListParams): GetApiInvoiceListParams {
    return {
        page: params.page,
        pageSize: params.pageSize,
        from: params.from,
        to: params.to,
        status: params.status,
        query: params.query,
        sortBy: params.sortBy,
        sortDir: params.sortDir,
        cashRegisterId: params.cashRegisterId,
    };
}

/** Invoice list — Orval-typed GET /api/Invoice/list (no client-side PascalCase normalization). */
export async function getInvoicesList(params: InvoiceListParams): Promise<InvoiceListItemDtoPagedResult> {
    return getApiInvoiceList(toListParams(params));
}

export const exportInvoices = (params: Omit<InvoiceListParams, 'page' | 'pageSize'>) => {
    return customInstance<Blob>({
        url: `/api/Invoice/export`,
        method: 'GET',
        params,
        responseType: 'blob',
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
    return customInstance<unknown>({
        url: `/api/Invoice/${id}/credit-note`,
        method: 'POST',
        data: body,
    });
};
