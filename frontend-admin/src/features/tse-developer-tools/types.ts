export interface TseDevToolCheck {
  id: string;
  name: string;
  isSuccess: boolean;
  details: string;
  severity: 'Info' | 'Warning' | 'Error' | string;
}

export interface TseDevToolResult {
  tenantId: string;
  tenantName?: string | null;
  operation: string;
  success: boolean;
  summary: string;
  generatedAtUtc: string;
  developmentOnly: boolean;
  results: TseDevToolCheck[];
  metadata?: Record<string, string> | null;
}

export interface TseDeveloperToolsAvailability {
  enabled: boolean;
  environmentName: string;
  message: string;
}
