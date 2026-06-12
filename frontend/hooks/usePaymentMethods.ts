import React, { useState, useEffect, useCallback } from 'react';
import {
  isRecord,
  normalizeToPosPaymentMethods,
  type NormalizedPosPaymentMethod,
} from '../services/api/normalizePosPaymentMethods';
import { posPaymentMethodsPath } from '../services/api/posPaymentPaths';
import { apiClient } from '../services/api/config';
import { sessionManager } from '../services/session/sessionManager';

// English Description: Hook for fetching and managing payment methods from backend. Also checks TSE status.
// OPTIMIZATION: Sürekli API çağrısı yerine sadece gerekli durumlarda fetch yapar

export interface PaymentMethodInfo {
  method: 'cash' | 'card' | 'voucher' | 'transfer';
  name: string;
  description: string;
  isEnabled: boolean;
  requiresTse: boolean;
  icon: string;
  minAmount: number;
  maxAmount: number;
}

export interface TseStatusInfo {
  isConnected: boolean;
  lastCheck: string;
  deviceInfo: string;
}

function hookMethodKey(m: NormalizedPosPaymentMethod): PaymentMethodInfo['method'] {
  if (m.type === 'card') return 'card';
  if (m.type === 'voucher') return 'voucher';
  if (m.type === 'transfer') return 'transfer';
  return 'cash';
}

function toPaymentMethodInfo(m: NormalizedPosPaymentMethod): PaymentMethodInfo {
  return {
    method: hookMethodKey(m),
    name: m.name,
    description: m.name,
    isEnabled: true,
    requiresTse: false,
    icon: m.icon,
    minAmount: 0.01,
    maxAmount: 100_000,
  };
}

