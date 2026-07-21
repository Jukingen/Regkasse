import { render } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import {
  DEFAULT_IMAGE_QUALITY,
  OptimizedImage,
  isDataOrBlobSrc,
  isSvgSrc,
  shouldSkipImageOptimization,
} from '@/components/OptimizedImage';

vi.mock('next/image', () => ({
  default: (props: Record<string, unknown>) => {
    // Strip Next-only props so jsdom img stays valid.
    const {
      priority: _p,
      placeholder: _ph,
      blurDataURL: _b,
      unoptimized: _u,
      quality: _q,
      fill: _f,
      ...img
    } = props;
    // eslint-disable-next-line @next/next/no-img-element, jsx-a11y/alt-text
    return <img data-testid="next-image" {...img} />;
  },
}));

describe('OptimizedImage helpers', () => {
  it('detects data/blob and svg sources', () => {
    expect(isDataOrBlobSrc('data:image/png;base64,xx')).toBe(true);
    expect(isDataOrBlobSrc('blob:http://localhost/1')).toBe(true);
    expect(isSvgSrc('/logo.svg')).toBe(true);
    expect(isSvgSrc('/photo.png')).toBe(false);
  });

  it('skips optimization for unknown remote hosts and keeps API hosts', () => {
    expect(shouldSkipImageOptimization('https://cdn.example.com/a.jpg')).toBe(true);
    expect(shouldSkipImageOptimization('https://api.regkasse.at/files/a.webp')).toBe(false);
    expect(shouldSkipImageOptimization('http://localhost:5184/x.png')).toBe(false);
    expect(shouldSkipImageOptimization('/logo.svg')).toBe(true);
    expect(shouldSkipImageOptimization('/templates/preview.png')).toBe(false);
  });
});

describe('OptimizedImage', () => {
  it('uses lazy loading and width-derived sizes by default', () => {
    const { getByTestId } = render(
      <OptimizedImage src="/photo.png" alt="Product" width={120} height={120} />
    );
    const img = getByTestId('next-image');
    expect(img.getAttribute('loading')).toBe('lazy');
    expect(img.getAttribute('sizes')).toBe('120px');
    expect(img.getAttribute('alt')).toBe('Product');
  });

  it('omits loading when priority is set (above-the-fold)', () => {
    const { getByTestId } = render(
      <OptimizedImage src="/logo.svg" alt="" width={32} height={32} priority />
    );
    const img = getByTestId('next-image');
    expect(img.getAttribute('loading')).toBeNull();
  });

  it('exports default quality constant for raster optimization', () => {
    expect(DEFAULT_IMAGE_QUALITY).toBe(75);
  });
});
