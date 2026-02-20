import { customInstance } from '@/lib/axios';
import { InvoiceListItemDto, InvoiceListParams, PagedResult } from '../types';

export const getInvoicesList = (params: InvoiceListParams) => {
    return customInstance<PagedResult<InvoiceListItemDto>>({
        url: `/api/Invoice/list`,
        method: 'GET',
        params
    });
};

export const exportInvoices = (params: Omit<InvoiceListParams, 'page' | 'pageSize'>) => {
    return customInstance<Blob>({
        url: `/api/Invoice/export`,
        method: 'GET',
        params,
        responseType: 'blob'
    });
};
