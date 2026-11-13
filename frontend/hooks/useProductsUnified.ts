// Türkçe Açıklama: Unified Product Hook - Tüm ürün API çağrılarını tek noktada birleştirir
// Duplicate hook'ları kaldırır ve consistent API kullanımı sağlar

import { useState, useEffect, useCallback, useRef } from 'react';

import { useAuth } from '../contexts/AuthContext';
import {
  Product,
  ProductCategory,
  getAllProducts,
  getAllCategories,
  clearProductCache,
  getProductCatalog,
  CatalogAuthRequiredError,
} from '../services/api/productService';
import { sessionManager } from '../services/session/sessionManager';
import { subscribeDevTenantSlug, resolveEffectiveTenantSlug } from '../services/tenant/devTenant';
import { tenantStorage } from '../services/tenant/tenantStorage';
import { safeLog } from '../utils/loggingUtils';
import { productMatchesSearchQuery } from '../utils/productLocalization';

export type CatalogCategory = ProductCategory;

interface UseProductsUnifiedState {
  products: Product[];
  categories: CatalogCategory[];
  loading: boolean;
  error: string | null;
  initialized: boolean;
}

const emptyState = (): UseProductsUnifiedState => ({
  products: [],
  categories: [],
  loading: false,
  error: null,
  initialized: false,
});

function scopeKeyFor(userId: string, tenantSlug: string): string {
  return `${userId}\u0000${tenantSlug}`;
}

function isSettledCatalog(state: UseProductsUnifiedState): boolean {
  return state.initialized && (state.products.length > 0 || state.categories.length > 0);
}

// Singleton cache. Entries are valid only for the user + tenant slug that loaded them.
class ProductCache {
  private static instance: ProductCache;
  private state: UseProductsUnifiedState = emptyState();
  private readonly listeners = new Set<() => void>();
  private loadingPromise: Promise<void> | null = null;
  private scopeKey: string | null = null;
  /** Scope whose last response contained categories or products. Empty/failed loads stay unset. */
  private settledScopeKey: string | null = null;

  static getInstance(): ProductCache {
    if (!ProductCache.instance) {
      ProductCache.instance = new ProductCache();
    }
    return ProductCache.instance;
  }

  /** Test-only. Drops the in-memory catalog so cases do not leak into each other. */
  resetForTests(): void {
    this.loadingPromise = null;
    this.scopeKey = null;
    this.settledScopeKey = null;
    this.state = emptyState();
    this.listeners.forEach((listener) => {
      listener();
    });
  }

  private updateState(newState: Partial<UseProductsUnifiedState>) {
    this.state = { ...this.state, ...newState };
    this.listeners.forEach((listener) => {
      listener();
    });
  }

  addListener(listener: () => void) {
    this.listeners.add(listener);
  }

  removeListener(listener: () => void) {
    this.listeners.delete(listener);
  }

  getState(): UseProductsUnifiedState {
    return { ...this.state };
  }

  /** Logout or a signed-out screen: drop the previous cashier's catalog. */
  clearSession(): void {
    clearProductCache();
    this.loadingPromise = null;
    this.scopeKey = null;
    this.settledScopeKey = null;
    this.state = emptyState();
    this.updateState(this.state);
  }

  /**
   * Load the catalog for this cashier and tenant. A different scope drops the previous
   * payload first. A settled non-empty catalog for the same scope is reused.
   */
  async ensureScope(userId: string, tenantSlug: string): Promise<void> {
    const key = scopeKeyFor(userId, tenantSlug);
    if (this.scopeKey !== key) {
      clearProductCache();
      this.loadingPromise = null;
      this.scopeKey = key;
      this.settledScopeKey = null;
      this.state = emptyState();
      this.updateState(this.state);
    }
    await this.loadData();
  }

  async loadData(): Promise<void> {
    if (this.loadingPromise) {
      await this.loadingPromise;
    }

    if (this.settledScopeKey === this.scopeKey && isSettledCatalog(this.state)) {
      safeLog('📦 Products already loaded, skipping...');
      return;
    }

    const scopeAtStart = this.scopeKey;
    const promise = this._performLoad(scopeAtStart);
    this.loadingPromise = promise;

    try {
      await promise;
    } finally {
      if (this.loadingPromise === promise) {
        this.loadingPromise = null;
      }
    }
  }

