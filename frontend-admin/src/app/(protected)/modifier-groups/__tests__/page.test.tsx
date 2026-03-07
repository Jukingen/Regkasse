/**
 * Regression tests after legacy modifier migration removal.
 * Ensures: add-on groups page renders; no legacy migration UI; active add-on behavior only.
 */

import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import ModifierGroupsPage from '../page';

vi.mock('@/lib/api/modifierGroups', () => ({
  getModifierGroups: vi.fn(() => Promise.resolve([])),
  createModifierGroup: vi.fn(),
  updateModifierGroup: vi.fn(),
  addProductToGroup: vi.fn(),
  removeProductFromGroup: vi.fn(),
}));

vi.mock('@/api/admin/products', () => ({
  getAdminProductsList: vi.fn(() => Promise.resolve({ items: [], total: 0 })),
}));

vi.mock('@/features/categories/hooks/useCategories', () => ({
  useCategories: () => ({
    useList: () => ({ data: [] }),
  }),
}));

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <ModifierGroupsPage />
    </QueryClientProvider>
  );
}

describe('Modifier groups page (add-on only, no legacy migration)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders add-on groups title and create group button', async () => {
    renderPage();
    expect(screen.getByText(/Add-on-Gruppen/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Gruppe anlegen/ })).toBeInTheDocument();
  });

  it('does not render legacy migration UI', async () => {
    renderPage();
    const body = document.body.textContent ?? '';
    // Absence of legacy migration strings (regression: must not reappear after removal)
    expect(body).not.toMatch(/Bulk-Migration|Als Produkt migrieren|Legacy-Modifier|migration-progress|Aktive Legacy-Modifier|Migrieren\s*ausführen/i);
  });

  it('shows add-on products copy (active add-on model only)', async () => {
    renderPage();
    // Intro paragraph always visible; "+ Produkt" in button or copy
    expect(screen.getByText(/Add-on-Produkte/)).toBeInTheDocument();
    expect(screen.getByText(/\+ Produkt/)).toBeInTheDocument();
  });
});
