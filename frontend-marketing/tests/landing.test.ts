import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { INDUSTRY_IDS } from '../src/content/landing';
import { catalogs } from '../src/i18n/catalogs';

const pageSource = readFileSync(
  join(dirname(fileURLToPath(import.meta.url)), '../src/components/LandingPage.tsx'),
  'utf8',
);

describe('marketing landing', () => {
  it('states the German hero', () => {
    expect(catalogs.de.hero.headline).toBe('Die Kasse für jede Branche');
    expect(catalogs.de.hero.subheadline).toBe('RKSV-konform, mandantenfähig, in Minuten startklar.');
    expect(pageSource).toContain('t.hero.headline');
    expect(pageSource).toContain('t.hero.subheadline');
  });

  it('renders the 11 industry tiles', () => {
    expect(INDUSTRY_IDS).toEqual([
      'gastronomy',
      'gastronomy-tables',
      'vet',
      'doctor',
      'hair-salon',
      'mobile-services',
      'handy-shop',
      'taxi',
      'ticket-sales',
      'beherbergung',
      'retail',
    ]);
    expect(pageSource).toContain('INDUSTRIES.map');
  });
});
