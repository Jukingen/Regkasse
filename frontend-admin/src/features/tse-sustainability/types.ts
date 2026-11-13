export interface TseSustainabilityTrend {
  date: string;
  label: string;
  carbonKg: number;
  energyKwh: number;
  transactionCount: number;
}

export interface TseSustainabilityReport {
  tenantId: string;
  tenantName?: string | null;
  periodStart: string;
  periodEnd: string;
  generatedAt: string;
  totalCarbonEmission: number;
  perTransactionEmission: number;
  perDeviceEmission: number;
  totalEnergyUsage: number;
  averageDeviceEnergyUsage: number;
  carbonSaved: number;
  energySaved: number;
  costSaved: number;
  industryAverage: number;
  percentile: number;
  activeDeviceCount: number;
  softOrDemoDeviceCount: number;
  signedTransactions: number;
  totalTransactions: number;
  carbonTrend: TseSustainabilityTrend[];
  diagnosticOnly: boolean;
}

export interface TseSustainabilitySuggestion {
  code: string;
  title: string;
  description: string;
  severity: string;
  estimatedCarbonSavedKgPerMonth: number;
  estimatedEnergySavedKwhPerMonth: number;
  estimatedCostSavedEurPerMonth: number;
}

export interface TseSustainabilityOptimizationResult {
  tenantId: string;
  generatedAt: string;
  potentialCarbonSavedKg: number;
  potentialEnergySavedKwh: number;
  potentialCostSavedEur: number;
  suggestions: TseSustainabilitySuggestion[];
  diagnosticOnly: boolean;
}
