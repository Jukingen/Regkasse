import { useState, useCallback } from 'react';
import { paymentService, PaymentRequest, PaymentResponse, PaymentMethod } from '../services/api/paymentService';
import { useAuth } from '../contexts/AuthContext';

// Türkçe Açıklama: Ödeme işlemleri için hook - Backend API ile entegre çalışır
export const usePayment = () => {
  // State split: loading (genel/action) vs methodsLoading (init)
  const [methodsLoading, setMethodsLoading] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [paymentMethods, setPaymentMethods] = useState<PaymentMethod[]>([]);

  const { user } = useAuth();

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
        { id: 'cash', name: 'Nakit', type: 'cash', icon: 'cash-outline' },
        { id: 'card', name: 'Kart', type: 'card', icon: 'card-outline' },
        { id: 'voucher', name: 'Kupon', type: 'voucher', icon: 'gift-outline' }
      ];
      setPaymentMethods(defaultMethods);
      return defaultMethods;
    } finally {
      setMethodsLoading(false);
    }
  }, []);

  // Ödeme işlemi
  const processPayment = useCallback(async (paymentRequest: PaymentRequest): Promise<PaymentResponse> => {
    try {
      setLoading(true);
      setError(null);

      // Kullanıcı kimlik doğrulaması kontrol et
      if (!user) {
        throw new Error('Kullanıcı girişi yapılmamış');
      }

      // Ödeme işlemini gerçekleştir
      const response = await paymentService.processPayment(paymentRequest);

      if (!response.success) {
        throw new Error(response.error ?? 'Ödeme işlemi başarısız');
      }

      return response;
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Ödeme işlemi başarısız';
      setError(errorMessage);
      throw err;
    } finally {
      setLoading(false);
    }
  }, [user]);

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
    processPayment,
    createReceipt,
    cancelPayment,
    refundPayment,
    clearError
  };
};
