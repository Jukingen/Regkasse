/**
 * Maps persisted `payment_details.rksv_special_receipt_kind` values to operator-facing labels.
 * Keep aligned with backend `RksvSpecialReceiptKinds` string constants.
 */

export type RksvSpecialReceiptKindCode =
  'Nullbeleg' | 'Startbeleg' | 'Monatsbeleg' | 'Jahresbeleg' | 'Schlussbeleg';

const KNOWN: Record<string, RksvSpecialReceiptKindCode> = {
  Nullbeleg: 'Nullbeleg',
  Startbeleg: 'Startbeleg',
  Monatsbeleg: 'Monatsbeleg',
  Jahresbeleg: 'Jahresbeleg',
  Schlussbeleg: 'Schlussbeleg',
};

export function normalizeRksvSpecialReceiptKind(
  raw: string | null | undefined
): RksvSpecialReceiptKindCode | null {
  if (!raw?.trim()) return null;
  const t = raw.trim();
  return KNOWN[t] ?? null;
}

/** German UI label for receipt tables and RKSV admin surfaces (de-DE operator copy). */
export function rksvSpecialReceiptKindLabelDe(kind: string | null | undefined): string {
  const n = normalizeRksvSpecialReceiptKind(kind);
  if (!n) return kind?.trim() ? kind.trim() : '—';
  switch (n) {
    case 'Nullbeleg':
      return 'Nullbeleg';
    case 'Startbeleg':
      return 'Startbeleg';
    case 'Monatsbeleg':
      return 'Monatsbeleg';
    case 'Jahresbeleg':
      return 'Jahresbeleg';
    case 'Schlussbeleg':
      return 'Schlussbeleg (Endbeleg)';
    default:
      return '—';
  }
}
