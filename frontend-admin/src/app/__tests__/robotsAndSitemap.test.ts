import { existsSync } from 'node:fs';
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

  it('does not ship public/robots.txt beside the App Router route', () => {
    expect(existsSync(join(process.cwd(), 'public/robots.txt'))).toBe(false);
  });
});
