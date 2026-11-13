import { resolveTaxLineNet } from '../services/receiptFormatter';
import { ReceiptDTO } from '../types/ReceiptDTO';
import { Invoice, InvoiceType, TaxSummary } from '../types/invoice';

function mapTaxSummary(dto: ReceiptDTO): TaxSummary {
  const summary: TaxSummary = {
    totalAmount: dto.grandTotal,
    totalTaxAmount: dto.taxAmount,
    standardTaxBase: 0,
    standardTaxAmount: 0,
    reducedTaxBase: 0,
    reducedTaxAmount: 0,
    specialTaxBase: 0,
    specialTaxAmount: 0,
    zeroTaxBase: 0,
    exemptTaxBase: 0,
  };

  for (const rate of dto.taxRates ?? []) {
    const net = resolveTaxLineNet(rate);
    const tax = rate.taxAmount ?? 0;
    if (rate.rate >= 19.5) {
      summary.standardTaxBase += net;
      summary.standardTaxAmount += tax;
    } else if (rate.rate >= 12) {
      summary.specialTaxBase += net;
      summary.specialTaxAmount += tax;
    } else if (rate.rate >= 9) {
      summary.reducedTaxBase += net;
      summary.reducedTaxAmount += tax;
    } else if (rate.rate > 0) {
      summary.specialTaxBase += net;
      summary.specialTaxAmount += tax;
    } else {
      summary.zeroTaxBase += net;
    }
  }

  return summary;
}

export const mapReceiptDtoToInvoice = (dto: ReceiptDTO): Invoice => {
  return {
    id: dto.receiptId,
    receiptNumber: dto.receiptNumber,
    createdAt: dto.date,
    invoiceDate: dto.date,
    invoiceType: InvoiceType.Standard,
    paymentDetails: {
      paymentMethod: dto.payments?.[0]?.method || 'cash',
      amount: dto.payments?.[0]?.amount ?? dto.grandTotal,
      currency: 'EUR',
      cashAmount: dto.payments?.[0]?.tendered,
      changeAmount: dto.payments?.[0]?.change,
    },
    // Map DTO items to Invoice items
    items: (dto.items || []).map((item) => ({
      id: '',
      productId: '',
      productName: item.name,
      quantity: item.quantity,
      unitPrice: item.unitPrice,
      taxType: item.taxRate,
      taxAmount: item.vatAmount ?? 0,
      totalAmount: item.totalPrice,
    })),
    // Map totals
    taxSummary: mapTaxSummary(dto),
    // Map company info to customerDetails (or a new field if Invoice supports it)
    // Invoice type currently has customerDetails but maybe not companyDetails.
    // For ReceiptPrint, we might need to rely on the fact that it hardcodes company info
    // OR update ReceiptPrint to take company info as prop.
    // Let's coerce it for now into a shape that might be useful or just rely on the DTO-fed props in the component.
    customerDetails: {
      companyName: dto.company?.name, // Use this field to pass company name if needed
      address: dto.company?.address,
      taxNumber: dto.company?.taxNumber,
    },
    taxDetails: {} as any,
    isPrinted: false,
    isElectronic: true,
    isVoid: false,
    tseSignature: dto.signature?.value || '',
    tseSerialNumber: dto.signature?.serialNumber || '',
    tseSignatureCounter: 0,
    tseTime: dto.signature?.timestamp || '',
    tseCertificate: '',
    tseProcessType: 'Normal',
    qrCode: dto.signature?.qrData || '',
    status: 'Completed' as any,
    footerText: dto.footerText ?? dto.thankYouMessage,
    thankYouMessage: dto.thankYouMessage ?? dto.footerText,
    companyDescription: dto.company?.description ?? undefined,
    cashierName: dto.cashierDisplayName?.trim() || (dto.cashierId && dto.cashierId.trim()) || '',
    kasseId: dto.kassenID,
    branchName: dto.branchName ?? undefined,
    terminalNumber: dto.terminalNumber ?? undefined,
  };
};
