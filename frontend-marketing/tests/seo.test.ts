import { describe, expect, it } from 'vitest';
import robots from '../src/app/robots';
import sitemap from '../src/app/sitemap';
import { buildMetadata } from '../src/seo/metadata';
import { PUBLIC_PATHS } from '../src/seo/site';

describe('marketing seo', () => {
  it('lists the four public URLs in the sitemap', () => {
    const urls = sitemap().map((entry) => new URL(entry.url).pathname);
    expect(urls).toEqual([...PUBLIC_PATHS]);
    expect(urls).toHaveLength(4);
  });

  it('allows crawlers and points at the sitemap', () => {
    const rules = robots();
    expect(rules.rules).toEqual({ userAgent: '*', allow: '/' });
    expect(rules.sitemap).toBe('https://regkasse.at/sitemap.xml');
  });

  it('publishes the German title, description, and canonical home URL', () => {
    const metadata = buildMetadata();
    expect(metadata.title).toMatchObject({ default: 'Regkasse — Die Kasse für jede Branche' });
    expect(metadata.description).toBe(
      'RKSV-konforme Registrierkasse für Gastronomie, Handel, Handwerk und mehr. Mandantenfähig, in Minuten startklar.',
    );
    expect(metadata.alternates).toMatchObject({ canonical: '/' });
    expect(metadata.robots).toMatchObject({ index: true, follow: true });
    expect(metadata.openGraph).toMatchObject({ images: [{ url: '/og-image.png', width: 1200, height: 630 }] });
  });
});
