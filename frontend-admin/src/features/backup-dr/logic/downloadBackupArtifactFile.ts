/**
 * Başarılı yedek çalıştırması artefaktı için blob indirme; Fake/stub ortamında gövde küçük yer tutucu olabilir (UI bunu ayrı etiketler).
 * Content-Disposition dosya adını okur (Orval customInstance başlıkları düşürdüğü için doğrudan axios örneği).
 * Large files: optional progressive Range download with pause/resume via `progress` option.
 */
import axios from 'axios';

import { AXIOS_INSTANCE } from '@/lib/axios';
import { triggerBlobDownload } from '@/lib/download/exportDownload';
import {
  ProgressiveDownloadCancelledError,
  ProgressiveDownloadSession,
  type ProgressiveDownloadSnapshot,
  fetchBlobProgressive,
  parseFilenameFromContentDisposition,
} from '@/lib/download/progressiveDownload';

export type BackupArtifactDownloadFailureCode =
  | 'run_not_found'
  | 'artifact_not_found'
  | 'file_missing'
  | 'not_found'
  | 'conflict'
  | 'storage'
  | 'forbidden'
  | 'simulated_not_downloadable'
  | 'unauthorized'
  /** HTTP 200 ama gövde boş veya Content-Length ile çelişiyor — büyük olasılıkla sunucu/depolama. */
  | 'empty_payload'
  | 'unknown'
  | 'cancelled';

export class BackupArtifactDownloadError extends Error {
  constructor(
    public readonly code: BackupArtifactDownloadFailureCode,
    message?: string
  ) {
    super(message ?? code);
    this.name = 'BackupArtifactDownloadError';
  }
}

export type BackupArtifactDownloadProgressOptions = {
  session: ProgressiveDownloadSession;
  onProgress: (snapshot: ProgressiveDownloadSnapshot) => void;
  label?: string;
  expectedSizeBytes?: number | null;
};

export type BackupArtifactDownloadSecurityOptions = {
  headers?: Record<string, string>;
};

export async function downloadBackupArtifactFile(
  runId: string,
  artifactId: string,
  fallbackFilename: string,
  progress?: BackupArtifactDownloadProgressOptions,
  security?: BackupArtifactDownloadSecurityOptions
): Promise<void> {
  const url = downloadPath(runId, artifactId);
  const securityHeaders = security?.headers;

  if (progress) {
    try {
      const result = await fetchBlobProgressive({
        url,
        fileName: fallbackFilename,
        label: progress.label,
        expectedTotalBytes: progress.expectedSizeBytes,
        session: progress.session,
        onProgress: progress.onProgress,
        axiosConfig: securityHeaders ? { headers: securityHeaders } : undefined,
      });
      const ct = String(result.headers['content-type'] ?? result.headers['Content-Type'] ?? '');
      await assertDownloadBlob(result.blob, ct);
      triggerBlobDownload(result.blob, result.fileName);
      return;
    } catch (err) {
      await mapAxiosDownloadErrorAsync(err);
    }
  }

  try {
    const res = await AXIOS_INSTANCE.get(url, {
      responseType: 'blob',
      headers: securityHeaders,
    });
    const blob = res.data as Blob;
    await assertDownloadBlob(blob, res.headers['content-type'] as string | undefined);
    const name = parseFilenameFromContentDisposition(
      res.headers['content-disposition'] as string | undefined,
      fallbackFilename
    );
    triggerBlobDownload(blob, name);
  } catch (err) {
    if (err instanceof BackupArtifactDownloadError) throw err;
    await mapAxiosDownloadErrorAsync(err);
  }
}

async function readJsonCodeFromBlob(blob: Blob): Promise<string | undefined> {
  try {
    const text = await blob.text();
    const o = JSON.parse(text) as { code?: string };
    return typeof o?.code === 'string' ? o.code : undefined;
  } catch {
    return undefined;
  }
}

function mapApiCodeToFailure(
  code: string | undefined
): BackupArtifactDownloadFailureCode | undefined {
  if (code === undefined) return undefined;
  if (code === 'BACKUP_ARTIFACT_FILE_MISSING') return 'file_missing';
  if (code === 'BACKUP_RUN_NOT_FOUND') return 'run_not_found';
  if (code === 'BACKUP_ARTIFACT_NOT_FOUND') return 'artifact_not_found';
  if (code === 'BACKUP_RUN_NOT_SUCCEEDED') return 'conflict';
  if (code === 'BACKUP_STORAGE_NOT_CONFIGURED') return 'storage';
  if (code === 'BACKUP_ARTIFACT_NOT_DOWNLOADABLE_SIMULATED') return 'simulated_not_downloadable';
  return undefined;
}

