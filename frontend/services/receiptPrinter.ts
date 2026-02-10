import { Platform } from 'react-native';
import * as Print from 'expo-print';
import { paymentService } from './api/paymentService';
import printerService from './PrinterService';
import { ReceiptDTO } from '../types/ReceiptDTO';

class ReceiptPrinter {
  /**
   * Print receipt for payment
   */
  async print(paymentId: string): Promise<void> {
    try {
      // Fetch receipt data from backend
      // Assuming backend returns ReceiptDTO structure
      const receiptData = await paymentService.getReceipt(paymentId) as ReceiptDTO;

      if (!receiptData) {
        throw new Error('Failed to fetch receipt data');
      }

      // Format receipt HTML
      const html = this.formatReceipt(this.normalizeReceiptDTO(receiptData));

      // Platform-specific printing
      if (Platform.OS === 'web') {
        await this.printWeb(html);
      } else {
        await this.printNative(this.normalizeReceiptDTO(receiptData), html);
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
      } : data.signature
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
  private safeCurrency(value: number | undefined | null): string {
    if (value === undefined || value === null || isNaN(value)) {
      return '0.00';
    }
    return value.toFixed(2).replace('.', ',');
  }

  /**
   * Format receipt data into HTML template
   */
  private formatReceipt(data: ReceiptDTO): string {
    const items = data.items || [];
    const company = data.company || { name: 'Unknown', address: '', taxNumber: '' };
    const taxRates = data.taxRates || [];
    const payments = data.payments || [];
    const signature = data.signature;

    const itemsHtml = items.map(item => `
        <tr>
          <td>${item.name || 'Unknown Item'}</td>
          <td style="text-align: center;">${item.quantity || 0}</td>
          <td style="text-align: right;">${this.safeCurrency(item.unitPrice)}</td>
          <td style="text-align: right;">${this.safeCurrency(item.totalPrice)} ${this.getTaxCode(item.taxRate)}</td>
        </tr>
      `).join('');

    const taxRowsHtml = taxRates.map(rate => `
        <div class="total-row">
           <span>${rate.rate}% (${this.getTaxCode(rate.rate)}) Netto: ${this.safeCurrency(rate.netAmount)} Steuer: ${this.safeCurrency(rate.taxAmount)} Brutto: ${this.safeCurrency(rate.grossAmount)}</span>
        </div>
    `).join('');

    const paymentsHtml = payments.map(p => `
        <div class="total-row">
            <span>${p.method}:</span>
            <span>${this.safeCurrency(p.amount)}</span>
        </div>
        ${p.method === 'cash' ? `
          <div class="total-row">
             <span>Gegeben:</span>
             <span>${this.safeCurrency(p.tendered)}</span>
          </div>
          <div class="total-row">
             <span>Rückgeld:</span>
             <span>${this.safeCurrency(p.change)}</span>
          </div>
        ` : ''}
    `).join('');

    // QR Code logic: Using a clear, offline-capable QR generator script injected
    // OR using a public API for simplicity in this patch.
    // Ideally, for RKSV, we need a robust solution.
    // Using an external API for now to ensure it works without complex JS injection in the restricted Expo Print environment.
    // If offline is critical, we would need to inject a pure JS QRCode library.
    const qrCodeUrl = signature ? `https://api.qrserver.com/v1/create-qr-code/?size=150x150&data=${encodeURIComponent(signature.qrData)}` : '';

    return `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <title>Receipt ${data.receiptNumber || 'N/A'}</title>
        <style>
          body {
            font-family: 'Courier New', monospace;
            max-width: 300px;
            margin: 20px auto;
            padding: 10px;
            color: #000;
          }
          h1 {
            text-align: center;
            font-size: 16px;
            margin: 5px 0;
            font-weight: bold;
          }
          .company-info {
            text-align: center;
            font-size: 12px;
            margin-bottom: 10px;
          }
          .meta-info {
             font-size: 12px;
             margin-bottom: 10px;
             border-bottom: 1px dashed #000;
             padding-bottom: 5px;
          }
          table {
            width: 100%;
            border-collapse: collapse;
            font-size: 12px;
            margin-bottom: 10px;
          }
          th {
            text-align: left;
            border-bottom: 1px solid #000;
            padding: 2px 0;
          }
          td {
            padding: 2px 0;
            vertical-align: top;
          }
          .totals {
            border-top: 1px dashed #000;
            margin-top: 5px;
            padding-top: 5px;
            font-size: 12px;
          }
          .total-row {
            display: flex;
            justify-content: space-between;
            margin: 2px 0;
          }
          .grand-total {
            font-weight: bold;
            font-size: 16px;
            border-top: 1px double #000;
            border-bottom: 1px double #000;
            padding: 5px 0;
            margin: 5px 0;
          }
          .footer {
            text-align: center;
            margin-top: 15px;
            font-size: 10px;
            border-top: 1px dashed #000;
            padding-top: 10px;
          }
          .signature-block {
             margin-top: 10px;
             font-size: 10px;
             word-break: break-all;
             text-align: center;
          }
          .qr-code {
             text-align: center;
             margin: 10px 0;
          }
          @media print {
            body { margin: 0; padding: 0; }
          }
        </style>
      </head>
      <body>
        <div class="company-info">
          <h1>${company.name || 'Store Name'}</h1>
          <div>${company.address || ''}</div>
          <div>UID: ${company.taxNumber || ''}</div>
        </div>
        
        <div class="meta-info">
          <div>Beleg: ${data.receiptNumber}</div>
          <div>Datum: ${new Date(data.date).toLocaleString('de-AT')}</div>
          <div>Kasse: ${data.kassenID} | Kassierer: ${data.cashierName}</div>
        </div>

        <table>
          <thead>
            <tr>
              <th>Art.</th>
              <th style="text-align: center;">Menge</th>
              <th style="text-align: right;">Einh.</th>
              <th style="text-align: right;">Eur</th>
            </tr>
          </thead>
          <tbody>
            ${itemsHtml}
          </tbody>
        </table>

        <div class="totals">
           <div class="total-row grand-total">
            <span>SUMME:</span>
            <span>€ ${this.safeCurrency(data.grandTotal)}</span>
          </div>
          
          <div style="margin-top: 10px; font-size: 10px;">
             ${taxRowsHtml}
          </div>

          <div style="margin-top: 10px;">
            ${paymentsHtml}
          </div>
        </div>

        ${signature ? `
          <div class="signature-block">
            <div style="font-weight: bold; margin-bottom: 5px;">Sicherheitseinrichtung</div>
            <div class="qr-code">
                <img src="${qrCodeUrl}" width="150" height="150" alt="RKSV QR Code" />
            </div>
            <div>${signature.value}</div>
            <div style="margin-top: 2px;">${signature.serialNumber} | ${signature.timestamp}</div>
          </div>
        ` : ''}

        <div class="footer">
          <div>${data.footerText || 'Vielen Dank für Ihren Einkauf!'}</div>
        </div>
      </body>
      </html>
    `;
  }

  private getTaxCode(rate: number): string {
    if (rate >= 20) return 'A';
    if (rate >= 10) return 'B';
    return 'C';
  }

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

export const receiptPrinter = new ReceiptPrinter();
export default receiptPrinter;
