import { AXIOS_INSTANCE, customInstance } from '@/lib/axios';

export type BulkImportError = {
    row: number;
    email?: string | null;
    error: string;
};

export type BulkImportPreviewRow = {
    row: number;
    email: string;
    username?: string | null;
    firstName?: string | null;
    lastName?: string | null;
    role: string;
    tenantSlug: string;
};

export type BulkImportPreview = {
    totalRows: number;
    previewRows: BulkImportPreviewRow[];
    parseError?: string | null;
};

export type BulkImportJobStatus =
    | 'Queued'
    | 'Running'
    | 'Completed'
    | 'Cancelled'
    | 'Failed';

export type BulkImportJobStatusResponse = {
    jobId: string;
    status: BulkImportJobStatus;
    totalRows: number;
    processedRows: number;
    successCount: number;
    failedCount: number;
    errors: BulkImportError[];
    result?: BulkImportResult | null;
    message?: string | null;
};

export type BulkImportResult = {
    totalRows: number;
    successCount: number;
    failedCount: number;
    errors: BulkImportError[];
    downloadUrl?: string | null;
};

export type BulkImportStartResponse = {
    jobId: string;
    totalRows: number;
};

const POLL_INTERVAL_MS = 500;

export async function downloadBulkImportTemplate(): Promise<void> {
    const response = await AXIOS_INSTANCE.get<Blob>('/api/admin/users/bulk-import/template', {
        responseType: 'blob',
    });
    triggerBrowserDownload(response.data, 'bulk-user-import-template.csv');
}

export async function previewBulkImportFile(file: File, maxRows = 10): Promise<BulkImportPreview> {
    const formData = new FormData();
    formData.append('file', file);
    return customInstance<BulkImportPreview>({
        url: '/api/admin/users/bulk-import/preview',
        method: 'POST',
        params: { maxRows },
        data: formData,
        headers: { 'Content-Type': 'multipart/form-data' },
    });
}

export async function startBulkImportJob(file: File): Promise<BulkImportStartResponse> {
    const formData = new FormData();
    formData.append('file', file);
    return customInstance<BulkImportStartResponse>({
        url: '/api/admin/users/bulk-import',
        method: 'POST',
        data: formData,
        headers: { 'Content-Type': 'multipart/form-data' },
    });
}

const STATUS_FROM_API: Record<number, BulkImportJobStatus> = {
    0: 'Queued',
    1: 'Running',
    2: 'Completed',
    3: 'Cancelled',
    4: 'Failed',
};

function normalizeJobStatus(raw: BulkImportJobStatus | number): BulkImportJobStatus {
    if (typeof raw === 'string') return raw;
    return STATUS_FROM_API[raw] ?? 'Failed';
}

function normalizeJobStatusResponse(data: BulkImportJobStatusResponse): BulkImportJobStatusResponse {
    return {
        ...data,
        status: normalizeJobStatus(data.status as BulkImportJobStatus | number),
    };
}

export async function getBulkImportJobStatus(jobId: string): Promise<BulkImportJobStatusResponse> {
    const data = await customInstance<BulkImportJobStatusResponse>({
        url: `/api/admin/users/bulk-import/jobs/${jobId}`,
        method: 'GET',
    });
    return normalizeJobStatusResponse(data);
}

export async function cancelBulkImportJob(jobId: string): Promise<void> {
    await customInstance<void>({
        url: `/api/admin/users/bulk-import/jobs/${jobId}`,
        method: 'DELETE',
    });
}

export async function downloadBulkImportResults(downloadPath: string): Promise<void> {
    const url = downloadPath.startsWith('/') ? downloadPath : `/${downloadPath}`;
    const response = await AXIOS_INSTANCE.get<Blob>(url, { responseType: 'blob' });
    const fileName = url.split('/').pop() ?? 'bulk-import-results.csv';
    triggerBrowserDownload(response.data, fileName);
}

export type BulkImportPollCallbacks = {
    onProgress: (status: BulkImportJobStatusResponse) => void;
    signal?: AbortSignal;
};

/** Poll job until terminal state; returns final status. */
export async function pollBulkImportJobUntilDone(
    jobId: string,
    callbacks: BulkImportPollCallbacks,
): Promise<BulkImportJobStatusResponse> {
    const terminal: BulkImportJobStatus[] = ['Completed', 'Cancelled', 'Failed'];

    while (true) {
        if (callbacks.signal?.aborted) {
            try {
                await cancelBulkImportJob(jobId);
            } catch {
                // ignore cancel errors on abort
            }
            throw new DOMException('Import aborted', 'AbortError');
        }

        const status = await getBulkImportJobStatus(jobId);
        callbacks.onProgress(status);

        if (terminal.includes(status.status)) {
            return status;
        }

        await sleep(POLL_INTERVAL_MS, callbacks.signal);
    }
}

function sleep(ms: number, signal?: AbortSignal): Promise<void> {
    return new Promise((resolve, reject) => {
        if (signal?.aborted) {
            reject(new DOMException('Aborted', 'AbortError'));
            return;
        }
        const timer = setTimeout(resolve, ms);
        signal?.addEventListener(
            'abort',
            () => {
                clearTimeout(timer);
                reject(new DOMException('Aborted', 'AbortError'));
            },
            { once: true },
        );
    });
}

function triggerBrowserDownload(blob: Blob, fileName: string) {
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(objectUrl);
}
