import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import { OpenMonatsbelegRequestsCard } from '@/features/dashboard/components/OpenMonatsbelegRequestsCard';
import { I18nProvider } from '@/i18n';

vi.mock('@/features/dashboard/hooks/useMonatsbelegManagerContactRequests', () => ({
  useMonatsbelegManagerContactRequests: () => ({ data: 3, isLoading: false }),
}));

function renderCard() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <OpenMonatsbelegRequestsCard />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('OpenMonatsbelegRequestsCard', () => {
  it('shows the request count and links to /rksv/monatsbelege', () => {
    renderCard();
    expect(screen.getByTestId('open-monatsbeleg-requests-card')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByTestId('open-monatsbeleg-requests-cta')).toHaveAttribute(
      'href',
      '/rksv/monatsbelege'
    );
  });
});
