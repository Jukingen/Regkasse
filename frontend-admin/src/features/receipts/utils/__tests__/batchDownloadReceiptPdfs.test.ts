import { describe, expect, it } from 'vitest';

import type { ReceiptListItemDto } from '@/features/receipts/types/receipts';
import {
  buildReceiptBatchZipFileName,
  buildReceiptPdfZipEntryName,
} from '@/features/receipts/utils/batchDownloadReceiptPdfs';

function row(partial: Partial<ReceiptListItemDto>): ReceiptListItemDto {
  return {
    receiptId: 'r1',
    paymentId: 'p1',
    receiptNumber: '45',
    issuedAt: '2026-07-22T12:00:00Z',
    cashierId: null,
    cashRegisterId: 'reg-1',
    subTotal: 0,
    taxTotal: 0,
    grandTotal: 0,
    createdAt: '2026-07-22T12:00:00Z',
    ...partial,
  };
}

describe('batchDownloadReceiptPdfs helpers', () => {
  it('builds zip entry names from receipt + register', () => {
    expect(
      buildReceiptPdfZipEntryName(
        row({ receiptNumber: '45', registerDisplayNumber: 'KASSE-001' }),
        0
      )
    ).toBe('45_KASSE-001.pdf');
  });

  it('builds batch zip file names with local stamp', () => {
    const name = buildReceiptBatchZipFileName(new Date(2026, 6, 22, 14, 30, 22));
    expect(name).toMatch(/^belege_pdf_20260722_\d{6}\.zip$/);
  });
});
