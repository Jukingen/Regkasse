/**
 * Re-export for printing helpers (canonical implementation: ../receiptPrinter.ts).
 */
export {
  default,
  receiptPrinter,
  printStornoReceipt,
  printRefundReceipt,
  fetchQrAsBase64,
  type ReceiptPrintOptions,
  type ReversalReceiptSnapshot,
} from '../receiptPrinter';
