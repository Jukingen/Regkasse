import { describe, expect, it } from '@jest/globals';
import fs from 'fs';
import path from 'path';

import {
  computePosWorkingHoursStatus,
  resolveEffectiveWorkingHoursDay,
  type PosWorkingHoursExtended,
} from '../utils/workingHoursStatus';

const openDay = (closeTime = '22:00', openTime = '09:00') => ({
  openTime,
  closeTime,
  isClosed: false,
});

function sampleHours(overrides: Partial<PosWorkingHoursExtended> = {}): PosWorkingHoursExtended {
  const day = openDay();
  return {
    reminderHoursBeforeClosing: 1,
    stopOnlineOrdersMinutesBeforeClose: 30,
    autoClosePOSAtClosing: false,
    closedDayMessage: 'Heute geschlossen',
    specialDays: [],
    monday: day,
    tuesday: day,
    wednesday: day,
    thursday: day,
    friday: day,
    saturday: day,
    sunday: day,
    ...overrides,
  };
}

describe('resolveEffectiveWorkingHoursDay', () => {
  it('applies special-day closed override', () => {
    const hours = sampleHours({
      specialDays: [{ date: '2026-12-24', isClosed: true }],
    });
    const resolved = resolveEffectiveWorkingHoursDay(hours, 2026, 12, 24);
    expect(resolved.isSpecialDay).toBe(true);
    expect(resolved.day.isClosed).toBe(true);
  });

  it('applies special-day custom hours', () => {
    const hours = sampleHours({
      specialDays: [{ date: '2026-12-31', isClosed: false, openTime: '10:00', closeTime: '18:00' }],
    });
    const resolved = resolveEffectiveWorkingHoursDay(hours, 2026, 12, 31);
    expect(resolved.isSpecialDay).toBe(true);
    expect(resolved.day.openTime).toBe('10:00');
    expect(resolved.day.closeTime).toBe('18:00');
  });
});

describe('computePosWorkingHoursStatus', () => {
  it('always allows POS operations even when restaurant is closed', () => {
    const now = new Date('2026-12-24T10:00:00.000Z');
    const status = computePosWorkingHoursStatus({
      now,
      timeZone: 'Europe/Vienna',
      workingHours: sampleHours({
        specialDays: [{ date: '2026-12-24', isClosed: true }],
      }),
    });
    expect(status.posOperationsAllowed).toBe(true);
    expect(status.restaurantIsOpen).toBe(false);
    expect(status.isOpen).toBe(false);
  });

  it('reports open mid-day Vienna Monday', () => {
    const now = new Date('2026-07-20T10:00:00.000Z');
    const status = computePosWorkingHoursStatus({
      now,
      timeZone: 'Europe/Vienna',
      workingHours: sampleHours(),
    });
    expect(status.posOperationsAllowed).toBe(true);
    expect(status.openTime).toBe('09:00');
    expect(status.closeTime).toBe('22:00');
    expect(status.restaurantIsOpen).toBe(true);
    expect(status.isClosingSoon).toBe(false);
    expect(status.showReminder).toBe(false);
    expect(status.message).toBe('Geöffnet');
    expect(status.timeUntilClose).toBeGreaterThan(60);
    expect(status.timeUntilOpen).toBe(0);
  });

  it('reports closing soon within reminder window', () => {
    const now = new Date('2026-07-20T19:30:00.000Z');
    const status = computePosWorkingHoursStatus({
      now,
      timeZone: 'Europe/Vienna',
      workingHours: sampleHours({ reminderHoursBeforeClosing: 1 }),
    });
    expect(status.posOperationsAllowed).toBe(true);
    expect(status.restaurantIsOpen).toBe(true);
    expect(status.isClosingSoon).toBe(true);
    expect(status.showReminder).toBe(true);
    expect(status.message).toBe('Schließung bald');
    expect(status.timeUntilClose).toBeLessThan(60);
  });

  it('reports closed on special day with custom message', () => {
    const now = new Date('2026-12-24T10:00:00.000Z');
    const status = computePosWorkingHoursStatus({
      now,
      timeZone: 'Europe/Vienna',
      workingHours: sampleHours({
        closedDayMessage: 'Heiligabend geschlossen',
        specialDays: [{ date: '2026-12-24', isClosed: true }],
      }),
    });
    expect(status.posOperationsAllowed).toBe(true);
    expect(status.restaurantIsOpen).toBe(false);
    expect(status.isSpecialDay).toBe(true);
    expect(status.message).toBe('Heiligabend geschlossen');
    expect(status.timeUntilClose).toBe(0);
  });

  it('reports closed before opening with timeUntilOpen', () => {
    const now = new Date('2026-07-20T05:00:00.000Z');
    const status = computePosWorkingHoursStatus({
      now,
      timeZone: 'Europe/Vienna',
      workingHours: sampleHours(),
    });
    expect(status.posOperationsAllowed).toBe(true);
    expect(status.restaurantIsOpen).toBe(false);
    expect(status.timeUntilOpen).toBeGreaterThan(100);
    expect(status.timeUntilOpen).toBeLessThan(130);
    expect(status.message).toBe('Heute geschlossen');
  });
});

