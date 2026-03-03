import { Platform } from 'react-native';
import * as Print from 'expo-print';
import { paymentService } from './api/paymentService';
import printerService from './PrinterService';
import { ReceiptDTO } from '../types/ReceiptDTO';
import { formatReceiptHtml } from './receiptFormatter';

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
      const receiptData = await paymentService.getReceipt(paymentId) as ReceiptDTO;

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
        await this.printNative(normalizedData, html);
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
  private normalizeReceiptDTO(data: any): ReceiptDTO {
    // If it's already normalized (camelCase), just return it
    if (data.receiptNumber && !data.ReceiptNumber) {
      return data as ReceiptDTO;
    }

    // Map PascalCase to camelCase
    return {
      receiptId: data.ReceiptId || data.receiptId || '',
      receiptNumber: data.ReceiptNumber || data.receiptNumber || 'Unknown',
      date: data.Date || data.date || new Date().toISOString(),
      cashierName: data.CashierName || data.cashierName || 'Unknown',
      tableNumber: data.TableNumber || data.tableNumber,
      kassenID: data.KassenID || data.kassenID || 'Unknown',

      company: {
        name: data.Company?.Name || data.company?.name || 'Unknown',
        address: data.Company?.Address || data.company?.address || '',
        taxNumber: data.Company?.TaxNumber || data.company?.taxNumber || ''
      },

      items: (data.Items || data.items || []).map((item: any) => ({
        name: item.Name || item.name || '',
        quantity: item.Quantity || item.quantity || 0,
        unitPrice: item.UnitPrice || item.unitPrice || 0,
        totalPrice: item.TotalPrice || item.totalPrice || 0,
        taxRate: item.TaxRate || item.taxRate || 0
      })),

      subtotal: data.SubTotal || data.subtotal || 0,
      taxAmount: data.TaxAmount || data.taxAmount || 0,
      grandTotal: data.GrandTotal || data.grandTotal || 0,

      taxRates: (data.TaxRates || data.taxRates || []).map((t: any) => ({
        rate: t.Rate || t.rate || 0,
        netAmount: t.NetAmount || t.netAmount || 0,
        taxAmount: t.TaxAmount || t.taxAmount || 0,
        grossAmount: t.GrossAmount || t.grossAmount || 0
      })),

      payments: (data.Payments || data.payments || []).map((p: any) => ({
        method: p.Method || p.method || 'cash',
        amount: p.Amount || p.amount || 0,
        tendered: p.Tendered || p.tendered,
        change: p.Change || p.change
      })),

      footerText: data.FooterText || data.footerText,

      signature: data.Signature ? {
        algorithm: data.Signature.Algorithm || data.signature?.algorithm || '',
        value: data.Signature.Value || data.signature?.value || '',
        serialNumber: data.Signature.SerialNumber || data.signature?.serialNumber || '',
        timestamp: data.Signature.Timestamp || data.signature?.timestamp || '',
        qrData: data.Signature.QrData || data.signature?.qrData || ''
      } : data.signature,
      verificationUrl: data.VerificationUrl ?? data.verificationUrl
    };
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
        date: new Date(receiptData.date).toLocaleDateString('de-AT'),
        time: new Date(receiptData.date).toLocaleTimeString('de-AT'),
        cashier: receiptData.cashierName,
        paymentMethod: receiptData.payments[0]?.method || 'cash',
        items: receiptData.items.map(item => ({
          name: item.name,
          quantity: item.quantity,
          price: item.unitPrice,
          total: item.totalPrice
        })),
        subtotal: receiptData.subtotal,
        tax: receiptData.taxAmount,
        total: receiptData.grandTotal
      });
    } catch (error) {
      console.error('Bondrucker print failed:', error);
      throw error;
    }
  }

  /**
   * Helper to safely format currency
   */
  /**
   * Print on web using iframe
   */
  private async printWeb(html: string): Promise<void> {
    return new Promise((resolve, reject) => {
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
          setTimeout(() => { // Extra delay for QR image rendering
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
   * Print on native using Expo Print
   */
  private async printNative(data: ReceiptDTO, html: string): Promise<void> {
    try {
      await Print.printAsync({
        html,
        width: 300, // Thermal printer width
      });
    } catch (error) {
      console.error('Native print failed:', error);
      throw error;
    }
  }
}

/** Backend /api/Payment/{id}/qr.png'den QR PNG'yi base64 data URL olarak getirir. Print template embed için. */
export async function fetchQrAsBase64(paymentId: string): Promise<string | null> {
  return paymentService.getQrPngAsBase64(paymentId);
}

export const receiptPrinter = new ReceiptPrinter();
export default receiptPrinter;