  private async _performLoad(scopeAtStart: string | null): Promise<void> {
    if (this.scopeKey !== scopeAtStart) return;

    const token = await sessionManager.getAccessToken();
    if (this.scopeKey !== scopeAtStart) return;
    if (!token) {
      safeLog('⏭️ Skipping product catalog: no access token');
      this.settledScopeKey = null;
      this.updateState({ loading: false, error: null, initialized: false });
      return;
    }

    try {
      this.updateState({ loading: true, error: null });

      safeLog('🔄 Loading products and categories via catalog...');

      try {
        const catalog = await getProductCatalog();
        if (this.scopeKey !== scopeAtStart) return;

        const products = Array.isArray(catalog.products) ? catalog.products : [];
        const categories: CatalogCategory[] = (catalog.categories ?? []).map((c) => ({
          id: c.id ?? '',
          name: c.name ?? '',
          icon: c.icon,
          color: c.color,
          sortOrder: c.sortOrder,
          vatRate: c.vatRate,
        }));

        this.applyCatalog(scopeAtStart, products, categories);
      } catch (catalogError) {
        if (catalogError instanceof CatalogAuthRequiredError) {
          if (this.scopeKey !== scopeAtStart) return;
          this.settledScopeKey = null;
          this.updateState({ loading: false, error: null, initialized: false });
          return;
        }

        console.error(
          '❌ Catalog loading failed, falling back to separate endpoints:',
          catalogError
        );

        const [productsResponse, categoriesResponse] = await Promise.all([
          getAllProducts(1, 1000),
          getAllCategories(),
        ]);
        if (this.scopeKey !== scopeAtStart) return;

        const products = Array.isArray(productsResponse) ? productsResponse : [];
        const rawCategories = Array.isArray(categoriesResponse) ? categoriesResponse : [];
        const categories: CatalogCategory[] = rawCategories.map((c, i) =>
          typeof c === 'string'
            ? { id: `fallback-${i}-${c}`, name: c }
            : {
                id: c.id || `fallback-${i}-${c.name}`,
                name: c.name ?? '',
                icon: c.icon,
                color: c.color,
                sortOrder: c.sortOrder,
              }
        );

        this.applyCatalog(scopeAtStart, products, categories);
      }
    } catch (error) {
      if (this.scopeKey !== scopeAtStart) return;
      const errorMessage = error instanceof Error ? error.message : 'Failed to load data';
      this.settledScopeKey = null;
      this.updateState({
        loading: false,
        error: errorMessage,
        initialized: false,
      });
      console.error('❌ Error loading products/categories:', error);
      throw error;
    }
  }

  /** Empty and failed catalogs stay unsettled so the next load for this scope can retry. */
  private applyCatalog(
    scopeAtStart: string | null,
    products: Product[],
    categories: CatalogCategory[]
  ) {
    if (this.scopeKey !== scopeAtStart) return;
    const settled = products.length > 0 || categories.length > 0;
    this.settledScopeKey = settled ? scopeAtStart : null;
    this.updateState({
      products,
      categories,
      loading: false,
      initialized: settled,
      error: null,
    });
    safeLog(
      `✅ Loaded ${products.length} products and ${categories.length} categories from catalog`
    );
  }

  async refreshData(): Promise<void> {
    safeLog('🔄 Refreshing all product data...');

    clearProductCache();
    this.settledScopeKey = null;
    this.loadingPromise = null;
    this.state = emptyState();
    this.updateState(this.state);

    await this.loadData();
  }

  /** Kategoriye göre ürünleri filtrele. null = "Alle" (tümü). */
  getProductsByCategory(categoryId: string | null): Product[] {
    if (categoryId == null || categoryId === '') {
      return this.state.products;
    }
    return this.state.products.filter((p) => {
      const id = (p as Product & { categoryId?: string }).categoryId;
      return id != null && id === categoryId;
    });
  }

  searchProducts(query: string): Product[] {
    if (!query.trim()) {
      return this.state.products;
    }

    const searchTerm = query.toLowerCase();
    return this.state.products.filter((product) => {
      const productCategory = product.productCategory || product.category;
      return (
        productMatchesSearchQuery(product, searchTerm) ||
        productCategory?.toLowerCase().includes(searchTerm)
      );
    });
  }
}

export function resetProductsUnifiedCacheForTests(): void {
  ProductCache.getInstance().resetForTests();
}

/**
 * Unified Product Hook - Tüm ürün işlemlerini tek noktada yönetir
 * Duplicate API çağrılarını önler ve consistent state yönetimi sağlar
 */
export const useProductsUnified = () => {
  const cache = ProductCache.getInstance();
  const { user } = useAuth();
  const [, forceUpdate] = useState({});
  const isMountedRef = useRef(true);
  const [slugEpoch, setSlugEpoch] = useState(0);
  const userId = user?.id ?? null;
  const userTenantId = user?.tenantId ?? null;
  const userTenantSlug = user?.tenantSlug ?? null;

  useEffect(() => {
    return subscribeDevTenantSlug(() => {
      setSlugEpoch((epoch) => epoch + 1);
    });
  }, []);

  useEffect(() => {
    const listener = () => {
      if (isMountedRef.current) {
        forceUpdate({});
      }
    };

    isMountedRef.current = true;
    cache.addListener(listener);
    return () => {
      isMountedRef.current = false;
      cache.removeListener(listener);
    };
  }, [cache]);

  useEffect(() => {
    let cancelled = false;

    if (!userId) {
      cache.clearSession();
      return;
    }

    (async () => {
      const slug =
        (await resolveEffectiveTenantSlug(await tenantStorage.getTenantSlug())) ??
        userTenantSlug ??
        '';
      if (cancelled) return;

      const loadOnce = async () => {
        try {
          await cache.ensureScope(userId, slug);
        } catch (error: unknown) {
          console.error(error);
        }
      };

      await loadOnce();
      if (cancelled) return;
      // A missing token, a failed call, or an empty first payload must not stick.
      if (!isSettledCatalog(cache.getState())) {
        await loadOnce();
      }
    })().catch((error: unknown) => {
      console.error(error);
    });

    return () => {
      cancelled = true;
    };
  }, [cache, userId, userTenantId, userTenantSlug, slugEpoch]);

  const state = cache.getState();

  const refreshData = useCallback(() => cache.refreshData(), [cache]);
  const getProductsByCategory = useCallback(
    (categoryId: string | null) => cache.getProductsByCategory(categoryId),
    [cache]
  );
  const searchProducts = useCallback((query: string) => cache.searchProducts(query), [cache]);

  return {
    ...state,
    refreshData,
    getProductsByCategory,
    searchProducts,

    loadProducts: refreshData,
    loadCategories: refreshData,
  };
};

export default useProductsUnified;
