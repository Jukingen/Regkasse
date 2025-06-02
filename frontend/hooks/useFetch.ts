import { useState, useEffect, useCallback } from 'react';
import { APIError, handleAPIError } from '../services/errorService';

interface FetchOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
  headers?: Record<string, string>;
  body?: any;
  skip?: boolean;
}

interface FetchState<T> {
  data: T | null;
  error: APIError | null;
  loading: boolean;
}

export function useFetch<T>(url: string, options: FetchOptions = {}) {
  const [state, setState] = useState<FetchState<T>>({
    data: null,
    error: null,
    loading: true,
  });

  const fetchData = useCallback(async () => {
    if (options.skip) {
      setState(prev => ({ ...prev, loading: false }));
      return;
    }

    setState(prev => ({ ...prev, loading: true, error: null }));

    try {
      const response = await fetch(url, {
        method: options.method || 'GET',
        headers: {
          'Content-Type': 'application/json',
          ...options.headers,
        },
        body: options.body ? JSON.stringify(options.body) : undefined,
      });

      if (!response.ok) {
        throw handleAPIError({ response });
      }

      const data = await response.json();
      setState({ data, error: null, loading: false });
    } catch (error) {
      const apiError = handleAPIError(error);
      setState({ data: null, error: apiError, loading: false });
    }
  }, [url, options.method, options.headers, options.body, options.skip]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const refetch = useCallback(() => {
    fetchData();
  }, [fetchData]);

  return {
    ...state,
    refetch,
  };
} 