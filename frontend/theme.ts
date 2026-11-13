import { RECEIPT_FONT_FALLBACK, RECEIPT_FONT_FAMILY } from './constants/fonts';

export const theme = {
  fonts: {
    /** Preferred receipt face (OCRA-B) with web/native fallbacks. */
    receipt: RECEIPT_FONT_FAMILY,
    /** Explicit fallback when custom font failed to load. */
    receiptFallback: RECEIPT_FONT_FALLBACK,
  },
};
