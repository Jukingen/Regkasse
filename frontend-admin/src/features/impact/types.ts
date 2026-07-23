export type ImpactChangeType = 'TaxRate' | 'Currency' | 'ProductPrice' | number;

export type ImpactSeverity = 'Info' | 'Warning' | 'Critical' | string;

export type ImpactAffectedRecords = {
  products: number;
  payments: number;
  invoices: number;
};

export type ImpactReport = {
  title: string;
  summary: string;
  changeType: ImpactChangeType;
  tenantId: string;
  affectedRecords: ImpactAffectedRecords;
  estimatedFinancialImpact?: number | null;
  estimatedFinancialImpactCurrency?: string | null;
  recommendations: string[];
  warnings: string[];
  severity: ImpactSeverity;
};

export type SimulateImpactRequest = {
  tenantId: string;
  changeType: 'TaxRate' | 'Currency' | 'ProductPrice';
  newTaxRate?: number;
  currentTaxRate?: number;
  newCurrency?: string;
  productPriceUpdates?: Array<{ productId: string; newPrice: number }>;
};
