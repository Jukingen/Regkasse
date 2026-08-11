import { describe, expect, it } from 'vitest';

import {
  healthLevelFromScore,
  mapBackupDashboardHealth,
  normalizeContentValidationStatus,
  normalizeRpoStatus,
} from '@/features/backup/logic/backupDashboardHealthPresentation';

describe('backupDashboardHealthPresentation', () => {
  it('maps score bands to healthy/warning/critical', () => {
    expect(healthLevelFromScore(100)).toBe('healthy');
    expect(healthLevelFromScore(80)).toBe('healthy');
    expect(healthLevelFromScore(79)).toBe('warning');
    expect(healthLevelFromScore(50)).toBe('warning');
    expect(healthLevelFromScore(49)).toBe('critical');
    expect(healthLevelFromScore(0)).toBe('critical');
  });

  it('normalizes RPO and content statuses', () => {
    expect(normalizeRpoStatus('Ok')).toBe('Healthy');
    expect(normalizeRpoStatus('Warning')).toBe('AtRisk');
    expect(normalizeRpoStatus('Overdue')).toBe('Critical');
    expect(normalizeContentValidationStatus('passed')).toBe('passed');
    expect(normalizeContentValidationStatus('available')).toBe('unknown');
  });

  it('builds widget view-model with emoji', () => {
    const vm = mapBackupDashboardHealth({
      healthScore: 92,
      healthLevel: 'healthy',
      lastVerificationStatus: 1,
      contentValidationSummaryStatus: 'passed',
      rpoStatus: 'Healthy',
      rpoHours: 3,
    });

    expect(vm.healthEmoji).toBe('🟢');
    expect(vm.healthLevel).toBe('healthy');
    expect(vm.verificationStatus).toBe('Passed');
    expect(vm.contentValidationStatus).toBe('passed');
    expect(vm.rpoStatus).toBe('Healthy');
    expect(vm.rpoHours).toBe(3);
  });
});
