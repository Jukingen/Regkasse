import { customInstance } from '@/lib/axios';

export async function downloadLicenseSaleInvoicePdf(saleId: string, fileName?: string): Promise<void> {
    const blob = await customInstance<Blob>({
        url: `/api/admin/billing/license-sales/${saleId}/pdf`,
        method: 'GET',
        responseType: 'blob',
    });
    const url = globalThis.URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName ?? `invoice-${saleId}.pdf`;
    anchor.click();
    globalThis.URL.revokeObjectURL(url);
}
