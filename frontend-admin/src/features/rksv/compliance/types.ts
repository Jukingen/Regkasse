/** Mirrors backend `RksvComplianceReportDto` (OpenAPI schema not yet typed in generated client). */

export type RksvComplianceReportSummary = {
  registersCovered?: number;
  fiscalReceiptsScanned?: number;
  specialReceiptsCount?: number;
  signatureChainBreaks?: number;
  sequenceGapCount?: number;
  tseSignatureMissingCount?: number;
  qrFormatInvalidCount?: number;
  qrFormatMissingCount?: number;
  overallPass?: boolean;
};

export type RksvComplianceSpecialReceipt = {
  paymentId?: string;
  receiptId?: string | null;
  receiptNumber?: string;
  kind?: string;
  nullbelegActsAsJahresbeleg?: boolean;
  cashRegisterId?: string;
  registerNumber?: string | null;
  issuedAtUtc?: string | null;
  year?: number | null;
  month?: number | null;
  hasTseSignature?: boolean;
};

export type RksvComplianceSignatureChainItem = {
  cashRegisterId?: string;
  registerNumber?: string | null;
  receiptId?: string;
  receiptNumber?: string;
  issuedAtUtc?: string;
  signaturePrefix?: string | null;
  prevSignaturePrefix?: string | null;
  expectedPrevSignaturePrefix?: string | null;
  status?: string;
  issue?: string | null;
};

export type RksvComplianceSequenceGap = {
  cashRegisterId?: string;
  registerNumber?: string | null;
  sequenceDateUtc?: string;
  expectedSequence?: number;
  previousReceiptNumber?: string | null;
  nextReceiptNumber?: string | null;
};

export type RksvComplianceTseSignatureMissing = {
  paymentId?: string;
  receiptId?: string | null;
  receiptNumber?: string;
  cashRegisterId?: string;
  registerNumber?: string | null;
  issuedAtUtc?: string | null;
  specialReceiptKind?: string | null;
  paymentSignatureMissing?: boolean;
  receiptSignatureMissing?: boolean;
};

export type RksvComplianceQrValidationItem = {
  receiptId?: string;
  receiptNumber?: string;
  cashRegisterId?: string;
  registerNumber?: string | null;
  issuedAtUtc?: string;
  qrPayloadMissing?: boolean;
  isValidFormat?: boolean;
  errors?: string[];
};

export type RksvComplianceReport = {
  generatedAtUtc?: string;
  cashRegisterId?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  summary?: RksvComplianceReportSummary;
  specialReceipts?: RksvComplianceSpecialReceipt[];
  signatureChain?: RksvComplianceSignatureChainItem[];
  sequenceGaps?: RksvComplianceSequenceGap[];
  tseSignatureMissing?: RksvComplianceTseSignatureMissing[];
  qrPayloadValidation?: RksvComplianceQrValidationItem[];
  legalNoticeDe?: string;
};

export type RksvComplianceReportQueryParams = {
  cashRegisterId?: string;
  fromUtc?: string;
  toUtc?: string;
};