export function usePaymentMethods(user: any, cashRegisterId?: string | null) {
  const [paymentMethods, setPaymentMethods] = useState<PaymentMethodInfo[]>([]);
  const [tseStatus, setTseStatus] = useState<TseStatusInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isInitialized, setIsInitialized] = useState(false); // Yeni: Initialization flag

  // Fetch payment methods from backend
  const fetchPaymentMethods = useCallback(async () => {
    // Token kontrolü
    const token = await sessionManager.getAccessToken();
    if (!user || !token) {
      console.error('❌ No user or token available');
      setError('User not authenticated. Please login first.');
      setPaymentMethods([]);
      return;
    }

    // OPTIMIZATION: Eğer zaten fetch edildiyse ve data varsa, tekrar fetch yapma
    if (!cashRegisterId?.trim()) {
      setError('Cash register is not selected.');
      setPaymentMethods([]);
      return;
    }

    if (isInitialized && paymentMethods.length > 0) {
      console.log('🔄 Payment methods already fetched, returning cached data');
      return;
    }

    try {
      setLoading(true);
      setError(null);
      console.log('🔄 Fetching payment methods from backend...');
      console.log('🔐 Using token:', token ? 'Available' : 'Missing');
      const json: unknown = await apiClient.get<unknown>(posPaymentMethodsPath(cashRegisterId.trim()));
      const methods = normalizeToPosPaymentMethods(json);
      const env = isRecord(json) ? json : null;

      if (methods.length === 0 && env?.success === false) {
        throw new Error(String(env.message ?? 'Payment methods could not be retrieved'));
      }

      const tseRaw = env && isRecord(env.tseStatus) ? env.tseStatus : null;
      const tseStatusInfo: TseStatusInfo = tseRaw
        ? {
            isConnected: Boolean(tseRaw.isConnected ?? tseRaw.IsConnected),
            lastCheck: String(tseRaw.lastCheck ?? tseRaw.LastCheck ?? ''),
            deviceInfo: String(tseRaw.deviceInfo ?? tseRaw.DeviceInfo ?? ''),
          }
        : { isConnected: false, lastCheck: '', deviceInfo: '' };

      setPaymentMethods(methods.map(toPaymentMethodInfo));
      setTseStatus(tseStatusInfo);
      setIsInitialized(true);
      console.log('✅ Payment methods fetched successfully:', {
        methodsCount: methods.length,
        tseConnected: tseStatusInfo.isConnected,
      });
    } catch (error: any) {
      console.error('❌ Payment methods fetch error:', error);
      const errorMessage = error.message || 'Payment methods could not be loaded';
      setError(errorMessage);

      // Keep all supported method types visible even in degraded mode.
      setPaymentMethods([
        {
          method: 'cash',
          name: 'Cash',
          description: 'Payment at the cash register',
          isEnabled: true,
          requiresTse: true,
          icon: 'cash-outline',
          minAmount: 0.01,
          maxAmount: 100000.0
        },
        {
          method: 'card',
          name: 'Card',
          description: 'Card terminal payment',
          isEnabled: true,
          requiresTse: true,
          icon: 'card-outline',
          minAmount: 0.01,
          maxAmount: 100000.0
        },
        {
          method: 'voucher',
          name: 'Voucher',
          description: 'Voucher payment',
          isEnabled: true,
          requiresTse: false,
          icon: 'gift-outline',
          minAmount: 0.01,
          maxAmount: 100000.0
        },
        {
          method: 'transfer',
          name: 'Transfer',
          description: 'Bank transfer payment',
          isEnabled: true,
          requiresTse: true,
          icon: 'swap-horizontal-outline',
          minAmount: 0.01,
          maxAmount: 100000.0
        }
      ]);
      setIsInitialized(true); // Hata durumunda da initialized olarak işaretle
    } finally {
      setLoading(false);
    }
  }, [user, cashRegisterId, isInitialized, paymentMethods.length]);

  useEffect(() => {
    setIsInitialized(false);
    setPaymentMethods([]);
  }, [cashRegisterId]);

  // OPTIMIZATION: Sadece user değiştiğinde ve henüz initialize edilmemişse fetch yap
  useEffect(() => {
    if (user && cashRegisterId?.trim() && !isInitialized) {
      fetchPaymentMethods();
    } else if (!user) {
      setPaymentMethods([]);
      setTseStatus(null);
      setError(null);
      setIsInitialized(false); // Reset initialization flag
    }
  }, [user, cashRegisterId, isInitialized, fetchPaymentMethods]);

  // Get specific payment method
  const getPaymentMethod = useCallback((method: string): PaymentMethodInfo | undefined => {
    return paymentMethods.find(pm => pm.method === method);
  }, [paymentMethods]);

  // Get active payment methods
  const getActivePaymentMethods = useCallback((): PaymentMethodInfo[] => {
    return paymentMethods.filter(pm => pm.isEnabled);
  }, [paymentMethods]);

  // Get TSE required payment methods
  const getTseRequiredMethods = useCallback((): PaymentMethodInfo[] => {
    return paymentMethods.filter(pm => pm.requiresTse && pm.isEnabled);
  }, [paymentMethods]);

  // Get available methods for specific amount
  const getAvailableMethodsForAmount = useCallback((amount: number): PaymentMethodInfo[] => {
    return paymentMethods.filter(pm =>
      pm.isEnabled &&
      amount >= pm.minAmount &&
      amount <= pm.maxAmount
    );
  }, [paymentMethods]);

  // Clear error
  const clearError = useCallback(() => {
    setError(null);
  }, []);

  // OPTIMIZATION: Manuel refresh için - sadece gerektiğinde kullanılır
  const refreshPaymentMethods = useCallback(() => {
    console.log('🔄 Manual refresh of payment methods...');
    setIsInitialized(false); // Reset initialization flag to force fresh fetch
    fetchPaymentMethods();
  }, [fetchPaymentMethods]);

  return {
    paymentMethods,
    tseStatus,
    loading,
    error,
    isInitialized, // Yeni: Initialization status
    getPaymentMethod,
    getActivePaymentMethods,
    getTseRequiredMethods,
    getAvailableMethodsForAmount,
    clearError,
    refreshPaymentMethods
  };
} 