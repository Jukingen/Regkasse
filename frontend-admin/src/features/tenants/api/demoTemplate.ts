import { AXIOS_INSTANCE, customInstance } from '@/lib/axios';
import type { DemoImportRequest, DemoProductImportResult } from '@/api/admin/products';

export type DemoTemplateValidationIssue = {
    row?: number | null;
    severity: 'error' | 'warning' | string;
    message: string;
};

export type DemoTemplatePreviewRow = {
    row: number;
    rowType: string;
    name: string;
    category?: string | null;
    description?: string | null;
    price?: number | null;
    taxRate?: number | null;
    sortOrder?: number | null;
    vatRate?: number | null;
};

export type DemoTemplateValidationResult = {
    isValid: boolean;
    parseError?: string | null;
    categoryCount: number;
    productCount: number;
    totalRows: number;
    issues: DemoTemplateValidationIssue[];
    previewRows: DemoTemplatePreviewRow[];
};

export type DemoTemplateImportOptions = Pick<
    DemoImportRequest,
    | 'overwriteExisting'
    | 'priceAdjustmentMode'
    | 'priceAdjustmentPercent'
    | 'priceRoundIncrement'
    | 'imageMode'
>;

const TEMPLATE_BASE = '/api/admin/products/demo/template';

export async function downloadDemoTemplate(): Promise<void> {
    const response = await AXIOS_INSTANCE.get<Blob>(`${TEMPLATE_BASE}`, {
        responseType: 'blob',
    });
    triggerBrowserDownload(response.data, 'demo-produkt-vorlage.csv');
}

export async function previewDemoTemplateFile(file: File, maxRows = 20): Promise<DemoTemplateValidationResult> {
    const formData = new FormData();
    formData.append('file', file);
    return customInstance<DemoTemplateValidationResult>({
        url: `${TEMPLATE_BASE}/preview`,
        method: 'POST',
        params: { maxRows },
        data: formData,
        headers: { 'Content-Type': 'multipart/form-data' },
    });
}

export async function importDemoTemplateFile(
    file: File,
    options: DemoTemplateImportOptions,
    tenantId?: string,
): Promise<DemoProductImportResult> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('overwriteExisting', String(options.overwriteExisting ?? false));
    if (options.priceAdjustmentMode) {
        formData.append('priceAdjustmentMode', options.priceAdjustmentMode);
    }
    if (options.priceAdjustmentPercent != null) {
        formData.append('priceAdjustmentPercent', String(options.priceAdjustmentPercent));
    }
    if (options.priceRoundIncrement != null) {
        formData.append('priceRoundIncrement', String(options.priceRoundIncrement));
    }
    if (options.imageMode) {
        formData.append('imageMode', options.imageMode);
    }

    const url = tenantId
        ? `/api/admin/tenants/${tenantId}/demo-products/template/import`
        : `${TEMPLATE_BASE}/import`;

    const { data } = await AXIOS_INSTANCE.post<DemoProductImportResult>(url, formData, {
        headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
}

export function isDemoTemplateFile(file: File): boolean {
    const name = file.name.toLowerCase();
    return name.endsWith('.csv') || name.endsWith('.xlsx') || name.endsWith('.xls');
}

function triggerBrowserDownload(blob: Blob, fileName: string) {
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(objectUrl);
}
