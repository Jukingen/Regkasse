import { describe, expect, it } from 'vitest';

import { buildDepExportComplianceReport } from '@/features/rksv/dep-export-compliance/exportDepExportComplianceReport';
import type {
  DepExportComplianceStatusDto,
  DepExportRequirementDto,
} from '@/features/rksv/hooks/useDepExportCompliance';

describe('buildDepExportComplianceReport', () => {
  it('aggregates status and requirements into a downloadable report', () => {
    const status: DepExportComplianceStatusDto = {
      tenantId: '11111111-1111-1111-1111-111111111111',
      isCompliant: false,
      totalRequirements: 2,
      completedCount: 1,
      pendingCount: 0,
      overdueCount: 1,
      legalIncompleteCount: 1,
      checkedAtUtc: '2026-07-25T00:00:00.000Z',
      disclaimer: 'Operational readiness only.',
      nextRequirement: null,
      currentPeriod: null,
    };

    const requirements: DepExportRequirementDto[] = [
      {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        tenantId: status.tenantId,
        requirementType: 'Legal',
        title: 'Yearly',
        description: 'Year 2025',
        dueDate: '2026-01-31T00:00:00.000Z',
        isCompleted: false,
        priority: 5,
        category: 'Yearly',
        periodStart: '2025-01-01T00:00:00.000Z',
        periodEnd: '2025-12-31T00:00:00.000Z',
      },
      {
        id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
        tenantId: status.tenantId,
        requirementType: 'Recommended',
        title: 'Quarterly',
        description: 'Q3',
        dueDate: '2026-10-31T00:00:00.000Z',
        isCompleted: true,
        priority: 3,
        category: 'Quarterly',
      },
    ];

    const report = buildDepExportComplianceReport({
      status,
      requirements,
      tenantSlug: 'demo',
      tenantName: 'Demo Cafe',
      generatedAtUtc: '2026-07-25T12:00:00.000Z',
    });

    expect(report.summary.score).toBe(50);
    expect(report.summary.isCompliant).toBe(false);
    expect(report.requirements).toHaveLength(2);
    expect(report.tenantSlug).toBe('demo');
    expect(report.generatedAtUtc).toBe('2026-07-25T12:00:00.000Z');
  });
});
