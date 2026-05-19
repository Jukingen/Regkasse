import { describe, expect, it } from 'vitest';
import {
  computeSignatureChainOutcome,
  filterReportForRegister,
  formatSignaturePreview,
  prevSignatureMatches,
} from '@/features/rksv/signature-chain/signatureChainUtils';
import type { RksvComplianceReport } from '@/features/rksv/compliance/types';

describe('signatureChainUtils', () => {
  it('formats long signature prefixes for display', () => {
    expect(formatSignaturePreview('abcdefgh12345678xyz')).toBe('abcdefgh…45678xyz');
    expect(formatSignaturePreview('short')).toBe('short');
  });

  it('filters chain rows by register', () => {
    const report: RksvComplianceReport = {
      signatureChain: [
        { cashRegisterId: 'a', receiptNumber: 'R1', status: 'Pass' },
        { cashRegisterId: 'b', receiptNumber: 'R2', status: 'Fail' },
      ],
      sequenceGaps: [{ cashRegisterId: 'a', expectedSequence: 2 }],
    };
    const filtered = filterReportForRegister(report, 'a');
    expect(filtered.chain).toHaveLength(1);
    expect(filtered.sequenceGaps).toHaveLength(1);
    expect(filtered.chainIssues).toHaveLength(0);
  });

  it('classifies outcome: intact, review, broken', () => {
    expect(computeSignatureChainOutcome([], [], [])).toBe('intact');
    expect(
      computeSignatureChainOutcome([{ status: 'Warn' } as never], [], []),
    ).toBe('review');
    expect(
      computeSignatureChainOutcome([{ status: 'Fail' } as never], [], []),
    ).toBe('broken');
    expect(computeSignatureChainOutcome([], [{ expectedSequence: 1 } as never], [])).toBe(
      'broken',
    );
  });

  it('prevSignatureMatches is true only for Pass', () => {
    expect(prevSignatureMatches({ status: 'Pass' })).toBe(true);
    expect(prevSignatureMatches({ status: 'Warn' })).toBe(false);
    expect(prevSignatureMatches({ status: 'Fail' })).toBe(false);
  });
});
