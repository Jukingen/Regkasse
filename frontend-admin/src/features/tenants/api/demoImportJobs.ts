import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

import type { DemoImportRequest, DemoProductImportResult } from '@/api/admin/products';
import { authStorage } from '@/features/auth/services/authStorage';
import { customInstance } from '@/lib/axios';

export type DemoImportJobStatus = 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

export type DemoImportCategoryProgress = {
  categoryName: string;
  total: number;
  processed: number;
  imported: number;
  skipped: number;
  state: 'Waiting' | 'Processing' | 'Completed' | string;
};

export type DemoImportProgress = {
  jobId: string;
  status: DemoImportJobStatus;
  totalProducts: number;
  processedProducts: number;
  importedCount: number;
  skippedCount: number;
  currentProductName?: string | null;
  percent: number;
  categories: DemoImportCategoryProgress[];
  result?: DemoProductImportResult | null;
  message?: string | null;
};

export type DemoImportJobStartResponse = {
  jobId: string;
  totalProducts: number;
};

export type DemoImportJobStatusResponse = {
  jobId: string;
  status: DemoImportJobStatus;
  progress: DemoImportProgress;
};

const STATUS_FROM_API: Record<number, DemoImportJobStatus> = {
  0: 'Queued',
  1: 'Running',
  2: 'Completed',
  3: 'Cancelled',
  4: 'Failed',
};

function normalizeStatus(raw: DemoImportJobStatus | number): DemoImportJobStatus {
  if (typeof raw === 'string') return raw;
  return STATUS_FROM_API[raw] ?? 'Failed';
}

function normalizeProgress(data: DemoImportProgress): DemoImportProgress {
  return {
    ...data,
    status: normalizeStatus(data.status as DemoImportJobStatus | number),
    categories: (data.categories ?? []).map((c) => ({
      ...c,
      state: c.state ?? 'Waiting',
    })),
  };
}

function normalizeJobStatusResponse(
  data: DemoImportJobStatusResponse
): DemoImportJobStatusResponse {
  return {
    ...data,
    status: normalizeStatus(data.status as DemoImportJobStatus | number),
    progress: normalizeProgress(data.progress),
  };
}

function getApiBaseUrl(): string {
  const configured = process.env.NEXT_PUBLIC_API_BASE_URL;
  if (configured) return configured.replace(/\/$/, '');
  if (process.env.NODE_ENV === 'development') return 'http://localhost:5184';
  throw new Error('NEXT_PUBLIC_API_BASE_URL is required.');
}

export async function startDemoImportJob(
  request: DemoImportRequest,
  tenantId?: string
): Promise<DemoImportJobStartResponse> {
  const url = tenantId
    ? `/api/admin/tenants/${tenantId}/demo-products/import/jobs`
    : '/api/admin/products/demo/import/jobs';

  return customInstance<DemoImportJobStartResponse>({
    url,
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    data: request,
  });
}

export async function getDemoImportJobStatus(
  jobId: string,
  tenantId?: string
): Promise<DemoImportJobStatusResponse> {
  const url = tenantId
    ? `/api/admin/tenants/demo-products/import/jobs/${jobId}`
    : `/api/admin/products/demo/import/jobs/${jobId}`;

  const data = await customInstance<DemoImportJobStatusResponse>({ url, method: 'GET' });
  return normalizeJobStatusResponse(data);
}

export async function cancelDemoImportJob(jobId: string, tenantId?: string): Promise<void> {
  const url = tenantId
    ? `/api/admin/tenants/demo-products/import/jobs/${jobId}`
    : `/api/admin/products/demo/import/jobs/${jobId}`;

  await customInstance<void>({ url, method: 'DELETE' });
}

export type DemoImportProgressCallbacks = {
  onProgress: (progress: DemoImportProgress) => void;
  signal?: AbortSignal;
};

const POLL_INTERVAL_MS = 400;
const TERMINAL: DemoImportJobStatus[] = ['Completed', 'Failed', 'Cancelled'];

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
      { once: true }
    );
  });
}

async function pollDemoImportJobUntilDone(
  jobId: string,
  callbacks: DemoImportProgressCallbacks,
  tenantId?: string
): Promise<DemoImportProgress> {
  while (true) {
    if (callbacks.signal?.aborted) {
      try {
        await cancelDemoImportJob(jobId, tenantId);
      } catch {
        // ignore
      }
      throw new DOMException('Import aborted', 'AbortError');
    }

    const status = await getDemoImportJobStatus(jobId, tenantId);
    callbacks.onProgress(status.progress);

    if (TERMINAL.includes(status.status)) {
      return status.progress;
    }

    await sleep(POLL_INTERVAL_MS, callbacks.signal);
  }
}

export async function runDemoImportWithProgress(
  request: DemoImportRequest,
  callbacks: DemoImportProgressCallbacks,
  tenantId?: string
): Promise<DemoImportProgress> {
  const started = await startDemoImportJob(request, tenantId);
  const jobId = started.jobId;
  const token = authStorage.getToken();

  let connection: HubConnection | null = null;

  if (token) {
    try {
      const hubUrl = `${getApiBaseUrl()}/hubs/demo-import-progress`;
      connection = new HubConnectionBuilder()
        .withUrl(hubUrl, { accessTokenFactory: () => token })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      await connection.start();
      await connection.invoke('SubscribeToJob', jobId);
    } catch {
      if (connection) {
        try {
          await connection.stop();
        } catch {
          // ignore
        }
      }
      connection = null;
    }
  }

  try {
    if (!connection) {
      return await pollDemoImportJobUntilDone(jobId, callbacks, tenantId);
    }

    return await new Promise<DemoImportProgress>((resolve, reject) => {
      const onAbort = () => {
        void cancelDemoImportJob(jobId, tenantId).finally(() => {
          reject(new DOMException('Import aborted', 'AbortError'));
        });
      };
      callbacks.signal?.addEventListener('abort', onAbort, { once: true });

      const handleProgress = (payload: DemoImportProgress) => {
        const progress = normalizeProgress({ ...payload, jobId });
        callbacks.onProgress(progress);
        if (TERMINAL.includes(progress.status)) {
          callbacks.signal?.removeEventListener('abort', onAbort);
          resolve(progress);
        }
      };

      connection!.on('ImportProgress', handleProgress);

      void getDemoImportJobStatus(jobId, tenantId)
        .then((s) => handleProgress(s.progress))
        .catch(reject);
    });
  } finally {
    if (connection && connection.state !== HubConnectionState.Disconnected) {
      try {
        await connection.stop();
      } catch {
        // ignore
      }
    }
  }
}
