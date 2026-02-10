export enum InvoiceType {
  Standard = 'Standard',       // Normal satış fişi
  Refund = 'Refund',          // İade fişi
  Correction = 'Correction',   // Düzeltme fişi
  Void = 'Void',              // İptal fişi
  DailyReport = 'DailyReport', // Günlük rapor
  MonthlyReport = 'MonthlyReport', // Aylık rapor
  YearlyReport = 'YearlyReport',   // Yıllık rapor
  Training = 'Training',      // Eğitim modu fişi
  Test = 'Test'               // Test fişi
}

export enum TaxType {
  Standard = 20,  // %20
  Reduced = 10,   // %10
  Special = 13,   // %13
  Zero = 0,       // %0
  Exempt = -1     // Vergiden muaf
}

export interface TaxSummary {
  standardTaxBase: number;
  standardTaxAmount: number;
  reducedTaxBase: number;
  reducedTaxAmount: number;
  specialTaxBase: number;
  specialTaxAmount: number;
  zeroTaxBase: number;
  exemptTaxBase: number;
  totalTaxAmount: number;
  totalAmount: number;
}

export interface PaymentDetails {
  paymentMethod: string;
  amount: number;
  currency: string;
  cardType?: string;
  cardLastDigits?: string;
  transactionId?: string;
  voucherCode?: string;
  voucherAmount?: number;
  cashAmount?: number;
  cardAmount?: number;
  changeAmount?: number;
}

export interface CustomerDetails {
  customerId?: string;
  taxNumber?: string;
  companyName?: string;
  firstName?: string;
  lastName?: string;
  address?: string;
  city?: string;
  postalCode?: string;
  country?: string;
  email?: string;
  phone?: string;
  vatNumber?: string;
}

export interface Invoice {
  id: string;
  receiptNumber: string;
  invoiceType: InvoiceType;
  taxDetails: Record<TaxType, number>;
  taxSummary: TaxSummary;
  paymentDetails: PaymentDetails;
  customerDetails?: CustomerDetails;
  isPrinted: boolean;
  isElectronic: boolean;
  isVoid: boolean;
  voidReason?: string;
  originalInvoiceId?: string;
  relatedInvoiceIds?: string[];
  tseSignature: string;
  tseSignatureCounter: number;
  tseTime: string;
  tseSerialNumber: string;
  tseCertificate: string;
  tseProcessType: string;
  tseProcessData?: any;
  qrCode: string;
  status: InvoiceStatus;
  items: InvoiceItem[];
  createdAt: string;
  invoiceDate: string; // Added to match UI usage
  updatedAt?: string;
  footerText?: string;
  cashierName?: string;
  kasseId?: string;

  // Flattened properties used in UI
  customer?: {
    firstName?: string;
    lastName?: string;
  };
  totalAmount?: number;
  taxAmount?: number;
  paymentMethod?: string;
  companyName?: string;
  companyAddress?: string;
  companyEmail?: string;
  companyPhone?: string;
  taxNumber?: string;
}

export enum InvoiceStatus {
  Pending = 'Pending',
  Completed = 'Completed',
  Cancelled = 'Cancelled',
  Refunded = 'Refunded'
}

export interface InvoiceItem {
  id: string;
  productId: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  taxType: TaxType;
  taxAmount: number;
  totalAmount: number;
  discountAmount?: number;
  notes?: string;
}

export interface InvoiceCreateRequest {
  customerId: string;
  items: {
    productId: string;
    quantity: number;
    notes?: string;
  }[];
  notes?: string;
} 