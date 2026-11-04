import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { render, screen } from '@testing-library/react-native';
import React from 'react';
import { Text } from 'react-native';

import { catalogGridItemWidth, ProductList } from '../components/ProductList';
import { changeLanguage } from '../i18n';

jest.mock('../components/ProductGridCard', () => ({
  ProductGridCard: () => null,
}));

jest.mock('../components/ProductRow', () => ({
  ProductRow: () => null,
}));

jest.mock('../src/components/common/WaveLoader', () => ({
  WaveLoader: () => null,
}));

const mockCatalog = {
  products: [] as { id: string; name: string }[],
  categories: [] as { id: string; name: string }[],
  loading: false,
  error: null as string | null,
  initialized: false,
  refreshData: jest.fn(async () => undefined),
  searchProducts: jest.fn(() => []),
  getProductsByCategory: jest.fn(() => []),
  loadProducts: jest.fn(),
  loadCategories: jest.fn(),
};

jest.mock('../hooks/useProductsUnified', () => ({
  useProductsUnified: () => mockCatalog,
}));

describe('catalogGridItemWidth', () => {
  it('splits the catalog pane into equal columns', () => {
    expect(catalogGridItemWidth(900, 3, 16)).toBe(289);
    expect(catalogGridItemWidth(0, 3, 16)).toBe(0);
    expect(catalogGridItemWidth(300, 2, 16)).toBe(134);
  });
});

describe('ProductList catalog error', () => {
  beforeEach(async () => {
    await changeLanguage('de');
    mockCatalog.error = 'Failed to load catalog';
    mockCatalog.products = [];
    mockCatalog.loading = false;
  });

  it('keeps the category header visible when the catalog request fails', async () => {
    await render(
      <ProductList
        ListHeaderComponent={<Text>Kategorie</Text>}
      />
    );

    expect(screen.getByText('Kategorie')).toBeTruthy();
    expect(screen.getByTestId('pos-catalog-error')).toBeTruthy();
    expect(screen.getByText('Failed to load catalog')).toBeTruthy();
    expect(screen.getByText('Produkte')).toBeTruthy();
  });
});
