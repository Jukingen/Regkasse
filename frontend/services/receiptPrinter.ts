import { Platform } from 'react-native';
import * as Print from 'expo-print';
import { paymentService } from './api/paymentService';
import printerService from './PrinterService';

export interface ReceiptData {
    receiptNumber: string;
    paymentId: string;
    timestamp: string;
    tableNumber?: number;
    cashierName: string;
    items: ReceiptItem[];
    subtotal: number;
    taxAmount: number;
    grandTotal: number;
    paymentMethod: string;
    tseSignature?: string;
    companyInfo: CompanyInfo;
}

export interface ReceiptItem {
    name: string;
    quantity: number;
    unitPrice: number;
    total: number;
    taxRate: number;
}

export interface CompanyInfo {
    name: string;
    address: string;
    taxNumber: string;
}

class ReceiptPrinter {
    /**
     * Print receipt for payment
     */
    async print(paymentId: string): Promise<void> {
        try {
            // Fetch receipt data from backend
            const receiptData = await paymentService.getReceipt(paymentId);

            if (!receiptData) {
                throw new Error('Failed to fetch receipt data');
            }

            // Format receipt HTML
            const html = this.formatReceipt(receiptData);

            // Platform-specific printing
            if (Platform.OS === 'web') {
                await this.printWeb(html);
            } else {
                await this.printNative(receiptData, html);
            }
        } catch (error) {
            console.error('Receipt print failed:', error);
            throw error;
        }
    }

    /**
     * Print using Bondrucker (thermal printer)
     */
    async printBondrucker(paymentId: string): Promise<void> {
        try {
            const receiptData = await paymentService.getReceipt(paymentId);

            if (!receiptData) {
                throw new Error('Failed to fetch receipt data');
            }

            // Use existing PrinterService for thermal printer
            await printerService.printReceipt({
                receiptNumber: receiptData.receiptNumber,
                timestamp: new Date(receiptData.timestamp),
                tableNumber: receiptData.tableNumber,
                items: receiptData.items.map(item => ({
                    name: item.name,
                    quantity: item.quantity,
                    price: item.unitPrice,
                    total: item.total
                })),
                subtotal: receiptData.subtotal,
                tax: receiptData.taxAmount,
                total: receiptData.grandTotal,
                paymentMethod: receiptData.paymentMethod,
                cashierName: receiptData.cashierName,
                companyInfo: receiptData.companyInfo,
                tseSignature: receiptData.tseSignature
            });
        } catch (error) {
            console.error('Bondrucker print failed:', error);
            throw error;
        }
    }

    /**
     * Format receipt data into HTML template
     */
    private formatReceipt(data: ReceiptData): string {
        const itemsHtml = data.items.map(item => `
      <tr>
        <td>${item.name}</td>
        <td style="text-align: center;">${item.quantity}</td>
        <td style="text-align: right;">€${item.unitPrice.toFixed(2)}</td>
        <td style="text-align: right;">€${item.total.toFixed(2)}</td>
      </tr>
    `).join('');

        return `
      <!DOCTYPE html>
      <html>
      <head>
        <meta charset="utf-8">
        <title>Receipt ${data.receiptNumber}</title>
        <style>
          body {
            font-family: 'Courier New', monospace;
            max-width: 300px;
            margin: 20px auto;
            padding: 10px;
          }
          h1 {
            text-align: center;
            font-size: 18px;
            margin: 10px 0;
          }
          .company-info {
            text-align: center;
            font-size: 12px;
            margin-bottom: 15px;
          }
          .receipt-header {
            border-bottom: 2px dashed #000;
            padding-bottom: 10px;
            margin-bottom: 10px;
          }
          table {
            width: 100%;
            border-collapse: collapse;
            font-size: 12px;
          }
          th {
            text-align: left;
            border-bottom: 1px solid #000;
            padding: 5px 0;
          }
          td {
            padding: 3px 0;
          }
          .totals {
            border-top: 2px dashed #000;
            margin-top: 10px;
            padding-top: 10px;
          }
          .total-row {
            display: flex;
            justify-content: space-between;
            margin: 3px 0;
          }
          .grand-total {
            font-weight: bold;
            font-size: 14px;
            border-top: 1px solid #000;
            padding-top: 5px;
            margin-top: 5px;
          }
          .footer {
            text-align: center;
            margin-top: 15px;
            font-size: 10px;
            border-top: 2px dashed #000;
            padding-top: 10px;
          }
          @media print {
            body {
              margin: 0;
              padding: 10px;
            }
          }
        </style>
      </head>
      <body>
        <div class="company-info">
          <h1>${data.companyInfo.name}</h1>
          <div>${data.companyInfo.address}</div>
          <div>UID: ${data.companyInfo.taxNumber}</div>
        </div>
        
        <div class="receipt-header">
          <div><strong>Receipt:</strong> ${data.receiptNumber}</div>
          <div><strong>Date:</strong> ${new Date(data.timestamp).toLocaleString('de-AT')}</div>
          ${data.tableNumber ? `<div><strong>Table:</strong> ${data.tableNumber}</div>` : ''}
          <div><strong>Cashier:</strong> ${data.cashierName}</div>
        </div>

        <table>
          <thead>
            <tr>
              <th>Item</th>
              <th style="text-align: center;">Qty</th>
              <th style="text-align: right;">Price</th>
              <th style="text-align: right;">Total</th>
            </tr>
          </thead>
          <tbody>
            ${itemsHtml}
          </tbody>
        </table>

        <div class="totals">
          <div class="total-row">
            <span>Subtotal:</span>
            <span>€${data.subtotal.toFixed(2)}</span>
          </div>
          <div class="total-row">
            <span>Tax (${(data.taxAmount / data.subtotal * 100).toFixed(0)}%):</span>
            <span>€${data.taxAmount.toFixed(2)}</span>
          </div>
          <div class="total-row grand-total">
            <span>TOTAL:</span>
            <span>€${data.grandTotal.toFixed(2)}</span>
          </div>
          <div class="total-row" style="margin-top: 10px;">
            <span>Payment Method:</span>
            <span>${data.paymentMethod}</span>
          </div>
        </div>

        ${data.tseSignature ? `
          <div class="footer">
            <div><strong>TSE Signature</strong></div>
            <div style="word-break: break-all; font-size: 8px;">${data.tseSignature}</div>
          </div>
        ` : ''}

        <div class="footer">
          <div>Thank you for your visit!</div>
        </div>
      </body>
      </html>
    `;
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

                // Wait for content to load
                iframe.onload = () => {
                    try {
                        iframe.contentWindow?.focus();
                        iframe.contentWindow?.print();

                        // Clean up after print
                        setTimeout(() => {
                            document.body.removeChild(iframe);
                            resolve();
                        }, 1000);
                    } catch (err) {
                        document.body.removeChild(iframe);
                        reject(err);
                    }
                };
            } catch (error) {
                reject(error);
            }
        });
    }

    /**
     * Print on native using Expo Print
     */
    private async printNative(data: ReceiptData, html: string): Promise<void> {
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
