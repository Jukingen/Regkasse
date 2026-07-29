import type {
  DepExportComplianceStatusDto,
  DepExportRequirementDto,
} from '@/features/rksv/hooks/useDepExportCompliance';
import { computeComplianceScore } from '@/features/rksv/hooks/useDepExportCompliance';
import { createJsonExportBlob, triggerBlobDownload } from '@/lib/download/exportDownload';

export type DepExportComplianceFullReport = {
  generatedAtUtc: string;
  tenantId: string;
  tenantSlug?: string | null;
  tenantName?: string | null;
  disclaimer: string;
  summary: {
    isCompliant: boolean;
    score: number;
    totalRequirements: number;
    completedCount: number;
    pendingCount: number;
    overdueCount: number;
    legalIncompleteCount: number;
    checkedAtUtc: string;
  };
  currentPeriod: DepExportComplianceStatusDto['currentPeriod'];
  nextRequirement: DepExportRequirementDto | null;
  requirements: DepExportRequirementDto[];
};

export function buildDepExportComplianceReport(input: {
  status: DepExportComplianceStatusDto;
  requirements: DepExportRequirementDto[];
  tenantSlug?: string | null;
  tenantName?: string | null;
  generatedAtUtc?: string;
}): DepExportComplianceFullReport {
  const { status, requirements } = input;
  return {
    generatedAtUtc: input.generatedAtUtc ?? new Date().toISOString(),
    tenantId: status.tenantId,
    tenantSlug: input.tenantSlug ?? null,
    tenantName: input.tenantName ?? null,
    disclaimer: status.disclaimer,
    summary: {
      isCompliant: status.isCompliant,
      score: computeComplianceScore(status),
      totalRequirements: status.totalRequirements,
      completedCount: status.completedCount,
      pendingCount: status.pendingCount,
      overdueCount: status.overdueCount,
      legalIncompleteCount: status.legalIncompleteCount,
      checkedAtUtc: status.checkedAtUtc,
    },
    currentPeriod: status.currentPeriod ?? null,
    nextRequirement: status.nextRequirement ?? null,
    requirements: [...requirements],
  };
}

function stamp(iso: string): string {
  return iso.replace(/[:.]/g, '-').slice(0, 19);
}

function escapeCsvCell(value: unknown): string {
  const s = value == null ? '' : String(value);
  if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

export function exportDepExportComplianceReportJson(report: DepExportComplianceFullReport): void {
  const blob = createJsonExportBlob(report);
  const slug = report.tenantSlug?.trim() || 'tenant';
  triggerBlobDownload(
    blob,
    `dep-export-compliance-report_${slug}_${stamp(report.generatedAtUtc)}_UTC.json`
  );
}

export function exportDepExportComplianceReportCsv(report: DepExportComplianceFullReport): void {
  const rows: string[][] = [
    [
      'category',
      'requirementType',
      'title',
      'description',
      'dueDate',
      'isCompleted',
      'priority',
      'periodStart',
      'periodEnd',
    ],
  ];

  for (const r of report.requirements) {
    rows.push([
      r.category,
      r.requirementType,
      r.title,
      r.description,
      r.dueDate ?? '',
      String(r.isCompleted),
      String(r.priority),
      r.periodStart ?? '',
      r.periodEnd ?? '',
    ]);
  }

  const csv = rows.map((row) => row.map(escapeCsvCell).join(',')).join('\n');
  const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
  const slug = report.tenantSlug?.trim() || 'tenant';
  triggerBlobDownload(
    blob,
    `dep-export-compliance-report_${slug}_${stamp(report.generatedAtUtc)}_UTC.csv`
  );
}
