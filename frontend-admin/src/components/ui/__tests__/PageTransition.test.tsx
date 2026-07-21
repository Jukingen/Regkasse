import { act, cleanup, render, screen } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { PAGE_LOADER_VISIBLE_MS, PageLoader } from '../PageLoader';
import { PAGE_TRANSITION_MS, PageTransition } from '../PageTransition';

const pathnameRef = { current: '/dashboard' };
const searchRef = { current: '' };

vi.mock('next/navigation', () => ({
  usePathname: () => pathnameRef.current,
  useSearchParams: () => ({
    toString: () => searchRef.current,
  }),
}));

describe('PageTransition', () => {
  beforeEach(() => {
    pathnameRef.current = '/dashboard';
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => {
      cb(0);
      return 1;
    });
  });

  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('wraps children and exposes transition markers', () => {
    render(
      <PageTransition>
        <p>Route body</p>
      </PageTransition>
    );

    const root = document.querySelector('[data-page-transition]');
    expect(root).toBeTruthy();
    expect(root?.getAttribute('data-pathname')).toBe('/dashboard');
    expect(screen.getByText('Route body')).toBeTruthy();
    expect(PAGE_TRANSITION_MS).toBeGreaterThanOrEqual(200);
    expect(PAGE_TRANSITION_MS).toBeLessThanOrEqual(300);
  });

  it('re-applies enter animation on pathname change without dropping children', () => {
    const { rerender } = render(
      <PageTransition>
        <span data-testid="stable-child">A</span>
      </PageTransition>
    );

    const child = screen.getByTestId('stable-child');
    pathnameRef.current = '/users';
    rerender(
      <PageTransition>
        <span data-testid="stable-child">A</span>
      </PageTransition>
    );

    expect(screen.getByTestId('stable-child')).toBe(child);
    const root = document.querySelector('[data-page-transition]');
    expect(root?.getAttribute('data-pathname')).toBe('/users');
    expect(root?.className).toMatch(/enter/);
  });
});

describe('PageLoader', () => {
  beforeEach(() => {
    pathnameRef.current = '/login';
    searchRef.current = '';
  });

  afterEach(() => {
    cleanup();
    vi.useRealTimers();
  });

  it('does not show on the initial mount', () => {
    render(<PageLoader />);
    expect(document.querySelector('[data-page-loader]')).toBeNull();
  });

  it('shows briefly after a client-side route change', () => {
    vi.useFakeTimers();
    const { rerender } = render(<PageLoader />);

    pathnameRef.current = '/dashboard';
    rerender(<PageLoader />);

    expect(document.querySelector('[data-page-loader]')).toBeTruthy();
    expect(PAGE_LOADER_VISIBLE_MS).toBeGreaterThanOrEqual(200);
    expect(PAGE_LOADER_VISIBLE_MS).toBeLessThanOrEqual(300);

    act(() => {
      vi.advanceTimersByTime(PAGE_LOADER_VISIBLE_MS);
    });
    expect(document.querySelector('[data-page-loader]')).toBeNull();
  });
});
