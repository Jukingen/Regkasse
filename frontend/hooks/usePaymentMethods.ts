import React, { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../contexts/AuthContext';
import { API_BASE_URL } from '../config'; // Config'den API URL'ini al

// English Description: Hook for fetching and managing payment methods from backend. Also checks TSE status.

export interface PaymentMethodInfo {
  method: 'cash' | 'card' | 'voucher';
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

export interface PaymentMethodsResponse {
  success: boolean;
  methods: PaymentMethodInfo[];
  tseStatus: TseStatusInfo;
  message: string;
}

export function usePaymentMethods(user: any) {
  const [paymentMethods, setPaymentMethods] = useState<PaymentMethodInfo[]>([]);
  const [tseStatus, setTseStatus] = useState<TseStatusInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Fetch payment methods from backend
  const fetchPaymentMethods = useCallback(async () => {
    // Token kontrolü
    if (!user || !user.token) {
      console.error('❌ No user or token available');
      setError('User not authenticated. Please login first.');
      setPaymentMethods([]);
      return;
    }

    try {
      setLoading(true);
      setError(null);
      console.log('🔄 Fetching payment methods from backend...');
      console.log('🔐 Using token:', user.token ? 'Available' : 'Missing');

      const response = await fetch(`${API_BASE_URL}/Payment/methods`, { // Absolute URL kullan
        method: 'GET',
        headers: {
          'Authorization': `Bearer ${user.token}`, // Token zaten Bearer prefix olmadan, burada ekliyoruz
          'Content-Type': 'application/json'
        }
      });

      if (!response.ok) {
        if (response.status === 401) {
          throw new Error('Authentication failed. Please login again.');
        }
        const errorData = await response.json();
        throw new Error(errorData.message || `HTTP ${response.status}: ${response.statusText}`);
      }

      const data: PaymentMethodsResponse = await response.json();
      
      if (data.success) {
        setPaymentMethods(data.methods);
        setTseStatus(data.tseStatus);
        console.log('✅ Payment methods fetched successfully:', {
          methodsCount: data.methods.length,
          tseConnected: data.tseStatus.isConnected
        });
      } else {
        throw new Error(data.message || 'Payment methods could not be retrieved');
      }
    } catch (error: any) {
      console.error('❌ Payment methods fetch error:', error);
      const errorMessage = error.message || 'Payment methods could not be loaded';
      setError(errorMessage);
      
      // Show default methods on error
      setPaymentMethods([
        {
          method: 'cash',
          name: 'Cash',
          description: 'Payment at the cash register',
          isEnabled: true,
          requiresTse: false,
          icon: 'cash-icon',
          minAmount: 0.01,
          maxAmount: 1000.00
        }
      ]);
    } finally {
      setLoading(false);
    }
  }, [user]);

  // Reload payment methods when user changes
  useEffect(() => {
    if (user) {
      fetchPaymentMethods();
    } else {
      setPaymentMethods([]);
      setTseStatus(null);
      setError(null);
    }
  }, [user, fetchPaymentMethods]);

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

  // Refresh payment methods
  const refreshPaymentMethods = useCallback(() => {
    fetchPaymentMethods();
  }, [fetchPaymentMethods]);

  return {
    paymentMethods,
    tseStatus,
    loading,
    error,
    getPaymentMethod,
    getActivePaymentMethods,
    getTseRequiredMethods,
    getAvailableMethodsForAmount,
    clearError,
    refreshPaymentMethods
  };
} 