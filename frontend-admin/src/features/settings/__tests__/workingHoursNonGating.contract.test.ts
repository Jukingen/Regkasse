/**
 * Contract: FA working-hours settings must configure website/app intake only.
 * Never use working hours to gate Admin routes, auth, or management APIs.
 */
import fs from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

const settingsRoot = path.join(__dirname, '..');
const sharedAuthRoot = path.join(__dirname, '../../../shared/auth');

function read(relFromSettings: string): string {
  return fs.readFileSync(path.join(settingsRoot, relFromSettings), 'utf8');
}

describe('FA working-hours non-gating contract', () => {
  it('WorkingHoursSettingsForm shows protection note and does not gate UI by hours', () => {
    const src = read('components/WorkingHoursSettingsForm.tsx');
    expect(src).toContain('settings.workingHours.protectionNote');
    expect(src).toContain('<Alert');
    expect(src).not.toMatch(/disabled\s*=\s*\{[^}]*isOpen/);
    expect(src).not.toMatch(/canOrder|IsAcceptingOnlineOrders|restaurantIsOpen/);
  });

  it('working-hours API client is settings CRUD only', () => {
    const src = read('api/workingHoursApi.ts');
    expect(src).toContain('/api/settings/working-hours');
    expect(src).not.toMatch(/\/api\/sites\//);
    expect(src).not.toMatch(/canOrder|isOpen/);
  });

  it('working-hours page is management-only (no open/closed access gate)', () => {
    const page = fs.readFileSync(
      path.join(__dirname, '../../../app/(protected)/settings/working-hours/page.tsx'),
      'utf8'
    );
    expect(page).toMatch(/Never restricts Admin access/i);
    expect(page).toContain('WorkingHoursSettingsForm');
    expect(page).not.toMatch(/canOrder|isOpen\s*===|ClosedScreen/);
  });

  it('auth routePermissions map working-hours to settings.view only (no time gate)', () => {
    const routePerms = fs.readFileSync(path.join(sharedAuthRoot, 'routePermissions.ts'), 'utf8');
    expect(routePerms).toContain("'/settings/working-hours'");
    expect(routePerms).toMatch(/'\/settings\/working-hours'\s*:\s*PERMISSIONS\.SETTINGS_VIEW/);
    expect(routePerms).not.toMatch(/canOrder|IsAcceptingOnlineOrders|restaurantIsOpen/);
  });
});
