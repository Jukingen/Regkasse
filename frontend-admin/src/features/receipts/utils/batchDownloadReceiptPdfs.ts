import {
  downloadReportPdf,
  reportPdfTypeFromSpecialReceiptKind,
} from '@/features/reports/api/reportPdfApi';
import type { ReceiptListItemDto } from '@/features/receipts/types/receipts';
import {
  type ZipFileEntry,
  packFilesIntoZipBlob,
  triggerBrowserDownload,
} from '@/lib/zip/packFilesIntoZipBlob';
import { formatRegisterDisplayLabel } from '@/shared/utils/registerIdentity';

export const BATCH_RECEIPT_PDF_MAX = 200;

export type BatchDownloadProgress = {
  phase: 'fetch' | 'pack' | 'done' | 'error';
  /** 0–100 overall. */
  percent: number;
  current: number;
  total: number;
  currentFileName?: string;
  failedCount: number;
  message?: string;
};

export type BatchDownloadReceiptPdfsResult = {
  zipBlob: Blob;
  zipFileName: string;
  successCount: number;
  failedCount: number;
  totalBytes: number;
};

function sanitizeFileSegment(value: string | null | undefined, fallback: string): string {
  const raw = (value ?? '').trim() || fallback;
  const cleaned = raw
    .replace(/[.\s/\\:]+/g, '_')
    .replace(/[^a-zA-Z0-9_-]/g, '')
    .replace(/^_+|_+$/g, '');
  return cleaned || fallback;
}

export function buildReceiptPdfZipEntryName(row: ReceiptListItemDto, index: number): string {
  const number = sanitizeFileSegment(row.receiptNumber, `receipt_${index + 1}`);
  const display = formatRegisterDisplayLabel(row.registerDisplayNumber);
  const register = sanitizeFileSegment(
    display !== '—' ? display : row.cashRegisterId,
    'register'
  );
  return `${number}_${register}.pdf`;
}

export function buildReceiptBatchZipFileName(at: Date = new Date()): string {
  const y = at.getFullYear();
  const m = String(at.getMonth() + 1).padStart(2, '0');
  const d = String(at.getDate()).padStart(2, '0');
  const hh = String(at.getHours()).padStart(2, '0');
  const mm = String(at.getMinutes()).padStart(2, '0');
  const ss = String(at.getSeconds()).padStart(2, '0');
  return `belege_pdf_${y}${m}${d}_${hh}${mm}${ss}.zip`;
}

/**
 * Downloads stored receipt PDFs for the given rows, packs them into a ZIP, and triggers a browser download.
 */
export async function batchDownloadReceiptPdfs(
  rows: ReceiptListItemDto[],
  options?: {
    onProgress?: (progress: BatchDownloadProgress) => void;
    signal?: AbortSignal;
  }
): Promise<BatchDownloadReceiptPdfsResult> {
  const eligible = rows.filter((r) => Boolean(r.paymentId?.trim()));
  if (eligible.length === 0) {
    throw new Error('NO_ELIGIBLE_RECEIPTS');
  }
  if (eligible.length > BATCH_RECEIPT_PDF_MAX) {
    throw new Error('BATCH_TOO_LARGE');
  }

  const entries: ZipFileEntry[] = [];
  let failedCount = 0;
  let totalBytes = 0;
  const usedNames = new Set<string>();

  for (let i = 0; i < eligible.length; i++) {
    if (options?.signal?.aborted) {
      throw new DOMException('Aborted', 'AbortError');
    }
    const row = eligible[i]!;
    const paymentId = row.paymentId!.trim();
    const fileName = uniqueEntryName(buildReceiptPdfZipEntryName(row, i), usedNames);
    options?.onProgress?.({
      phase: 'fetch',
      percent: Math.round((i / eligible.length) * 70),
      current: i + 1,
      total: eligible.length,
      currentFileName: fileName,
      failedCount,
    });

    try {
      const blob = await downloadReportPdf(
        reportPdfTypeFromSpecialReceiptKind(row.rksvSpecialReceiptKind),
        paymentId,
        { signal: options?.signal }
      );
      totalBytes += blob.size;
      entries.push({ path: fileName, data: blob });
    } catch (err) {
      if (options?.signal?.aborted) {
        throw err instanceof DOMException ? err : new DOMException('Aborted', 'AbortError');
      }
      failedCount += 1;
    }
  }

  if (entries.length === 0) {
    throw new Error('ALL_DOWNLOADS_FAILED');
  }

  options?.onProgress?.({
    phase: 'pack',
    percent: 75,
    current: entries.length,
    total: eligible.length,
    failedCount,
  });

  const zipBlob = await packFilesIntoZipBlob(entries, {
    onProgress: (packPercent) => {
      options?.onProgress?.({
        phase: 'pack',
        percent: 75 + Math.round((packPercent / 100) * 25),
        current: entries.length,
        total: eligible.length,
        failedCount,
      });
    },
  });

  const zipFileName = buildReceiptBatchZipFileName();
  triggerBrowserDownload(zipBlob, zipFileName);

  options?.onProgress?.({
    phase: 'done',
    percent: 100,
    current: entries.length,
    total: eligible.length,
    failedCount,
  });

  return {
    zipBlob,
    zipFileName,
    successCount: entries.length,
    failedCount,
    totalBytes,
  };
}

function uniqueEntryName(base: string, used: Set<string>): string {
  if (!used.has(base)) {
    used.add(base);
    return base;
  }
  const dot = base.lastIndexOf('.');
  const stem = dot > 0 ? base.slice(0, dot) : base;
  const ext = dot > 0 ? base.slice(dot) : '';
  let n = 2;
  let candidate = `${stem}_${n}${ext}`;
  while (used.has(candidate)) {
    n += 1;
    candidate = `${stem}_${n}${ext}`;
  }
  used.add(candidate);
  return candidate;
}
