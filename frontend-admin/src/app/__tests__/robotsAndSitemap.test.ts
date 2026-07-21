import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import robots from '@/app/robots';
import sitemap from '@/app/sitemap';

describe('admin SEO disallow', () => {
  it('robots metadata disallows all user agents from /', () => {
    const result = robots();
    expect(result.rules).toEqual({
      userAgent: '*',
      disallow: '/',
    });
    expect(result).not.toHaveProperty('sitemap');
  });

  it('sitemap is empty (no admin routes listed)', () => {
    expect(sitemap()).toEqual([]);
  });

  it('public/robots.txt mirrors Disallow: /', () => {
    const text = readFileSync(join(process.cwd(), 'public/robots.txt'), 'utf8');
    expect(text).toMatch(/User-agent:\s*\*/i);
    expect(text).toMatch(/Disallow:\s*\/\s*$/m);
    expect(text).not.toMatch(/^Allow:/im);
    expect(text).not.toMatch(/^Sitemap:/im);
  });
});
