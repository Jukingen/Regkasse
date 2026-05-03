import { useState, useCallback } from 'react';
import {
  paymentService,
  PaymentRequest,
  PaymentResponse,
  PaymentMethod,
  type VoucherValidateResult,
} from '../services/api/paymentService';
import { getPaymentErrorDisplayMessage, normalizePaymentError } from '../features/payment/paymentErrors';
import { debugPosPaymentTrace } from '../utils/debugPosPaymentTrace';
import { sessionManager } from '../services/session/sessionManager';

// Türkçe Açıklama: Ödeme işlemleri için hook - Backend API ile entegre çalışır
export const usePayment = () => {
  // State split: loading (genel/action) vs methodsLoading (init)
  const [methodsLoading, setMethodsLoading] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [paymentMethods, setPaymentMethods] = useState<PaymentMethod[]>([]);

  // Ödeme yöntemlerini getir
  const getPaymentMethods = useCallback(async () => {
    try {
      setMethodsLoading(true);
      setError(null);

      const methods = await paymentService.getPaymentMethods();
      setPaymentMethods(methods);
      return methods;
    } catch (err) {
      console.error('Failed to load payment methods', err);
      // Hata olsa bile UI bloklanmasın, default listeyi dön
      const defaultMethods: PaymentMethod[] = [
        { id: 'cash', name: 'Nakit', type: 'cash', icon: 'cash-outline', isDefault: true, requiresReceivedAmount: true },
        { id: 'card', name: 'Kart', type: 'card', icon: 'card-outline', requiresReceivedAmount: false },
        { id: 'voucher', name: 'Kupon', type: 'voucher', icon: 'gift-outline', requiresReceivedAmount: false },
        { id: 'transfer', name: 'Havale', type: 'transfer', icon: 'swap-horizontal-outline', requiresReceivedAmount: false },
      ];
      setPaymentMethods(defaultMethods);
      return defaultMethods;
    } finally {
      setMethodsLoading(false);
    }
  }, []);

  const validateVoucher = useCallback(async (voucherCode: string, amount?: number): Promise<VoucherValidateResult> => {
    const token = await sessionManager.getAccessToken();
    if (!token) {
      return { ok: false, errorCode: 'NO_TOKEN', message: 'Nicht angemeldet.' };
    }
    return paymentService.validateVoucher(voucherCode, amount);
  }, []);

  // Ödeme işlemi
  const processPayment = useCallback(async (paymentRequest: PaymentRequest): Promise<PaymentResponse> => {
    debugPosPaymentTrace('use_payment_hook_enter', {
      cashRegisterId: paymentRequest.cashRegisterId,
      itemCount: paymentRequest.items?.length ?? 0,
    });
    try {
      setLoading(true);
      setError(null);

      // Align with apiClient: session is JWT in storage, not AuthContext user (can be briefly null during refresh).
      const token = await sessionManager.getAccessToken();
      if (!token) {
        debugPosPaymentTrace('submit_blocked_missing_token', {});
        throw new Error('Nicht angemeldet. Bitte erneut anmelden.');
      }

      const response = await paymentService.processPayment(paymentRequest);

      if (response.fiscalStatus === 'NON_FISCAL_PENDING') {
        debugPosPaymentTrace('use_payment_non_fiscal_pending', { pendingQueueId: response.pendingQueueId });
        return response;
      }

      if (!response.success) {
        debugPosPaymentTrace('use_payment_service_reported_failure', {
          error: response.error,
          message: response.message,
        });
        throw new Error(response.error ?? 'Zahlung fehlgeschlagen');
      }

      debugPosPaymentTrace('use_payment_hook_success', { paymentId: response.paymentId });
      return response;
    } catch (err) {
      const normalized = normalizePaymentError(err);
      const errorMessage = getPaymentErrorDisplayMessage(normalized);
      setError(errorMessage);
      debugPosPaymentTrace('use_payment_hook_caught', { errorMessage });
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  // Fiş oluştur
  const createReceipt = useCallback(async (paymentId: string) => {
    try {
      setLoading(true);
      setError(null);

      const receipt = await paymentService.createReceipt(paymentId);
      return receipt;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Fiş oluşturulamadı';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  // Ödeme iptal
  const cancelPayment = useCallback(async (paymentId: string, reason: string) => {
    try {
      setLoading(true);
      setError(null);

      const response = await paymentService.cancelPayment(paymentId, reason);
      return response;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Ödeme iptal edilemedi';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  // Ödeme iade
  const refundPayment = useCallback(async (paymentId: string, amount: number, reason: string) => {
    try {
      setLoading(true);
      setError(null);

      const response = await paymentService.refundPayment(paymentId, amount, reason);
      return response;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Ödeme iade edilemedi';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, []);

  // Hata temizle
  const clearError = useCallback(() => {
    setError(null);
  }, []);

  return {
    loading, // İşlem yükleniyor
    methodsLoading, // Liste yükleniyor
    error,
    paymentMethods,
    getPaymentMethods,
    validateVoucher,
    processPayment,
    createReceipt,
    cancelPayment,
    refundPayment,
    clearError
  };
};
