import { useState, useCallback } from 'react';

// ❌ DEPRECATED: Bu hook artık kullanılmamalı
// ✅ YENİ: useApiManager hook'unu kullanın
// import { useApiManager } from '../hooks/useApiManager';

export interface FetchOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  headers?: Record<string, string>;
  body?: any;
}

// ❌ DEPRECATED: Direct fetch() kullanımı - apiClient kullanın
export function useFetch<T = any>(url: string, options?: FetchOptions) {
  const [data, setData] = useState<T | null>(null);
  const [error, setError] = useState<any>(null);
  const [loading, setLoading] = useState(false);

  const fetchData = useCallback(
    async (overrideOptions?: FetchOptions) => {
      setLoading(true);
      setError(null);
      try {
        const mergedOptions = { ...options, ...overrideOptions };
        const fetchOptions: RequestInit = {
          method: mergedOptions.method || 'GET',
          headers: {
            'Content-Type': 'application/json',
            ...(mergedOptions.headers || {}),
          },
        };
        if (mergedOptions.body) {
          fetchOptions.body = JSON.stringify(mergedOptions.body);
        }
        const response = await fetch(url, fetchOptions);
        if (!response.ok) {
          const errorText = await response.text();
          throw new Error(errorText || response.statusText);
        }
        const responseData = await response.json();
        setData(responseData);
        return responseData;
      } catch (err) {
        setError(err);
        throw err;
      } finally {
        setLoading(false);
      }
    },
    [url, options]
  );

  return { data, error, loading, fetchData };
} 