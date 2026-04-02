/**
 * Başarılı yedek çalıştırması artefaktı için blob indirme; Fake/stub ortamında gövde küçük yer tutucu olabilir (UI bunu ayrı etiketler).
 * Content-Disposition dosya adını okur (Orval customInstance başlıkları düşürdüğü için doğrudan axios örneği).
 */

import axios from 'axios';
import { AXIOS_INSTANCE } from '@/lib/axios';

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
  | 'unknown';

export class BackupArtifactDownloadError extends Error {
  constructor(
    public readonly code: BackupArtifactDownloadFailureCode,
    message?: string,
  ) {
    super(message ?? code);
    this.name = 'BackupArtifactDownloadError';
  }
}

function parseFilenameFromContentDisposition(header: string | undefined, fallback: string): string {
  if (!header) return fallback;
  const utf8 = /filename\*=UTF-8''([^;]+)/i.exec(header);
  if (utf8?.[1]) {
    try {
      return decodeURIComponent(utf8[1].trim());
    } catch {
      /* yoksay */
    }
  }
  const quoted = /filename="([^"]+)"/i.exec(header);
  if (quoted?.[1]) return quoted[1].trim();
  const plain = /filename=([^;]+)/i.exec(header);
  if (plain?.[1]) return plain[1].trim().replace(/^"|"$/g, '');
  return fallback;
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

function mapApiCodeToFailure(code: string | undefined): BackupArtifactDownloadFailureCode | undefined {
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

export async function downloadBackupArtifactFile(
  runId: string,
  artifactId: string,
  fallbackFilename: string,
): Promise<void> {
  try {
    const res = await AXIOS_INSTANCE.get(downloadPath(runId, artifactId), {
      responseType: 'blob',
    });
    const blob = res.data as Blob;
    const ct = (res.headers['content-type'] as string | undefined)?.toLowerCase() ?? '';
    if (ct.includes('application/json')) {
      const code = await readJsonCodeFromBlob(blob);
      const mapped = mapApiCodeToFailure(code);
      if (mapped) throw new BackupArtifactDownloadError(mapped);
      if (code) throw new BackupArtifactDownloadError('unknown');
    }
    if (blob.size === 0) {
      throw new BackupArtifactDownloadError('empty_payload');
    }
    const name = parseFilenameFromContentDisposition(
      res.headers['content-disposition'] as string | undefined,
      fallbackFilename,
    );
    const url = URL.createObjectURL(blob);
    try {
      const a = document.createElement('a');
      a.href = url;
      a.download = name;
      a.rel = 'noopener';
      document.body.appendChild(a);
      a.click();
      a.remove();
    } finally {
      URL.revokeObjectURL(url);
    }
  } catch (err) {
    if (axios.isAxiosError(err)) {
      const status = err.response?.status;
      if (status === 401) {
        throw new BackupArtifactDownloadError('unauthorized');
      }
      const raw = err.response?.data;
      let code: string | undefined;
      if (raw instanceof Blob) {
        code = await readJsonCodeFromBlob(raw);
      } else if (raw && typeof raw === 'object' && 'code' in raw) {
        const c = (raw as { code?: unknown }).code;
        code = typeof c === 'string' ? c : undefined;
      }
      if (code !== undefined || raw instanceof Blob) {
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
          throw new BackupArtifactDownloadError('forbidden');
        }
        throw new BackupArtifactDownloadError('unknown');
      }
    }
    throw err;
  }
}
