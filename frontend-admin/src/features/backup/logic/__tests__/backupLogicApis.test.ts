import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  getBackupVerifyChecksumQueryKey,
  verifyBackupChecksum,
} from '@/features/backup/logic/backupChecksumVerifyApi';
import {
  getBackupContentValidation,
  getBackupContentValidationQueryKey,
  normalizeContentValidationStatus,
} from '@/features/backup/logic/backupContentValidationApi';
import {
  getBackupDashboardHealth,
  getBackupDashboardHealthQueryKey,
} from '@/features/backup/logic/backupDashboardHealthApi';
import {
  BACKUP_DASHBOARD_STATS_PATH,
  getBackupDashboardStats,
  getBackupDashboardStatsQueryKey,
} from '@/features/backup/logic/backupDashboardStatsApi';
import { BACKUP_DRILL_RUN_PATH, runRestoreDrill } from '@/features/backup/logic/backupDrillApi';
import { getPitrAvailability, validatePitrRestorePoint } from '@/features/backup/logic/backupPitrApi';
import {
  BACKUP_STORAGE_COSTS_PATH,
  getBackupStorageCosts,
  getBackupStorageCostsQueryKey,
} from '@/features/backup/logic/backupStorageCostsApi';
import {
  getBackupVerificationReport,
  getBackupVerificationReportQueryKey,
} from '@/features/backup/logic/backupVerificationReportApi';

const mockCustomInstance = vi.fn();

vi.mock('@/lib/axios', () => ({
  customInstance: (config: unknown) => mockCustomInstance(config),
}));

describe('backup logic API clients', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('builds stable query keys', () => {
    expect(getBackupVerifyChecksumQueryKey('run-1')).toEqual([
      '/api/admin/backup/runs',
      'run-1',
      'verify-checksum',
    ]);
    expect(getBackupContentValidationQueryKey('run-2')).toEqual([
      '/api/admin/backup/runs',
      'run-2',
      'content-validation',
    ]);
    expect(getBackupVerificationReportQueryKey('run-3')).toEqual([
      '/api/admin/backup/runs',
      'run-3',
      'verification-report',
    ]);
    expect(getBackupDashboardHealthQueryKey()).toEqual([
      '/api/admin/backup/dashboard/health',
    ]);
    expect(getBackupDashboardStatsQueryKey()).toEqual([BACKUP_DASHBOARD_STATS_PATH]);
    expect(getBackupStorageCostsQueryKey()).toEqual([BACKUP_STORAGE_COSTS_PATH]);
  });

  it('normalizes content validation status tokens', () => {
    expect(normalizeContentValidationStatus('Passed')).toBe('passed');
    expect(normalizeContentValidationStatus('FAILED')).toBe('failed');
    expect(normalizeContentValidationStatus('warning')).toBe('partial');
    expect(normalizeContentValidationStatus('partial')).toBe('partial');
    expect(normalizeContentValidationStatus('unavailable')).toBe('unavailable');
    expect(normalizeContentValidationStatus(null)).toBe('other');
    expect(normalizeContentValidationStatus('weird')).toBe('other');
  });

  it('verifyBackupChecksum GETs run verify-checksum', async () => {
    const payload = { runId: 'r1', isValid: true };
    mockCustomInstance.mockResolvedValue(payload);
    await expect(verifyBackupChecksum('r1')).resolves.toEqual(payload);
    expect(mockCustomInstance).toHaveBeenCalledWith({
      url: '/api/admin/backup/runs/r1/verify-checksum',
      method: 'GET',
    });
  });

  it('getBackupContentValidation GETs content-validation', async () => {
    mockCustomInstance.mockResolvedValue({ runId: 'r1', overallStatus: 'Passed' });
    await getBackupContentValidation('r1');
    expect(mockCustomInstance).toHaveBeenCalledWith({
      url: '/api/admin/backup/runs/r1/content-validation',
      method: 'GET',
    });
  });

  it('runRestoreDrill POSTs empty body by default', async () => {
    mockCustomInstance.mockResolvedValue({ runId: 'd1', success: true, status: 'Succeeded' });
    await runRestoreDrill();
    expect(mockCustomInstance).toHaveBeenCalledWith({
      url: BACKUP_DRILL_RUN_PATH,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: {},
    });
  });

  it('runRestoreDrill forwards backupRunId', async () => {
    mockCustomInstance.mockResolvedValue({ runId: 'd1', success: true, status: 'Queued' });
    await runRestoreDrill({ backupRunId: 'backup-9', idempotencyKey: 'idem-1' });
    expect(mockCustomInstance).toHaveBeenCalledWith(
      expect.objectContaining({
        data: { backupRunId: 'backup-9', idempotencyKey: 'idem-1' },
      })
    );
  });

  it('PITR availability and validate call expected paths', async () => {
    mockCustomInstance.mockResolvedValueOnce({ walArchivingEnabled: true });
    await getPitrAvailability();
    expect(mockCustomInstance).toHaveBeenCalledWith({
      url: '/api/admin/backup/pitr/availability',
      method: 'GET',
    });

    mockCustomInstance.mockResolvedValueOnce({ isValid: true });
    await validatePitrRestorePoint({ targetTimeUtc: '2026-08-01T00:00:00Z' });
    expect(mockCustomInstance).toHaveBeenCalledWith({
      url: '/api/admin/backup/pitr/validate',
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: { targetTimeUtc: '2026-08-01T00:00:00Z' },
    });
  });

  it('dashboard health/stats and storage costs GET wrappers', async () => {
    mockCustomInstance.mockResolvedValue({});
    await getBackupDashboardHealth();
    await getBackupDashboardStats();
    await getBackupStorageCosts();
    expect(mockCustomInstance).toHaveBeenNthCalledWith(1, {
      url: '/api/admin/backup/dashboard/health',
      method: 'GET',
    });
    expect(mockCustomInstance).toHaveBeenNthCalledWith(2, {
      url: BACKUP_DASHBOARD_STATS_PATH,
      method: 'GET',
    });
    expect(mockCustomInstance).toHaveBeenNthCalledWith(3, {
      url: BACKUP_STORAGE_COSTS_PATH,
      method: 'GET',
    });
  });

  it('getBackupVerificationReport GETs verification-report', async () => {
    mockCustomInstance.mockResolvedValue({ backupRunId: 'r1', status: 'Verified' });
    await getBackupVerificationReport('r1');
    expect(mockCustomInstance).toHaveBeenCalledWith({
      url: '/api/admin/backup/runs/r1/verification-report',
      method: 'GET',
    });
  });
});
