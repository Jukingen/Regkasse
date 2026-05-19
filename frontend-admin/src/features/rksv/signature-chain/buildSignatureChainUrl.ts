export type SignatureChainUrlParams = {
  cashRegisterId?: string;
  receiptId?: string;
  fromUtc?: string;
  toUtc?: string;
  /** When true, the page runs verification on load. */
  autoVerify?: boolean;
};

export function buildSignatureChainVerificationUrl(params: SignatureChainUrlParams): string {
  const sp = new URLSearchParams();
  if (params.cashRegisterId) sp.set('cashRegisterId', params.cashRegisterId);
  if (params.receiptId) sp.set('receiptId', params.receiptId);
  if (params.fromUtc) sp.set('fromUtc', params.fromUtc);
  if (params.toUtc) sp.set('toUtc', params.toUtc);
  if (params.autoVerify) sp.set('autoVerify', '1');
  const q = sp.toString();
  return `/rksv/signature-chain${q ? `?${q}` : ''}`;
}
