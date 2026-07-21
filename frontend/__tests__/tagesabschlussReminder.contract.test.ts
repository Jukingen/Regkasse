/**
 * Contract: POS Tagesabschluss reminder must stay a non-blocking banner (no auto-close,
 * no sales lock). Working hours only drive visibility of the reminder.
 */
import { describe, expect, it } from '@jest/globals';
import fs from 'fs';
import path from 'path';

describe('TagesabschlussReminder (POS)', () => {
  const source = fs.readFileSync(
    path.join(__dirname, '../components/TagesabschlussReminder.tsx'),
    'utf8'
  );
  const layoutSource = fs.readFileSync(path.join(__dirname, '../app/(tabs)/_layout.tsx'), 'utf8');

  it('opens DailyClosingModal directly instead of navigating to settings', () => {
    expect(source).toContain('setShowModal(true)');
    expect(source).toContain('DailyClosingModal');
    expect(source).not.toContain('useRouter');
    expect(source).not.toContain('/(tabs)/settings');
    expect(source).not.toContain("router.push('/tagesabschluss'");
  });

  it('shows reminder only when shouldShowReminder and canClose (display gate, not sales lock)', () => {
    expect(source).toMatch(
      /!shouldShowReminder\s*\|\|\s*!canClose|!canClose\s*\|\|\s*!shouldShowReminder/
    );
  });

  it('documents that it never blocks POS operations', () => {
    expect(source).toMatch(/never blocks/i);
    expect(source).toMatch(/Never auto-closes/i);
  });

  it('does not use working hours to disable payments or cart', () => {
    expect(source).not.toMatch(/posOperationsAllowed\s*===\s*false/);
    expect(source).not.toMatch(/restaurantIsOpen\s*===\s*false/);
    expect(source).not.toContain('disabled={true}');
    expect(source).not.toContain('pointerEvents="box-none"');
  });

  it('is mounted as a sibling banner above Tabs (not a wrapping gate)', () => {
    expect(layoutSource).toContain('<TagesabschlussReminder />');
    expect(layoutSource).toContain('<Tabs');
    const reminderIdx = layoutSource.indexOf('<TagesabschlussReminder />');
    const tabsIdx = layoutSource.indexOf('<Tabs');
    expect(reminderIdx).toBeGreaterThan(-1);
    expect(tabsIdx).toBeGreaterThan(reminderIdx);
  });

  it('uses warning emoji and settings i18n reminder keys', () => {
    expect(source).toContain('⚠️');
    expect(source).toContain("t('settings:dailyClosing.reminder.title')");
    expect(source).toContain("t('settings:dailyClosing.reminder.cta')");
  });

  it('shows a countdown label for remaining time until closing', () => {
    expect(source).toContain('countdownLabel');
  });
});
