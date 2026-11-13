import { render } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { OptimizedImage } from '@/components/OptimizedImage';

vi.mock('next/image', () => ({
  default: (props: Record<string, unknown>) => {
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
    return <img data-testid="next-image" data-unoptimized={_u ? 'true' : undefined} {...img} />;
  },
}));

describe('OptimizedImage', () => {
  it('uses lazy loading by default for raster images', () => {
    const { getByTestId } = render(
      <OptimizedImage src="/photo.png" alt="Product" width={120} height={120} />
    );
    const img = getByTestId('next-image');
    expect(img.getAttribute('loading')).toBe('lazy');
    expect(img.getAttribute('alt')).toBe('Product');
  });

  it('omits loading when priority is set (above-the-fold)', () => {
    const { getByTestId } = render(
      <OptimizedImage src="/logo.svg" alt="" width={32} height={32} priority />
    );
    const img = getByTestId('next-image');
    expect(img.getAttribute('loading')).toBeNull();
  });

  it('marks data/blob and svg sources as unoptimized', () => {
    const { getByTestId, rerender } = render(
      <OptimizedImage src="data:image/png;base64,xx" alt="" width={32} height={32} />
    );
    expect(getByTestId('next-image').getAttribute('data-unoptimized')).toBe('true');

    rerender(<OptimizedImage src="/logo.svg" alt="" width={32} height={32} />);
    expect(getByTestId('next-image').getAttribute('data-unoptimized')).toBe('true');

    rerender(<OptimizedImage src="/photo.png" alt="" width={32} height={32} />);
    expect(getByTestId('next-image').getAttribute('data-unoptimized')).toBeNull();
  });
});
