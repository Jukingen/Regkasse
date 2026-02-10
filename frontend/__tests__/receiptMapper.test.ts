import { mapReceiptDtoToInvoice } from '../utils/receiptMapper';
import { ReceiptDTO } from '../types/ReceiptDTO';

const mockReceiptDTO: ReceiptDTO = {
    receiptId: 'test-id',
    receiptNumber: '12345',
    date: '2023-01-01T12:00:00Z',
    cashierName: 'Test Cashier',
    company: {
        name: 'Test Company',
        address: 'Test Address',
        taxNumber: 'ATU12345678'
    },
    kassenID: 'KASSE01',
    items: [
        { name: 'Item 1', quantity: 1, unitPrice: 10, totalPrice: 10, taxRate: 20 }
    ],
    taxRates: [],
    subtotal: 8.33,
    taxAmount: 1.67,
    grandTotal: 10.0,
    payments: [{ method: 'cash', amount: 10 }],
    footerText: 'Test Footer',
    signature: {
        algorithm: 'ES256',
        value: 'sig-value',
        serialNumber: 'serial-123',
        timestamp: '2023-01-01T12:00:00Z',
        qrData: 'qr-data'
    }
};

describe('receiptMapper', () => {
    it('should map ReceiptDTO to Invoice correctly', () => {
        const result = mapReceiptDtoToInvoice(mockReceiptDTO);

        expect(result.id).toBe(mockReceiptDTO.receiptId);
        expect(result.receiptNumber).toBe(mockReceiptDTO.receiptNumber);

        // Verify TaxAmount mapping
        expect(result.taxSummary.totalTaxAmount).toBe(mockReceiptDTO.taxAmount);

        // Verify Footer Text mapping
        expect(result.footerText).toBe(mockReceiptDTO.footerText);

        // Verify Company Info mapping
        expect(result.customerDetails?.companyName).toBe(mockReceiptDTO.company.name);
        expect(result.customerDetails?.address).toBe(mockReceiptDTO.company.address);
        expect(result.customerDetails?.taxNumber).toBe(mockReceiptDTO.company.taxNumber);

        // Verify Signature
        expect(result.tseSignature).toBe(mockReceiptDTO.signature.value);

        // Verify Cashier and Kasse
        expect(result.cashierName).toBe(mockReceiptDTO.cashierName);
        expect(result.kasseId).toBe(mockReceiptDTO.kassenID);
    });
});
