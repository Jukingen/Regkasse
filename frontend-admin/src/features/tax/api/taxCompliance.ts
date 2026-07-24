import { customInstance } from '@/lib/axios';

export type ComplianceIssue = {
  severity: string;
  code: string;
  message: string;
  action: string;
  affectedCount: number;
  sampleProductIds?: string[];
};

export type ComplianceReport = {
  isCompliant: boolean;
  issues: ComplianceIssue[];
  totalProducts: number;
  compliantProducts: number;
  nonCompliantProducts: number;
  checkedAtUtc: string;
};

export const taxComplianceQueryKey = ['tax-compliance'] as const;

export async function getTaxComplianceReport(): Promise<ComplianceReport> {
  return customInstance<ComplianceReport>({
    url: '/api/admin/tax-compliance',
    method: 'GET',
  });
}
