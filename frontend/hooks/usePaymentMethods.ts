// Bu hook, ödeme seçeneklerini sadece backend API'den çeker.
import { useState, useEffect } from 'react';
import { apiClient } from '../services/api/config';

export interface PaymentMethod {
  id: string;
  name: string;
  type: string;
  icon?: string;
}

export function usePaymentMethods() {
  const [methods, setMethods] = useState<PaymentMethod[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    apiClient.get('/payment/methods')
      .then(res => setMethods(res.data))
      .finally(() => setLoading(false));
  }, []);

  return { methods, loading };
} 