const downloadPath = (runId: string, artifactId: string) =>
  `/api/admin/backup/runs/${runId}/artifacts/${artifactId}/download`;

async function assertDownloadBlob(blob: Blob, contentType: string | undefined): Promise<void> {
  const ct = (contentType ?? '').toLowerCase();
  if (ct.includes('application/json')) {
    const code = await readJsonCodeFromBlob(blob);
    const mapped = mapApiCodeToFailure(code);
    if (mapped) throw new BackupArtifactDownloadError(mapped);
    if (code) throw new BackupArtifactDownloadError('unknown');
  }
  if (blob.size === 0) {
    throw new BackupArtifactDownloadError('empty_payload');
  }
}

function mapAxiosDownloadErrorAsync(err: unknown): Promise<never> {
  if (err instanceof BackupArtifactDownloadError) {
    return Promise.reject(err);
  }
  if (err instanceof ProgressiveDownloadCancelledError) {
    return Promise.reject(new BackupArtifactDownloadError('cancelled'));
  }
  if (!axios.isAxiosError(err)) {
    return Promise.reject(err);
  }

  return (async () => {
    const status = err.response?.status;
    if (status === 401) {
      throw new BackupArtifactDownloadError('unauthorized');
    }
    const raw = err.response?.data;
    let code: string | undefined;
    if (raw instanceof Blob) {
      code = await readJsonCodeFromBlob(raw);
    } else if (raw instanceof ArrayBuffer) {
      try {
        code = (JSON.parse(new TextDecoder().decode(raw)) as { code?: string }).code;
      } catch {
        code = undefined;
      }
    } else if (raw && typeof raw === 'object' && 'code' in raw) {
      const c = (raw as { code?: unknown }).code;
      code = typeof c === 'string' ? c : undefined;
    }

    if (status === 404) {
      if (code === 'BACKUP_ARTIFACT_FILE_MISSING') {
        throw new BackupArtifactDownloadError('file_missing');
      }
      if (code === 'BACKUP_RUN_NOT_FOUND') {
        throw new BackupArtifactDownloadError('run_not_found');
      }
      if (code === 'BACKUP_ARTIFACT_NOT_FOUND') {
        throw new BackupArtifactDownloadError('artifact_not_found');
      }
      throw new BackupArtifactDownloadError('not_found');
    }
    if (status === 409 || code === 'BACKUP_RUN_NOT_SUCCEEDED') {
      throw new BackupArtifactDownloadError('conflict');
    }
    if (status === 503 || code === 'BACKUP_STORAGE_NOT_CONFIGURED') {
      throw new BackupArtifactDownloadError('storage');
    }
    if (status === 403) {
      if (code === 'BACKUP_ARTIFACT_NOT_DOWNLOADABLE_SIMULATED') {
        throw new BackupArtifactDownloadError('simulated_not_downloadable');
      }
      if (
        code === 'SENSITIVE_EXPORT_ACK_REQUIRED' ||
        code === 'SENSITIVE_EXPORT_APPROVAL_REQUIRED' ||
        code === 'SENSITIVE_EXPORT_2FA_REQUIRED' ||
        code === 'SENSITIVE_EXPORT_2FA_INVALID' ||
        code === 'DOWNLOAD_TICKET_INVALID'
      ) {
        const securityErr = new Error(code);
        (securityErr as Error & { code: string }).code = code;
        throw securityErr;
      }
      throw new BackupArtifactDownloadError('forbidden');
    }
    if (status === 429 && code === 'DOWNLOAD_DAILY_LIMIT') {
      const securityErr = new Error(code);
      (securityErr as Error & { code: string }).code = code;
      throw securityErr;
    }
    if (status === 413 && code === 'DOWNLOAD_FILE_TOO_LARGE') {
      const securityErr = new Error(code);
      (securityErr as Error & { code: string }).code = code;
      throw securityErr;
    }
    throw new BackupArtifactDownloadError('unknown');
  })();
}
