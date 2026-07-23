import { Platform } from 'react-native';

import printerService from './PrinterService';
import { paymentService } from './api/paymentService';
import { formatReceiptHtml } from './receiptFormatter';
import {
  formatRefundReceiptHtml,
  formatStornoReceiptHtml,
  type ReversalReceiptSnapshot,
} from './reversalReceiptFormatter';
import { ReceiptDTO } from '../types/ReceiptDTO';
import { formatUserDate, formatUserTime } from '../utils/dateFormatter';
import { printHtmlAsync } from '../utils/expoPrintShare';
import { normalizeReceiptDto } from '../utils/normalizeReceiptDto';

export type { ReversalReceiptSnapshot } from './reversalReceiptFormatter';

/** RKSV fiş print seçenekleri */
export interface ReceiptPrintOptions {
  /** Demo/fiscal modda "DEMO" etiketi göster */
  isDemoFiscal?: boolean;
}

/** paymentId → base64 QR cache (aynı fiş için tekrar fetch önlenir) */
const qrCache = new Map<string, string>();

class ReceiptPrinter {
  /**
   * Print receipt for payment. RKSV QR backend endpoint'ten base64 olarak gömülür.
   */
  async print(paymentId: string, options?: ReceiptPrintOptions): Promise<void> {
    try {
      const receiptData = (await paymentService.getReceipt(paymentId)) as ReceiptDTO;

      if (!receiptData) {
        throw new Error('Failed to fetch receipt data');
      }

      // QR'ı backend'den al, cache'le (aynı paymentId için tekrar fetch etme)
      let qrBase64: string | undefined = qrCache.get(paymentId);
      if (!qrBase64) {
        const fetched = await paymentService.getQrPngAsBase64(paymentId);
        qrBase64 = fetched ?? undefined;
        if (qrBase64) qrCache.set(paymentId, qrBase64);
      }

      const normalizedData = this.normalizeReceiptDTO(receiptData);
      const html = formatReceiptHtml(normalizedData, {
        qrBase64: qrBase64 ?? undefined,
        isDemoFiscal: options?.isDemoFiscal ?? false,
        verificationUrl: normalizedData.verificationUrl,
      });

      if (Platform.OS === 'web') {
        await this.printWeb(html);
      } else {
        await this.printNative(html);
      }
    } catch (error) {
      console.error('Receipt print failed:', error);
      throw error;
    }
  }

  /**
   * Helper to normalize PascalCase properties to camelCase
   * Because backend sends ReceiptNumber, Date, etc., but frontend expects receiptNumber, date
   */
  private normalizeReceiptDTO(data: unknown): ReceiptDTO {
    return normalizeReceiptDto(data);
  }

  /**
   * Print storno receipt. When <paramref name="stornoPayment"/>.paymentId is set, prints the
   * full RKSV fiscal beleg (QR + TSE). Otherwise prints a thermal summary slip.
   */
  async printStornoReceipt(
    stornoPayment: ReversalReceiptSnapshot,
    originalPayment: ReversalReceiptSnapshot,
    options?: ReceiptPrintOptions
  ): Promise<void> {
    if (stornoPayment.paymentId) {
      await this.print(stornoPayment.paymentId, options);
      return;
    }
    const html = formatStornoReceiptHtml(stornoPayment, originalPayment);
    await this.printHtml(html);
  }

  /**
   * Print partial refund receipt. Uses fiscal print when paymentId is present.
   */
  async printRefundReceipt(
    refundPayment: ReversalReceiptSnapshot,
    originalPayment: ReversalReceiptSnapshot,
    options?: ReceiptPrintOptions
  ): Promise<void> {
    if (refundPayment.paymentId) {
      await this.print(refundPayment.paymentId, options);
      return;
    }
    const html = formatRefundReceiptHtml(refundPayment, originalPayment);
    await this.printHtml(html);
  }

  private async printHtml(html: string): Promise<void> {
    if (Platform.OS === 'web') {
      await this.printWeb(html);
      return;
    }
    await printHtmlAsync(html);
  }

  /**
   * Print using Bondrucker (thermal printer)
   */
  async printBondrucker(paymentId: string): Promise<void> {
    try {
      const receiptDataRaw = await paymentService.getReceipt(paymentId);
      if (!receiptDataRaw) {
        throw new Error('Failed to fetch receipt data');
      }

      const receiptData = this.normalizeReceiptDTO(receiptDataRaw);

      // Map RequestDTO to PrinterService format
      await printerService.printReceipt({
        receiptNumber: receiptData.receiptNumber,
        date: formatUserDate(receiptData.date),
        time: formatUserTime(receiptData.date) || '—',
        cashier:
          receiptData.cashierDisplayName?.trim() ||
          (receiptData.cashierId && receiptData.cashierId.trim()) ||
          '—',
        paymentMethod: receiptData.payments[0]?.method || 'cash',
        items: receiptData.items.map((item) => ({
          name: item.name,
          quantity: item.quantity,
          price: item.unitPrice,
          total: item.totalPrice,
        })),
        subtotal: receiptData.subtotal,
        tax: receiptData.taxAmount,
        total: receiptData.grandTotal,
      });
    } catch (error) {
      console.error('Bondrucker print failed:', error);
      throw error;
    }
  }

  /**
   * Print on web using iframe
   */
  private async printWeb(html: string): Promise<void> {
    await new Promise<void>((resolve, reject) => {
      try {
        const iframe = document.createElement('iframe');
        iframe.style.display = 'none';
        document.body.appendChild(iframe);

        const iframeDoc = iframe.contentDocument || iframe.contentWindow?.document;
        if (!iframeDoc) {
          throw new Error('Failed to access iframe document');
        }

        iframeDoc.open();
        iframeDoc.write(html);
        iframeDoc.close();

        // Wait for content (and images like QR) to load
        iframe.onload = () => {
          setTimeout(() => {
            // Extra delay for QR image rendering
            try {
              iframe.contentWindow?.focus();
              iframe.contentWindow?.print();

              // Clean up
              setTimeout(() => {
                if (document.body.contains(iframe)) {
                  document.body.removeChild(iframe);
                }
                resolve();
              }, 1000);
            } catch (err) {
              if (document.body.contains(iframe)) {
                document.body.removeChild(iframe);
              }
              reject(err);
            }
          }, 500);
        };
      } catch (error) {
        reject(error);
      }
    });
  }

  /**
   * Print on native using Expo Print (preview + thermal width; PDF fallback on failure).
   */
  private async printNative(html: string): Promise<void> {
    await printHtmlAsync(html);
  }
}

/** QR PNG as base64 data URL from GET /api/pos/payment/{id}/qr.png (print template embed). */
export async function fetchQrAsBase64(paymentId: string): Promise<string | null> {
  return await paymentService.getQrPngAsBase64(paymentId);
}

export const receiptPrinter = new ReceiptPrinter();
export default receiptPrinter;

/** Convenience export matching POS call sites. */
export async function printStornoReceipt(
  stornoPayment: ReversalReceiptSnapshot,
  originalPayment: ReversalReceiptSnapshot,
  options?: ReceiptPrintOptions
): Promise<void> {
  await receiptPrinter.printStornoReceipt(stornoPayment, originalPayment, options);
}

export async function printRefundReceipt(
  refundPayment: ReversalReceiptSnapshot,
  originalPayment: ReversalReceiptSnapshot,
  options?: ReceiptPrintOptions
): Promise<void> {
  await receiptPrinter.printRefundReceipt(refundPayment, originalPayment, options);
}
