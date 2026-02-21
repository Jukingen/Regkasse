/**
 * Invoice feature types.
 * Re-exports from Orval-generated model types for contract alignment.
 * InvoiceListParams kept as manual type for query param construction convenience.
 */

// Re-export generated types
export type { InvoiceListItemDto } from '@/api/generated/model/invoiceListItemDto';
export type { PagedResultOfInvoiceListItemDto } from '@/api/generated/model/pagedResultOfInvoiceListItemDto';
export type { GetInvoiceListParams } from '@/api/generated/model/getInvoiceListParams';
export type { InvoiceStatus } from '@/api/generated/model/invoiceStatus';
export type { ExportInvoicesParams } from '@/api/generated/model/exportInvoicesParams';

import type { InvoiceStatus as _InvoiceStatus } from '@/api/generated/model/invoiceStatus';

// Legacy compat alias
export type PagedResult<T> = {
    items?: T[];
    page?: number;
    pageSize?: number;
    totalCount?: number;
    totalPages?: number;
};

// Manual params type kept for backward compat with invoiceService.ts
export interface InvoiceListParams {
    page?: number;
    pageSize?: number;
    from?: string; // ISO Date
    to?: string; // ISO Date
    status?: _InvoiceStatus;
    query?: string;
    sortBy?: 'invoiceDate' | 'invoiceNumber' | 'totalAmount' | 'status';
    sortDir?: 'asc' | 'desc';
    cashRegisterId?: string;
}
