/**
 * @vitest-environment jsdom
 */
import { describe, expect, it } from 'vitest';

import {
  BACKUP_COMPLIANCE_PATH,
  BACKUP_COSTS_PATH,
  BACKUP_DASHBOARD_PATH,
  BACKUP_HUB_LANDING_PATH,
  BACKUP_PERFORMANCE_PATH,
  BACKUP_RESTORE_HISTORY_PATH,
  BACKUP_SECONDARY_NAV_ITEMS,
  backupPathFromPathname,
} from '@/shared/backupAreaRoutes';

describe('backup overview hub routes', () => {
  it('lands overview on /backup', () => {
    expect(BACKUP_HUB_LANDING_PATH).toBe('/backup');
    expect(BACKUP_SECONDARY_NAV_ITEMS[0]?.href).toBe('/backup');
  });

  it('maps /backup and /backup/dashboard to overview hub for nav selection', () => {
    expect(backupPathFromPathname('/backup')).toBe(BACKUP_HUB_LANDING_PATH);
    expect(backupPathFromPathname('/backup/dashboard')).toBe(BACKUP_HUB_LANDING_PATH);
    expect(BACKUP_DASHBOARD_PATH).toBe('/backup/dashboard');
  });

  it('exposes performance secondary nav and path mapping', () => {
    expect(BACKUP_PERFORMANCE_PATH).toBe('/backup/performance');
    expect(backupPathFromPathname('/backup/performance')).toBe(BACKUP_PERFORMANCE_PATH);
    expect(BACKUP_SECONDARY_NAV_ITEMS.some((i) => i.href === BACKUP_PERFORMANCE_PATH)).toBe(true);
  });

  it('exposes compliance secondary nav and path mapping', () => {
    expect(BACKUP_COMPLIANCE_PATH).toBe('/backup/compliance');
    expect(backupPathFromPathname('/backup/compliance')).toBe(BACKUP_COMPLIANCE_PATH);
    expect(BACKUP_SECONDARY_NAV_ITEMS.some((i) => i.href === BACKUP_COMPLIANCE_PATH)).toBe(true);
  });

  it('exposes costs secondary nav and path mapping', () => {
    expect(BACKUP_COSTS_PATH).toBe('/backup/costs');
    expect(backupPathFromPathname('/backup/costs')).toBe(BACKUP_COSTS_PATH);
    expect(BACKUP_SECONDARY_NAV_ITEMS.some((i) => i.href === BACKUP_COSTS_PATH)).toBe(true);
  });

  it('exposes restore-history secondary nav and path mapping', () => {
    expect(BACKUP_RESTORE_HISTORY_PATH).toBe('/backup/restore-history');
    expect(backupPathFromPathname('/backup/restore-history')).toBe(BACKUP_RESTORE_HISTORY_PATH);
    expect(BACKUP_SECONDARY_NAV_ITEMS.some((i) => i.href === BACKUP_RESTORE_HISTORY_PATH)).toBe(
      true
    );
  });
});
