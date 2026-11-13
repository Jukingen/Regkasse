import { beforeEach, describe, expect, it, jest } from '@jest/globals';
import { act, renderHook, waitFor } from '@testing-library/react-native';

import { useAuth } from '../contexts/AuthContext';
import {
  resetProductsUnifiedCacheForTests,
  useProductsUnified,
} from '../hooks/useProductsUnified';
import { getAllCategories, getAllProducts, getProductCatalog } from '../services/api/productService';
import { sessionManager } from '../services/session/sessionManager';
import { resolveEffectiveTenantSlug } from '../services/tenant/devTenant';
import { tenantStorage } from '../services/tenant/tenantStorage';

jest.mock('../contexts/AuthContext', () => ({
  useAuth: jest.fn(),
}));

jest.mock('../services/api/productService', () => ({
  getProductCatalog: jest.fn(),
  getAllProducts: jest.fn(),
  getAllCategories: jest.fn(),
  clearProductCache: jest.fn(),
  CatalogAuthRequiredError: class CatalogAuthRequiredError extends Error {
    constructor() {
      super('CATALOG_AUTH_REQUIRED');
      this.name = 'CatalogAuthRequiredError';
    }
  },
}));

jest.mock('../services/session/sessionManager', () => ({
  sessionManager: {
    getAccessToken: jest.fn(),
  },
}));

jest.mock('../services/tenant/tenantStorage', () => ({
  tenantStorage: {
    getTenantSlug: jest.fn(async () => 'dev'),
  },
}));

let mockSlug = 'dev';
let mockSlugListener: (() => void) | null = null;

jest.mock('../services/tenant/devTenant', () => ({
  resolveEffectiveTenantSlug: jest.fn(async () => mockSlug),
  subscribeDevTenantSlug: (listener: () => void) => {
    mockSlugListener = listener;
    return () => {
      if (mockSlugListener === listener) mockSlugListener = null;
    };
  },
}));

const mockUseAuth = useAuth as jest.MockedFunction<typeof useAuth>;

type AsyncMock = {
  mockReset: () => void;
  mockResolvedValue: (value: unknown) => void;
  mockRejectedValue: (reason: Error) => void;
};

function asAsyncMock(fn: unknown): AsyncMock {
  return fn as AsyncMock;
}
const mockCatalog = getProductCatalog as jest.MockedFunction<typeof getProductCatalog>;
const mockToken = sessionManager.getAccessToken as jest.MockedFunction<
  typeof sessionManager.getAccessToken
>;

const suppe = {
  categories: [{ id: 'c1', name: 'Gastronomie' }],
  products: [{ id: 'p1', name: 'Suppe' }],
};

const mineralwasser = {
  categories: [{ id: 'c2', name: 'Gastronomie Tische' }],
  products: [{ id: 'p2', name: 'Mineralwasser' }],
};

describe('useProductsUnified catalog scope', () => {
  let user: { id: string; tenantId: string; tenantSlug: string } | null;

  beforeEach(() => {
    resetProductsUnifiedCacheForTests();
    mockSlug = 'dev';
    mockSlugListener = null;
    user = { id: 'cashier1', tenantId: 'tenant-dev', tenantSlug: 'dev' };
    mockUseAuth.mockImplementation(() => ({ user }) as ReturnType<typeof useAuth>);
    mockToken.mockReset();
    mockToken.mockResolvedValue('token');
    mockCatalog.mockReset();
    mockCatalog.mockResolvedValue(suppe as Awaited<ReturnType<typeof getProductCatalog>>);
    asAsyncMock(getAllProducts).mockReset();
    asAsyncMock(getAllCategories).mockReset();
    asAsyncMock(tenantStorage.getTenantSlug).mockResolvedValue('dev');
    (resolveEffectiveTenantSlug as jest.MockedFunction<typeof resolveEffectiveTenantSlug>).mockImplementation(
      async () => mockSlug
    );
  });

  it('does not request the catalog when the access token is missing', async () => {
    mockToken.mockResolvedValue(null);

    const { result } = await renderHook(() => useProductsUnified());

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });
    expect(mockCatalog).not.toHaveBeenCalled();
    expect(getAllProducts).not.toHaveBeenCalled();
    expect(result.current.products).toEqual([]);
    expect(result.current.initialized).toBe(false);
  });

  it('loads products for the signed-in cashier and retries an empty first payload', async () => {
    mockCatalog
      .mockResolvedValueOnce({ categories: [], products: [] })
      .mockResolvedValueOnce(suppe as Awaited<ReturnType<typeof getProductCatalog>>);

    const { result } = await renderHook(() => useProductsUnified());

    await waitFor(() => {
      expect(result.current.products.map((row) => row.name)).toEqual(['Suppe']);
    });
    expect(mockCatalog).toHaveBeenCalledTimes(2);
    expect(result.current.categories.map((row) => row.name)).toEqual(['Gastronomie']);
  });

  it('refetches when the development tenant slug changes', async () => {
    mockCatalog.mockImplementation(async () =>
      (mockSlug === 'dev' ? suppe : mineralwasser) as Awaited<ReturnType<typeof getProductCatalog>>
    );

    const { result } = await renderHook(() => useProductsUnified());

    await waitFor(() => {
      expect(result.current.products.map((row) => row.name)).toEqual(['Suppe']);
    });

    mockSlug = 'dev-gastronomy-tables';
    await act(async () => {
      mockSlugListener?.();
    });

    await waitFor(() => {
      expect(result.current.products.map((row) => row.name)).toEqual(['Mineralwasser']);
    });
    expect(result.current.categories.map((row) => row.name)).toEqual(['Gastronomie Tische']);
  });

  it('clears the catalog when the cashier logs out', async () => {
    const { result, rerender } = await renderHook(() => useProductsUnified());

    await waitFor(() => {
      expect(result.current.products).toHaveLength(1);
    });

    user = null;
    rerender({});

    await waitFor(() => {
      expect(result.current.products).toEqual([]);
    });
    expect(result.current.categories).toEqual([]);
  });

  it('keeps a failed load retryable', async () => {
    mockCatalog.mockRejectedValue(new Error('catalog down'));
    asAsyncMock(getAllProducts).mockRejectedValue(new Error('products down'));
    asAsyncMock(getAllCategories).mockRejectedValue(new Error('categories down'));

    const { result } = await renderHook(() => useProductsUnified());

    await waitFor(() => {
      expect(result.current.error).toBeTruthy();
    });
    expect(result.current.initialized).toBe(false);

    mockCatalog.mockResolvedValue(suppe as Awaited<ReturnType<typeof getProductCatalog>>);
    await act(async () => {
      await result.current.refreshData();
    });

    await waitFor(() => {
      expect(result.current.products.map((row) => row.name)).toEqual(['Suppe']);
    });
    expect(result.current.error).toBeNull();
  });
});
