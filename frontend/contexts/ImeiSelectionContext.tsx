import React, { createContext, useCallback, useContext, useMemo, useState } from 'react';

type ImeiSelectionContextValue = {
  reserve: (productId: string, imei: string) => void;
  releaseLast: (productId: string) => void;
  peek: (productId: string) => string[];
  take: (productId: string, count: number) => string[] | null;
  reset: () => void;
};

const ImeiSelectionContext = createContext<ImeiSelectionContextValue | null>(null);

export function ImeiSelectionProvider({ children }: { children: React.ReactNode }) {
  const [byProductId, setByProductId] = useState<Record<string, string[]>>({});

  const reserve = useCallback((productId: string, imei: string) => {
    const code = imei.trim();
    if (!productId || !code) return;
    setByProductId((prev) => ({
      ...prev,
      [productId]: [...(prev[productId] ?? []), code],
    }));
  }, []);

  const releaseLast = useCallback((productId: string) => {
    setByProductId((prev) => {
      const current = prev[productId];
      if (!current || current.length === 0) return prev;
      const next = current.slice(0, -1);
      if (next.length === 0) {
        const { [productId]: _, ...rest } = prev;
        return rest;
      }
      return { ...prev, [productId]: next };
    });
  }, []);

  const peek = useCallback(
    (productId: string) => byProductId[productId] ?? [],
    [byProductId]
  );

  const take = useCallback((productId: string, count: number): string[] | null => {
    const current = byProductId[productId] ?? [];
    if (count <= 0) return [];
    if (current.length < count) return null;
    const selected = current.slice(0, count);
    setByProductId((prev) => {
      const remaining = (prev[productId] ?? []).slice(count);
      if (remaining.length === 0) {
        const { [productId]: _, ...rest } = prev;
        return rest;
      }
      return { ...prev, [productId]: remaining };
    });
    return selected;
  }, [byProductId]);

  const reset = useCallback(() => setByProductId({}), []);

  const value = useMemo(
    () => ({ reserve, releaseLast, peek, take, reset }),
    [reserve, releaseLast, peek, take, reset]
  );

  return <ImeiSelectionContext.Provider value={value}>{children}</ImeiSelectionContext.Provider>;
}

export function useImeiSelections(): ImeiSelectionContextValue {
  const value = useContext(ImeiSelectionContext);
  if (!value) {
    throw new Error('useImeiSelections must be used within ImeiSelectionProvider');
  }
  return value;
}

export function useOptionalImeiSelections(): ImeiSelectionContextValue | null {
  return useContext(ImeiSelectionContext);
}
