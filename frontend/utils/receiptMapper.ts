import { ReceiptDTO } from '../types/ReceiptDTO';
import { Invoice, InvoiceType, TaxType } from '../types/invoice';

export const mapReceiptDtoToInvoice = (dto: ReceiptDTO): Invoice => {
    return {
        id: dto.receiptId,
        receiptNumber: dto.receiptNumber,
        createdAt: dto.date,
        invoiceType: InvoiceType.Standard,
        paymentDetails: {
            paymentMethod: dto.payments?.[0]?.method || 'cash',
            amount: dto.grandTotal,
            currency: 'EUR'
        },
        // Map DTO items to Invoice items
        items: (dto.items || []).map(item => ({
            id: '',
            productId: '',
            productName: item.name,
            quantity: item.quantity,
            unitPrice: item.unitPrice,
            taxType: item.taxRate as TaxType,
            taxAmount: 0,
            totalAmount: item.totalPrice
        })),
        // Map totals
        taxSummary: {
            totalAmount: dto.grandTotal,
            totalTaxAmount: dto.taxAmount,
            standardTaxBase: 0, standardTaxAmount: 0, reducedTaxBase: 0, reducedTaxAmount: 0,
            specialTaxBase: 0, specialTaxAmount: 0, zeroTaxBase: 0, exemptTaxBase: 0
        },
        // Map company info to customerDetails (or a new field if Invoice supports it)
        // Invoice type currently has customerDetails but maybe not companyDetails.
        // For ReceiptPrint, we might need to rely on the fact that it hardcodes company info 
        // OR update ReceiptPrint to take company info as prop.
        // Let's coerce it for now into a shape that might be useful or just rely on the DTO-fed props in the component.
        customerDetails: {
            companyName: dto.company?.name, // Use this field to pass company name if needed
            address: dto.company?.address,
            taxNumber: dto.company?.taxNumber
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
        footerText: dto.footerText,
        cashierName: dto.cashierName,
        kasseId: dto.kassenID
    };
};
