/**
 * Contract: POS Tagesabschluss reminder must stay a non-blocking banner (no auto-close).
 */
import { describe, expect, it } from '@jest/globals';
import fs from 'fs';
import path from 'path';

describe('TagesabschlussReminder (POS)', () => {
  const source = fs.readFileSync(
    path.join(__dirname, '../components/TagesabschlussReminder.tsx'),
    'utf8',
  );

  it('navigates to settings ShiftManager path, not an unknown /tagesabschluss route', () => {
    expect(source).toContain("/(tabs)/settings");
    expect(source).not.toContain("router.push('/tagesabschluss'");
  });

  it('keeps German operator-facing reminder copy', () => {
    expect(source).toContain('Tagesabschluss steht aus');
    expect(source).toContain('Jetzt durchführen');
  });
});
