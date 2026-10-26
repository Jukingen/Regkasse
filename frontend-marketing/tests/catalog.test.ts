import { describe, expect, it } from 'vitest';
import { catalogs } from '../src/i18n/catalogs';
import { formatEuroPeriod, PUBLISHED_TIERS, tierAmount } from '../src/i18n/pricing';
import { LANGS, type Catalog } from '../src/i18n/types';
import { INDUSTRY_IDS } from '../src/content/landing';

function leafPaths(value: unknown, prefix = ''): string[] {
  if (Array.isArray(value)) {
    return value.flatMap((item, index) => leafPaths(item, `${prefix}[${index}]`));
  }
  if (value && typeof value === 'object') {
    return Object.entries(value).flatMap(([key, child]) => leafPaths(child, prefix ? `${prefix}.${key}` : key));
  }
  return [prefix];
}

describe('marketing catalogs', () => {
  it('ships complete en and tr catalogs', () => {
    const german = leafPaths(catalogs.de);
    expect(german.length).toBeGreaterThan(0);
    for (const lang of LANGS) {
      const catalog = catalogs[lang] as Catalog;
      expect(leafPaths(catalog)).toEqual(german);
      for (const path of german) {
        const value = path.split('.').reduce<unknown>((current, key) => {
          const index = key.match(/^(.+)\[(\d+)\]$/);
          if (!index) return (current as Record<string, unknown>)[key];
          const list = (current as Record<string, unknown>)[index[1]];
          return (list as unknown[])[Number(index[2])];
        }, catalog);
        expect(typeof value).toBe('string');
        expect((value as string).trim().length).toBeGreaterThan(0);
      }
    }
  });

  it('has 10 FAQ items and all 11 industries in each language', () => {
    for (const lang of LANGS) {
      expect(catalogs[lang].faq).toHaveLength(10);
      for (const id of INDUSTRY_IDS) {
        expect(catalogs[lang].industries[id]?.name.trim().length).toBeGreaterThan(0);
        expect(catalogs[lang].industries[id]?.description.trim().length).toBeGreaterThan(0);
      }
    }
    expect(catalogs.de.faq.map((item) => item.question)).toEqual([
      'RKSV-konform?',
      'Wie lange dauert die Einrichtung?',
      'Kann ich meine Daten exportieren?',
      'Welche Branchen?',
      'Was kostet die Kündigung?',
      'Probe-Mandant?',
      'Backup-Frequenz?',
      'DSGVO?',
      'Support-Sprachen?',
      'Multi-Standort?',
    ]);
  });

  it('formats published euro prices', () => {
    const starter = PUBLISHED_TIERS[0];
    const professional = PUBLISHED_TIERS[1];
    expect(starter.monthlyEur).toBe(29);
    expect(professional.monthlyEur).toBe(79);
    expect(formatEuroPeriod(tierAmount(29, false), 'Monat')).toBe('€29/Monat');
    expect(formatEuroPeriod(tierAmount(79, false), 'Monat')).toBe('€79/Monat');
    expect(formatEuroPeriod(tierAmount(29, true), 'Jahr')).toBe('€290/Jahr');
    expect(formatEuroPeriod(tierAmount(79, true), 'Jahr')).toBe('€790/Jahr');
  });
});
