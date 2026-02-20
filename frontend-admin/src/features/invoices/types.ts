import { InvoiceStatus } from '@/api/generated/model';

export type { InvoiceStatus };

export interface InvoiceListItemDto {
    id: string;
    invoiceNumber: string;
    invoiceDate: string;
    customerName?: string;
    companyName: string;
    totalAmount: number;
    status: InvoiceStatus;
    kassenId: string;
    tseSignature?: string;
}

export interface PagedResult<T> {
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
}

export interface InvoiceListParams {
    page?: number;
    pageSize?: number;
    from?: string; // ISO Date
    to?: string; // ISO Date
    status?: InvoiceStatus;
    query?: string;
    sortBy?: 'invoiceDate' | 'invoiceNumber' | 'totalAmount' | 'status';
    sortDir?: 'asc' | 'desc';
}
