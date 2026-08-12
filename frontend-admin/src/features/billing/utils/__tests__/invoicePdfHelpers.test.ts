import { beforeEach, describe, expect, it, vi } from 'vitest';

import { downloadLicenseSaleInvoicePdf } from '@/features/billing/utils/downloadInvoicePdf';
import { fetchLicenseSalePreviewPdf } from '@/features/billing/utils/previewInvoicePdf';

const mockCustomInstance = vi.fn();

vi.mock('@/lib/axios', () => ({
  customInstance: (config: unknown) => mockCustomInstance(config),
}));

describe('billing invoice pdf helpers', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('fetchLicenseSalePreviewPdf posts blob request', async () => {
    const blob = new Blob(['pdf']);
    mockCustomInstance.mockResolvedValue(blob);
    await expect(
      fetchLicenseSalePreviewPdf({ tenantId: 't1', months: 12 } as never)
    ).resolves.toBe(blob);
    expect(mockCustomInstance).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/billing/license-sales/preview-pdf',
        method: 'POST',
        responseType: 'blob',
      })
    );
  });

  it('downloadLicenseSaleInvoicePdf creates and clicks an anchor', async () => {
    const blob = new Blob(['pdf']);
    mockCustomInstance.mockResolvedValue(blob);

    const createObjectURL = vi.fn(() => 'blob:invoice');
    const revokeObjectURL = vi.fn();
    vi.stubGlobal('URL', { createObjectURL, revokeObjectURL });

    const click = vi.fn();
    const anchor = { href: '', download: '', click } as unknown as HTMLAnchorElement;
    vi.spyOn(document, 'createElement').mockReturnValue(anchor);

    await downloadLicenseSaleInvoicePdf('sale-1', 'custom.pdf');
    expect(mockCustomInstance).toHaveBeenCalledWith(
      expect.objectContaining({
        url: '/api/admin/billing/license-sales/sale-1/pdf',
        method: 'GET',
        responseType: 'blob',
      })
    );
    expect(anchor.download).toBe('custom.pdf');
    expect(click).toHaveBeenCalled();
    expect(revokeObjectURL).toHaveBeenCalledWith('blob:invoice');

    vi.unstubAllGlobals();
  });
});
