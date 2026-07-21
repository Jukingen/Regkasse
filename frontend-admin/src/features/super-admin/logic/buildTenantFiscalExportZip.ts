import { type ZipFileEntry, packFilesIntoZipBlob } from '@/lib/zip/packFilesIntoZipBlob';

export function slugifyZipFilePart(value: string | null | undefined, fallback: string): string {
  const normalized = value
    ?.trim()
    .toLowerCase()
    .replace(/[^a-z0-9_-]+/g, '-')
    .replace(/-+/g, '-');
  return normalized && normalized.length > 0 ? normalized.replace(/^-|-$/g, '') : fallback;
}

export type BuildTenantFiscalExportZipInput = {
  tenantSlug?: string | null;
  tenantName?: string | null;
  periodFromYmd: string;
  periodToYmd: string;
  files: readonly {
    registerNumber?: string | null;
    registerIndex: number;
    blob: Blob;
  }[];
  onZipProgress?: (percent: number) => void;
};

export type BuildTenantFiscalExportZipResult = {
  blob: Blob;
  fileName: string;
};

export async function buildTenantFiscalExportZip(
  input: BuildTenantFiscalExportZipInput
): Promise<BuildTenantFiscalExportZipResult> {
  const tenantSlug = slugifyZipFilePart(input.tenantSlug, 'tenant');
  const tenantName = slugifyZipFilePart(input.tenantName, 'tenant');
  const { periodFromYmd, periodToYmd } = input;

  const entries: ZipFileEntry[] = input.files.map((file) => {
    const registerNumber = slugifyZipFilePart(
      file.registerNumber,
      `register-${file.registerIndex + 1}`
    );
    return {
      path: `${tenantSlug}/${registerNumber}-${periodFromYmd}-${periodToYmd}.json`,
      data: file.blob,
    };
  });

  const blob = await packFilesIntoZipBlob(entries, {
    streamFiles: true,
    compressionLevel: 1,
    onProgress: input.onZipProgress,
  });

  return {
    blob,
    fileName: `fiscal-export-${tenantName}-${tenantSlug}-${periodFromYmd}-${periodToYmd}.zip`,
  };
}
