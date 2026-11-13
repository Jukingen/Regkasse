import { describe, expect, it } from 'vitest';

import {
  getLicenseTestManualSuccessMessage,
  getLicenseTestScenarioSuccessMessage,
} from '@/features/license/utils/licenseTestMessages';

const t = (key: string, params?: Record<string, string | number>) => {
  const table: Record<string, string> = {
    'license.testPanel.scenarioSuccess': `Lizenz auf ${params?.days ?? 0} Tage gesetzt.`,
    'license.testPanel.scenarioExpiredSuccess': 'Lizenz als abgelaufen gesetzt.',
    'license.testPanel.manualSuccess': `Lizenz gültig bis ${params?.date ?? ''} gesetzt.`,
  };
  return table[key] ?? key;
};

describe('licenseTestMessages', () => {
  it('builds scenario success message with day count', () => {
    expect(getLicenseTestScenarioSuccessMessage('Days1', t)).toBe('Lizenz auf 1 Tage gesetzt.');
    expect(getLicenseTestScenarioSuccessMessage('Days7', t)).toBe('Lizenz auf 7 Tage gesetzt.');
  });

  it('builds expired scenario message', () => {
    expect(getLicenseTestScenarioSuccessMessage('Expired', t)).toBe(
      'Lizenz als abgelaufen gesetzt.'
    );
  });

  it('builds manual update message with formatted date', () => {
    const message = getLicenseTestManualSuccessMessage('2026-07-16T12:00:00.000Z', t);
    expect(message).toContain('Lizenz gültig bis');
    expect(message).toContain('gesetzt.');
  });
});
