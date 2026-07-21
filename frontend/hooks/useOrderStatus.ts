import { useCallback, useState } from 'react';

import {
  fetchPublicOnlineOrderStatus,
  type PublicOnlineOrderStatus,
} from '../services/api/onlineOrderStatusService';

export function useOrderStatus() {
  const [order, setOrder] = useState<PublicOnlineOrderStatus | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notFound, setNotFound] = useState(false);

  const fetchOrder = useCallback(async (tenant: string, orderNumber: string, phone?: string) => {
    if (!tenant.trim() || !orderNumber.trim()) {
      setError('missing_params');
      setOrder(null);
      return null;
    }

    setIsLoading(true);
    setError(null);
    setNotFound(false);
    try {
      const result = await fetchPublicOnlineOrderStatus({
        tenant,
        orderNumber,
        phone,
      });
      setOrder(result);
      return result;
    } catch (err: unknown) {
      setOrder(null);
      const status =
        err && typeof err === 'object' && 'response' in err
          ? (err as { response?: { status?: number } }).response?.status
          : undefined;
      if (status === 404) {
        setNotFound(true);
        setError(null);
      } else {
        setError('fetch_failed');
      }
      return null;
    } finally {
      setIsLoading(false);
    }
  }, []);

  const reset = useCallback(() => {
    setOrder(null);
    setError(null);
    setNotFound(false);
  }, []);

  return { order, isLoading, error, notFound, fetchOrder, reset };
}
