// API optimizasyon testleri
// Bu testler, sürekli API çağrılarının önlendiğini ve akıllı fetching'in çalıştığını doğrular

import { renderHook, act } from '@testing-library/react-hooks';
import { useOptimizedDataFetching } from '../hooks/useOptimizedDataFetching';

// Mock fetch
const mockFetch = jest.fn();
global.fetch = mockFetch;

// Mock useAuth
jest.mock('../contexts/AuthContext', () => ({
  useAuth: () => ({
    user: { id: 'test-user', token: 'test-token' },
    isAuthenticated: true,
  }),
}));

// Mock useNetInfo
jest.mock('@react-native-community/netinfo', () => ({
  useNetInfo: () => ({
    isConnected: true,
    isInternetReachable: true,
  }),
}));

describe('API Optimization Tests', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  describe('useOptimizedDataFetching', () => {
    it('should fetch data only once on mount', async () => {
      const mockData = { id: 1, name: 'Test Data' };
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      const { result } = renderHook(() =>
        useOptimizedDataFetching(
          () => fetch('/api/test'),
          [],
          { cacheTime: 5000, staleTime: 2000 }
        )
      );

      // İlk fetch yapılmalı
      expect(result.current.loading).toBe(true);
      
      await act(async () => {
        await result.current.fetchData();
      });

      expect(result.current.data).toEqual(mockData);
      expect(result.current.isInitialized).toBe(true);
      expect(mockFetch).toHaveBeenCalledTimes(1);
    });

    it('should not fetch again if data is fresh', async () => {
      const mockData = { id: 1, name: 'Test Data' };
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      const { result } = renderHook(() =>
        useOptimizedDataFetching(
          () => fetch('/api/test'),
          [],
          { cacheTime: 5000, staleTime: 2000 }
        )
      );

      // İlk fetch
      await act(async () => {
        await result.current.fetchData();
      });

      // İkinci fetch çağrısı - cache'den döndürülmeli
      await act(async () => {
        await result.current.fetchData();
      });

      // Sadece bir kere fetch yapılmış olmalı
      expect(mockFetch).toHaveBeenCalledTimes(1);
      expect(result.current.data).toEqual(mockData);
    });

    it('should fetch again after cache expires', async () => {
      const mockData1 = { id: 1, name: 'Test Data 1' };
      const mockData2 = { id: 2, name: 'Test Data 2' };
      
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => mockData1,
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => mockData2,
        });

      const { result } = renderHook(() =>
        useOptimizedDataFetching(
          () => fetch('/api/test'),
          [],
          { cacheTime: 1000, staleTime: 500 } // Kısa cache süresi
        )
      );

      // İlk fetch
      await act(async () => {
        await result.current.fetchData();
      });

      expect(result.current.data).toEqual(mockData1);

      // Cache süresini geç
      act(() => {
        jest.advanceTimersByTime(1500);
      });

      // İkinci fetch - cache expired olduğu için yapılmalı
      await act(async () => {
        await result.current.fetchData();
      });

      expect(mockFetch).toHaveBeenCalledTimes(2);
      expect(result.current.data).toEqual(mockData2);
    });

    it('should handle manual refresh', async () => {
      const mockData1 = { id: 1, name: 'Test Data 1' };
      const mockData2 = { id: 2, name: 'Test Data 2' };
      
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => mockData1,
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => mockData2,
        });

      const { result } = renderHook(() =>
        useOptimizedDataFetching(
          () => fetch('/api/test'),
          [],
          { cacheTime: 5000, staleTime: 2000 }
        )
      );

      // İlk fetch
      await act(async () => {
        await result.current.fetchData();
      });

      expect(result.current.data).toEqual(mockData1);

      // Manuel refresh
      await act(async () => {
        await result.current.refresh();
      });

      expect(mockFetch).toHaveBeenCalledTimes(2);
      expect(result.current.data).toEqual(mockData2);
    });

    it('should not fetch when disabled', async () => {
      const { result } = renderHook(() =>
        useOptimizedDataFetching(
          () => fetch('/api/test'),
          [],
          { enabled: false }
        )
      );

      await act(async () => {
        await result.current.fetchData();
      });

      expect(mockFetch).not.toHaveBeenCalled();
      expect(result.current.data).toBeNull();
    });

    it('should handle network errors gracefully', async () => {
      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      const { result } = renderHook(() =>
        useOptimizedDataFetching(
          () => fetch('/api/test'),
          [],
          { cacheTime: 5000, staleTime: 2000 }
        )
      );

      await act(async () => {
        await result.current.fetchData();
      });

      expect(result.current.error).toBe('Network error');
      expect(result.current.data).toBeNull();
      expect(result.current.loading).toBe(false);
    });
  });

  describe('Cache TTL Logic', () => {
    it('should calculate stale status correctly', async () => {
      const mockData = { id: 1, name: 'Test Data' };
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      const { result } = renderHook(() =>
        useOptimizedDataFetching(
          () => fetch('/api/test'),
          [],
          { cacheTime: 5000, staleTime: 2000 }
        )
      );

      // İlk fetch
      await act(async () => {
        await result.current.fetchData();
      });

      expect(result.current.isStale).toBe(false);

      // Stale time'ı geç
      act(() => {
        jest.advanceTimersByTime(2500);
      });

      expect(result.current.isStale).toBe(true);

      // Cache time'ı geç
      act(() => {
        jest.advanceTimersByTime(3000);
      });

      expect(result.current.shouldFetch).toBe(true);
    });
  });

  describe('Performance Metrics', () => {
    it('should measure fetch performance', async () => {
      const mockData = { id: 1, name: 'Test Data' };
      mockFetch.mockResolvedValueOnce({
        ok: true,
        json: async () => mockData,
      });

      const consoleSpy = jest.spyOn(console, 'log').mockImplementation();

      const { result } = renderHook(() =>
        useOptimizedDataFetching(
          () => fetch('/api/test'),
          [],
          { cacheTime: 5000, staleTime: 2000 }
        )
      );

      const startTime = Date.now();
      
      await act(async () => {
        await result.current.fetchData();
      });

      const endTime = Date.now();
      const duration = endTime - startTime;

      expect(duration).toBeGreaterThan(0);
      expect(consoleSpy).toHaveBeenCalledWith('🔄 Fetching fresh data...');

      consoleSpy.mockRestore();
    });
  });
});

describe('Hook Integration Tests', () => {
  it('should work with multiple hooks simultaneously', async () => {
    const mockData1 = { id: 1, name: 'Data 1' };
    const mockData2 = { id: 2, name: 'Data 2' };
    
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockData1,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockData2,
      });

    const { result: hook1 } = renderHook(() =>
      useOptimizedDataFetching(
        () => fetch('/api/test1'),
        [],
        { cacheTime: 3000, staleTime: 1000 }
      )
    );

    const { result: hook2 } = renderHook(() =>
      useOptimizedDataFetching(
        () => fetch('/api/test2'),
        [],
        { cacheTime: 5000, staleTime: 2000 }
      )
    );

    // Her iki hook'u da fetch et
    await act(async () => {
      await Promise.all([
        hook1.current.fetchData(),
        hook2.current.fetchData(),
      ]);
    });

    expect(hook1.current.data).toEqual(mockData1);
    expect(hook2.current.data).toEqual(mockData2);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });
});
