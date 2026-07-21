import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { MetricCard } from '@/features/backup-dr/components/MetricCard';

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('MetricCard', () => {
  it('renders title, value, and trend', () => {
    render(
      <MetricCard
        title="Erfolgsrate"
        value="92%"
        status="success"
        trend={4}
        trendLabel="vs Vormonat"
      />
    );
    expect(screen.getByText('Erfolgsrate')).toBeInTheDocument();
    expect(screen.getByText('92%')).toBeInTheDocument();
    expect(screen.getByText(/4% vs Vormonat/)).toBeInTheDocument();
  });
});
