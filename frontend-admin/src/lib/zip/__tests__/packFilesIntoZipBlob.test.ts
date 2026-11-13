import { describe, expect, it } from 'vitest';

import {
  buildTenantFiscalExportZip,
  slugifyZipFilePart,
} from '@/features/super-admin/logic/buildTenantFiscalExportZip';
import { ZipPackError, packFilesIntoZipBlob } from '@/lib/zip/packFilesIntoZipBlob';

describe('packFilesIntoZipBlob', () => {
  it('rejects empty entry lists', async () => {
    await expect(packFilesIntoZipBlob([])).rejects.toMatchObject({
      code: 'ZIP_NO_ENTRIES',
    } satisfies Partial<ZipPackError>);
  });

  it('builds a non-empty zip blob with streamFiles', async () => {
    const percents: number[] = [];
    const blob = await packFilesIntoZipBlob(
      [
        { path: 'a/hello.json', data: '{"ok":true}' },
        { path: 'a/world.json', data: '{"n":1}' },
      ],
      {
        streamFiles: true,
        onProgress: (p) => percents.push(p),
      }
    );

    expect(blob).toBeInstanceOf(Blob);
    expect(blob.type).toContain('zip');
    expect(blob.size).toBeGreaterThan(20);
    expect(percents.length).toBeGreaterThan(0);
    expect(percents[percents.length - 1]).toBeGreaterThanOrEqual(99);
  });
});

describe('buildTenantFiscalExportZip', () => {
  it('slugifies paths and names the archive', async () => {
    expect(slugifyZipFilePart(' Café / Main ', 'x')).toBe('caf-main');
    expect(slugifyZipFilePart('   ', 'fallback')).toBe('fallback');

    const result = await buildTenantFiscalExportZip({
      tenantSlug: 'Acme Corp',
      tenantName: 'Acme',
      periodFromYmd: '2024-01-01',
      periodToYmd: '2024-12-31',
      files: [
        {
          registerIndex: 0,
          registerNumber: 'KASSE 01',
          blob: new Blob(['{"receipts":[]}'], { type: 'application/json' }),
        },
      ],
    });

    expect(result.fileName).toBe('fiscal-export-acme-acme-corp-2024-01-01-2024-12-31.zip');
    expect(result.blob.size).toBeGreaterThan(20);
  });
});