describe('POS working-hours non-gating contract', () => {
  it('WorkingHoursStatus is display-only and states POS stays ready', () => {
    const src = fs.readFileSync(
      path.join(__dirname, '..', 'components', 'WorkingHoursStatus.tsx'),
      'utf8'
    );
    expect(src).toMatch(/DISPLAY ONLY|display only/i);
    expect(src).toMatch(/POS immer bereit/);
    expect(src).toMatch(/pointerEvents=["']none["']/);
    expect(src).not.toMatch(/disabled\s*=\s*\{?\s*!restaurantIsOpen/);
    expect(src).not.toMatch(/if\s*\(\s*!posOperationsAllowed/);
  });

  it('cash-register screen never early-returns on closed hours / !isOpen', () => {
    const src = fs.readFileSync(
      path.join(__dirname, '..', 'app', '(tabs)', 'cash-register.tsx'),
      'utf8'
    );
    expect(src).toMatch(/WORKING HOURS:\s*never gate/i);
    expect(src).not.toMatch(/if\s*\(\s*!isOpen\s*\)/);
    expect(src).not.toMatch(/if\s*\(\s*!restaurantIsOpen\s*\)/);
    expect(src).not.toMatch(/useWorkingHours/);
  });

  it('Header re-exports display-only WorkingHoursStatus', () => {
    const src = fs.readFileSync(path.join(__dirname, '..', 'components', 'Header.tsx'), 'utf8');
    expect(src).toMatch(/DISPLAY ONLY|display-only/i);
    expect(src).toContain('WorkingHoursStatus');
  });

  it('does not gate payment / cart / order modules on useWorkingHours', () => {
    const root = path.join(__dirname, '..');
    const guardedGlobs = [
      'services/api/payment',
      'services/api/cart',
      'services/api/checkout',
      'hooks/useCart',
      'hooks/usePayment',
      'contexts/CartContext',
      'contexts/PaymentContext',
      'app/(tabs)/cash-register.tsx',
      'app/(tabs)/cart.tsx',
      'app/(tabs)/payment.tsx',
    ];

    for (const rel of guardedGlobs) {
      const full = path.join(root, rel);
      if (!fs.existsSync(full)) continue;
      const walk = (p: string): string[] => {
        const st = fs.statSync(p);
        if (st.isFile()) return [p];
        return fs.readdirSync(p).flatMap((name) => walk(path.join(p, name)));
      };
      for (const file of walk(full)) {
        if (!/\.(ts|tsx)$/.test(file)) continue;
        const src = fs.readFileSync(file, 'utf8');
        expect(src).not.toMatch(
          /useWorkingHours|restaurantIsOpen|computePosWorkingHoursStatus|canOrder|IsAcceptingOnlineOrders/
        );
      }
    }
  });

  it('useWorkingHours always forces posOperationsAllowed true', () => {
    const src = fs.readFileSync(path.join(__dirname, '..', 'hooks', 'useWorkingHours.ts'), 'utf8');
    expect(src).toMatch(/posOperationsAllowed:\s*true/);
    expect(src).toMatch(/posOperationsAllowed:\s*true\s*,\s*loading/);
    expect(src).toMatch(/Never use this hook to block/i);
  });
});
