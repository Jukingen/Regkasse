// API optimizasyon testleri
// Bu testler, sürekli API çağrılarının önlendiğini ve akıllı fetching'in çalıştığını doğrular

import { renderHook, act, waitFor } from '@testing-library/react-native';

import { useOptimizedDataFetching } from '../hooks/useOptimizedDataFetching';

jest.mock('../config', () => ({
  API_BASE_URL: 'http://localhost:5184/api',
}));

jest.mock('@react-native-async-storage/async-storage', () =>
  require('@react-native-async-storage/async-storage/jest/async-storage-mock')
);

jest.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 'test-user', token: 'test-token' },
    isAuthenticated: true,
  }),
}));

jest.mock('@react-native-community/netinfo', () => ({
  useNetInfo: () => ({
    isConnected: true,
    isInternetReachable: true,
  }),
}));

describe('API Optimization Tests', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('useOptimizedDataFetching', () => {
    it('should fetch data only once on mount', async () => {
      const mockData = { id: 1, name: 'Test Data' };
      const fetchFn = jest.fn().mockResolvedValue(mockData);

      const { result } = await renderHook(() =>
        useOptimizedDataFetching(fetchFn, [], {
          cacheTime: 5000,
          staleTime: 2000,
        })
      );

      await waitFor(() => {
        expect(result.current.isInitialized).toBe(true);
      });

      expect(result.current.data).toEqual(mockData);
      expect(result.current.loading).toBe(false);
      expect(fetchFn).toHaveBeenCalledTimes(1);
    });

    it('should not fetch again if data is fresh', async () => {
      const mockData = { id: 1, name: 'Test Data' };
      const fetchFn = jest.fn().mockResolvedValue(mockData);

      const { result } = await renderHook(() =>
        useOptimizedDataFetching(fetchFn, [], {
          cacheTime: 5000,
          staleTime: 2000,
        })
      );

      await waitFor(() => {
        expect(result.current.isInitialized).toBe(true);
      });

      await act(async () => {
        await result.current.fetchData();
      });

      expect(fetchFn).toHaveBeenCalledTimes(1);
      expect(result.current.data).toEqual(mockData);
    });

    it('should fetch again after cache expires', async () => {
      const mockData1 = { id: 1, name: 'Test Data 1' };
      const mockData2 = { id: 2, name: 'Test Data 2' };
      const fetchFn = jest.fn().mockResolvedValueOnce(mockData1).mockResolvedValueOnce(mockData2);

      const now = 1_000_000;
      const dateNowSpy = jest.spyOn(Date, 'now').mockReturnValue(now);

      const { result } = await renderHook(() =>
        useOptimizedDataFetching(fetchFn, [], {
          cacheTime: 1000,
          staleTime: 500,
        })
      );

      await waitFor(() => {
        expect(result.current.data).toEqual(mockData1);
      });

      dateNowSpy.mockReturnValue(now + 1500);

      await act(async () => {
        await result.current.fetchData();
      });

      expect(fetchFn).toHaveBeenCalledTimes(2);
      expect(result.current.data).toEqual(mockData2);

      dateNowSpy.mockRestore();
    });

    it('should handle manual refresh', async () => {
      const mockData1 = { id: 1, name: 'Test Data 1' };
      const mockData2 = { id: 2, name: 'Test Data 2' };
      const fetchFn = jest.fn().mockResolvedValueOnce(mockData1).mockResolvedValueOnce(mockData2);

      const { result } = await renderHook(() =>
        useOptimizedDataFetching(fetchFn, [], {
          cacheTime: 5000,
          staleTime: 2000,
        })
      );

      await waitFor(() => {
        expect(result.current.data).toEqual(mockData1);
      });

      await act(async () => {
        await result.current.refresh();
      });

      expect(fetchFn).toHaveBeenCalledTimes(2);
      expect(result.current.data).toEqual(mockData2);
    });

    it('should not fetch when disabled', async () => {
      const fetchFn = jest.fn().mockResolvedValue({ id: 1 });

      const { result } = await renderHook(() =>
        useOptimizedDataFetching(fetchFn, [], { enabled: false })
      );

      await act(async () => {
        await result.current.fetchData();
      });

      expect(fetchFn).not.toHaveBeenCalled();
      expect(result.current.data).toBeNull();
    });

    it('should handle network errors gracefully', async () => {
      const fetchFn = jest.fn().mockRejectedValue(new Error('Network error'));

      const { result } = await renderHook(() =>
        useOptimizedDataFetching(fetchFn, [], {
          cacheTime: 5000,
          staleTime: 2000,
        })
      );

      await waitFor(() => {
        expect(result.current.error).toBe('Network error');
      });

      expect(result.current.data).toBeNull();
      expect(result.current.loading).toBe(false);
    });
  });

  describe('Cache TTL Logic', () => {
    it('should refetch when data becomes stale', async () => {
      const mockData1 = { id: 1, name: 'Test Data 1' };
      const mockData2 = { id: 2, name: 'Test Data 2' };
      const fetchFn = jest.fn().mockResolvedValueOnce(mockData1).mockResolvedValueOnce(mockData2);

      const now = 2_000_000;
      const dateNowSpy = jest.spyOn(Date, 'now').mockReturnValue(now);

      const { result } = await renderHook(() =>
        useOptimizedDataFetching(fetchFn, [], {
          cacheTime: 5000,
          staleTime: 2000,
        })
      );

      await waitFor(() => {
        expect(result.current.data).toEqual(mockData1);
      });
      expect(fetchFn).toHaveBeenCalledTimes(1);

      // Still within staleTime — cache hit
      dateNowSpy.mockReturnValue(now + 1000);
      await act(async () => {
        await result.current.fetchData();
      });
      expect(fetchFn).toHaveBeenCalledTimes(1);

      // Past staleTime — should refetch
      dateNowSpy.mockReturnValue(now + 2500);
      await act(async () => {
        await result.current.fetchData();
      });
      expect(fetchFn).toHaveBeenCalledTimes(2);
      expect(result.current.data).toEqual(mockData2);

      dateNowSpy.mockRestore();
    });
  });

  describe('Performance Metrics', () => {
    it('should measure fetch performance', async () => {
      const mockData = { id: 1, name: 'Test Data' };
      const fetchFn = jest.fn().mockResolvedValue(mockData);
      const consoleSpy = jest.spyOn(console, 'log').mockImplementation(() => {});

      const { result } = await renderHook(() =>
        useOptimizedDataFetching(fetchFn, [], {
          cacheTime: 5000,
          staleTime: 2000,
        })
      );

      await waitFor(() => {
        expect(result.current.isInitialized).toBe(true);
      });

      expect(result.current.data).toEqual(mockData);
      expect(consoleSpy).toHaveBeenCalledWith('🔄 Fetching fresh data...');

      consoleSpy.mockRestore();
    });
  });
});

describe('Hook Integration Tests', () => {
  it('should work with multiple hooks simultaneously', async () => {
    const mockData1 = { id: 1, name: 'Data 1' };
    const mockData2 = { id: 2, name: 'Data 2' };

    const fetchFn1 = jest.fn().mockResolvedValue(mockData1);
    const fetchFn2 = jest.fn().mockResolvedValue(mockData2);

    const { result: hook1 } = await renderHook(() =>
      useOptimizedDataFetching(fetchFn1, [], {
        cacheTime: 3000,
        staleTime: 1000,
      })
    );

    const { result: hook2 } = await renderHook(() =>
      useOptimizedDataFetching(fetchFn2, [], {
        cacheTime: 5000,
        staleTime: 2000,
      })
    );

    await waitFor(() => {
      expect(hook1.current.data).toEqual(mockData1);
      expect(hook2.current.data).toEqual(mockData2);
    });

    expect(fetchFn1).toHaveBeenCalledTimes(1);
    expect(fetchFn2).toHaveBeenCalledTimes(1);
  });
});
