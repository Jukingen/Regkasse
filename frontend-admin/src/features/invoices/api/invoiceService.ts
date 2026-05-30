import { customInstance } from '@/lib/axios';
import { getApiInvoiceList, postApiInvoiceIdCreditNote } from '@/api/generated/invoice/invoice';
import type { CreateCreditNoteRequest } from '@/api/generated/model/createCreditNoteRequest';
import type { GetApiInvoiceListParams } from '@/api/generated/model/getApiInvoiceListParams';
import type { Invoice } from '@/api/generated/model/invoice';
import type { InvoiceListItemDtoPagedResult } from '@/api/generated/model/invoiceListItemDtoPagedResult';
import type { InvoiceListParams } from '../types';

/** Alias for callers — same shape as Orval `CreateCreditNoteRequest`. */
export type CreateCreditNoteBody = CreateCreditNoteRequest;

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

export const getInvoicePreview = (id: string): Promise<Blob> => {
    return customInstance<Blob>({
        url: `/api/Invoice/${id}/preview`,
        method: 'GET',
        responseType: 'blob',
    });
};

export interface ResendInvoiceResult {
    message?: string;
    error?: string;
}

export const resendInvoiceEmail = (id: string, recipientEmail?: string): Promise<ResendInvoiceResult> => {
    return customInstance<ResendInvoiceResult>({
        url: `/api/Invoice/${id}/resend`,
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        data: recipientEmail ? { recipientEmail } : {},
    });
};

/** Credit note — Orval-typed POST /api/Invoice/{id}/credit-note (returns `Invoice` per OpenAPI). */
export function createCreditNote(id: string, body: CreateCreditNoteBody): Promise<Invoice> {
    return postApiInvoiceIdCreditNote(id, body);
}
