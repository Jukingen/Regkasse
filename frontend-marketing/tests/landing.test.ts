import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { HERO, INDUSTRY_IDS } from '../src/content/landing';

const pageSource = readFileSync(
  join(dirname(fileURLToPath(import.meta.url)), '../src/app/page.tsx'),
  'utf8',
);

describe('marketing landing', () => {
  it('states the hero', () => {
    expect(HERO.headline).toBe('Die Kasse für jede Branche');
    expect(HERO.subheadline).toBe('RKSV-konform, mandantenfähig, in Minuten startklar.');
    expect(pageSource).toContain('HERO.headline');
    expect(pageSource).toContain('HERO.subheadline');
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
