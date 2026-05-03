import { normalizeRksvSpecialReceiptKind } from '@/features/rksv-operations/rksvSpecialReceiptDisplay';

/** Admin `useI18n().t` compatible signature (no direct `i18next` dependency for typecheck). */
export type ReceiptTranslateFn = (key: string, options?: Record<string, string | number>) => string;

/** Localized label for RKSV `rksv_special_receipt_kind` values in admin receipt surfaces. */
export function formatRksvSpecialReceiptKindDisplay(t: ReceiptTranslateFn, kind: string | null | undefined): string {
    if (!kind?.trim()) return t('receipts.detail.card.valueSpecialKindNone');
    const n = normalizeRksvSpecialReceiptKind(kind);
    if (!n) return t('receipts.specialKind.unknown');
    return t(`receipts.specialKind.${n.toLowerCase() as 'nullbeleg' | 'startbeleg' | 'monatsbeleg' | 'jahresbeleg' | 'schlussbeleg'}`);
}
