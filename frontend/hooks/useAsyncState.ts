import { useState, useCallback, useRef } from 'react';
import { Alert } from 'react-native';

export interface AsyncState<T = any> {
  data: T | null;
  loading: boolean;
  error: string | null;
  success: boolean;
  lastUpdated: Date | null;
}

export interface AsyncActions<T = any> {
  execute: (...args: any[]) => Promise<T | null>;
  reset: () => void;
  setData: (data: T) => void;
  setError: (error: string) => void;
  setLoading: (loading: boolean) => void;
}

export function useAsyncState<T = any>(
  asyncFunction: (...args: any[]) => Promise<T>,
  options: {
    autoExecute?: boolean;
    showErrorAlert?: boolean;
    showSuccessAlert?: boolean;
    successMessage?: string;
    errorMessage?: string;
    onSuccess?: (data: T) => void;
    onError?: (error: string) => void;
  } = {}
): [AsyncState<T>, AsyncActions<T>] {
  const {
    autoExecute = false,
    showErrorAlert = false,
    showSuccessAlert = false,
    successMessage,
    errorMessage,
    onSuccess,
    onError
  } = options;

  const [state, setState] = useState<AsyncState<T>>({
    data: null,
    loading: false,
    error: null,
    success: false,
    lastUpdated: null
  });

  const abortControllerRef = useRef<AbortController | null>(null);

  const reset = useCallback(() => {
    setState({
      data: null,
      loading: false,
      error: null,
      success: false,
      lastUpdated: null
    });
  }, []);

  const setData = useCallback((data: T) => {
    setState(prev => ({
      ...prev,
      data,
      success: true,
      lastUpdated: new Date()
    }));
  }, []);

  const setError = useCallback((error: string) => {
    setState(prev => ({
      ...prev,
      error,
      success: false,
      loading: false
    }));
  }, []);

  const setLoading = useCallback((loading: boolean) => {
    setState(prev => ({
      ...prev,
      loading
    }));
  }, []);

  const execute = useCallback(async (...args: any[]): Promise<T | null> => {
    console.log('🔄 useAsyncState execute çağrıldı:', {
      functionName: asyncFunction.name || 'Anonymous',
      args: args,
      autoExecute: autoExecute
    });
    
    // Önceki isteği iptal et
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
    }

    // Yeni abort controller oluştur
    abortControllerRef.current = new AbortController();

    try {
      console.log('🔄 Loading state set ediliyor...');
      setState(prev => ({
        ...prev,
        loading: true,
        error: null,
        success: false
      }));

      console.log('🔄 Async function çağrılıyor...');
      const result = await asyncFunction(...args);
      console.log('🔄 Async function sonucu:', result);

      // İstek iptal edildiyse sonucu işleme
      if (abortControllerRef.current.signal.aborted) {
        return null;
      }

      setState(prev => ({
        ...prev,
        data: result,
        loading: false,
        success: true,
        lastUpdated: new Date()
      }));

      if (showSuccessAlert && successMessage) {
        Alert.alert('Success', successMessage);
      }

      onSuccess?.(result);
      return result;

    } catch (error: any) {
      console.log('❌ useAsyncState error:', {
        error: error,
        errorMessage: error?.message,
        errorStatus: error?.status,
        errorData: error?.data
      });
      
      // İstek iptal edildiyse hata gösterme
      if (abortControllerRef.current.signal.aborted) {
        return null;
      }

      const finalErrorMessage = errorMessage || error?.message || 'An error occurred';
      console.log('❌ Final error message:', finalErrorMessage);
      
      setState(prev => ({
        ...prev,
        error: finalErrorMessage,
        loading: false,
        success: false
      }));

      if (showErrorAlert) {
        Alert.alert('Error', finalErrorMessage);
      }

      onError?.(finalErrorMessage);
      return null;

    } finally {
      abortControllerRef.current = null;
    }
  }, [asyncFunction, showErrorAlert, showSuccessAlert, successMessage, errorMessage, onSuccess, onError]);

  return [state, { execute, reset, setData, setError, setLoading }];
} 