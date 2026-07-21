import type {
  RksvComplianceReport,
  RksvComplianceSequenceGap,
  RksvComplianceSignatureChainItem,
  RksvComplianceTseSignatureMissing,
} from '@/features/rksv/compliance/types';

export type SignatureChainOutcome = 'intact' | 'review' | 'broken';

export function isChainItemIssue(item: RksvComplianceSignatureChainItem): boolean {
  return item.status !== 'Pass';
}

export function isChainItemFail(item: RksvComplianceSignatureChainItem): boolean {
  return item.status === 'Fail';
}

export function prevSignatureMatches(item: RksvComplianceSignatureChainItem): boolean {
  return item.status === 'Pass';
}

export function formatSignaturePreview(prefix: string | null | undefined): string {
  if (!prefix) return '—';
  if (prefix.length <= 17) return prefix;
  return `${prefix.slice(0, 8)}…${prefix.slice(-8)}`;
}

export function filterReportForRegister(
  report: RksvComplianceReport | undefined,
  cashRegisterId: string | undefined
): {
  chain: RksvComplianceSignatureChainItem[];
  chainIssues: RksvComplianceSignatureChainItem[];
  sequenceGaps: RksvComplianceSequenceGap[];
  tseMissing: RksvComplianceTseSignatureMissing[];
} {
  if (!report || !cashRegisterId) {
    return { chain: [], chainIssues: [], sequenceGaps: [], tseMissing: [] };
  }
  const chain = (report.signatureChain ?? []).filter((c) => c.cashRegisterId === cashRegisterId);
  const chainIssues = chain.filter(isChainItemIssue);
  const sequenceGaps = (report.sequenceGaps ?? []).filter(
    (g) => g.cashRegisterId === cashRegisterId
  );
  const tseMissing = (report.tseSignatureMissing ?? []).filter(
    (t) => t.cashRegisterId === cashRegisterId
  );
  return { chain, chainIssues, sequenceGaps, tseMissing };
}

export function computeSignatureChainOutcome(
  chainIssues: RksvComplianceSignatureChainItem[],
  sequenceGaps: RksvComplianceSequenceGap[],
  tseMissing: RksvComplianceTseSignatureMissing[]
): SignatureChainOutcome {
  const hasFail = chainIssues.some(isChainItemFail);
  if (hasFail || sequenceGaps.length > 0 || tseMissing.length > 0) return 'broken';
  if (chainIssues.length > 0) return 'review';
  return 'intact';
}
