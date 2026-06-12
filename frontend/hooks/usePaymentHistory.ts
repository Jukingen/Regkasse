import { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';

import {
  fetchPaymentHistory,
  paymentHistoryLabelKeyToI18n,
  postStorno,
  type PaymentHistoryAvailableAction,
  type PaymentHistoryItem,
  type PaymentHistoryResponse,
  type StornoRequestPayload,
  type StornoResponsePayload,
} from '../services/api/paymentHistoryService';
import { paymentService } from '../services/api/paymentService';

function readApiErrorMessage(error: unknown): string {
  const e = error as { response?: { data?: unknown }; message?: string } | null;
  const data = e?.response?.data;
  if (data && typeof data === 'object' && data !== null) {
    const record = data as Record<string, unknown>;
    if (typeof record.message === 'string' && record.message) return record.message;
    if (typeof record.error === 'string' && record.error) return record.error;
  }
  if (typeof e?.message === 'string' && e.message) return e.message;
  return 'Zahlungshistorie konnte nicht geladen werden';
}

function isPaymentHistoryNoRegisterError(error: unknown): boolean {
  const e = error as { response?: { status?: number; data?: unknown } } | null;
  if (e?.response?.status !== 400) return false;
  const data = e.response.data;
  if (!data || typeof data !== 'object') return false;
  const record = data as Record<string, unknown>;
  const details = record.details;
  if (!details || typeof details !== 'object') return false;
  return (details as Record<string, unknown>).code === 'POS_PAYMENT_HISTORY_NO_REGISTER';
}

export type UsePaymentHistoryOptions = {
  hours?: number;
  limit?: number;
  offset?: number;
  cashRegisterId?: string | null;
  enabled?: boolean;
};

export function usePaymentHistory(options: UsePaymentHistoryOptions = {}) {
  const { i18n } = useTranslation();
  const language = (i18n.language || 'de').split('-')[0];
  const hours = options.hours ?? 24;
  const limit = options.limit ?? 20;
  const offset = options.offset ?? 0;
  const cashRegisterId = options.cashRegisterId?.trim() || null;
  const shouldFetch = options.enabled !== false && Boolean(cashRegisterId);

  const [data, setData] = useState<PaymentHistoryResponse | null>(null);
  const [payments, setPayments] = useState<PaymentHistoryItem[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isRefetching, setIsRefetching] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(
    async (opts?: { silent?: boolean }) => {
      if (!shouldFetch) {
        setData(null);
        setPayments([]);
        setError(null);
        return null;
      }

      if (opts?.silent) setIsRefetching(true);
      else setIsLoading(true);
      setError(null);

      try {
        const result = await fetchPaymentHistory({
          hours,
          language,
          limit,
          offset,
          cashRegisterId,
        });
        setData(result);
        setPayments(result.payments);
        return result;
      } catch (e) {
        if (isPaymentHistoryNoRegisterError(e)) {
          setData(null);
          setPayments([]);
          setError(null);
          return null;
        }
        setError(readApiErrorMessage(e));
        return null;
      } finally {
        setIsLoading(false);
        setIsRefetching(false);
      }
    },
    [shouldFetch, hours, language, limit, offset, cashRegisterId]
  );

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return {
    data,
    payments,
    isLoading,
    isRefetching,
    error,
    refresh,
    refetch: refresh,
    hours,
    language,
  };
}

export type StornoMutationInput = StornoRequestPayload;

export function useStorno(onSuccess?: () => void) {
  const { t } = useTranslation();
  const [isPending, setIsPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastResult, setLastResult] = useState<StornoResponsePayload | null>(null);

  const translateBackendKey = useCallback(
    (key?: string | null, fallback?: string) => {
      if (!key) return fallback ?? '';
      const i18nKey = paymentHistoryLabelKeyToI18n(key);
      const translated = t(i18nKey, { defaultValue: '' });
      return translated || fallback || key;
    },
    [t]
  );

  const mutateAsync = useCallback(
    async (input: StornoMutationInput): Promise<StornoResponsePayload> => {
      setIsPending(true);
      setError(null);
      try {
        const result = await postStorno(input);
        setLastResult(result);
        if (!result.success) {
          const msg = translateBackendKey(
            result.errorKey,
            'Storno konnte nicht durchgeführt werden'
          );
          setError(msg);
        } else {
          onSuccess?.();
        }
        return result;
      } catch (e) {
        const msg = readApiErrorMessage(e);
        setError(msg);
        const failed: StornoResponsePayload = { success: false, errorKey: 'errors.stornoFailed' };
        setLastResult(failed);
        return failed;
      } finally {
        setIsPending(false);
      }
    },
    [onSuccess, translateBackendKey]
  );

  return {
    mutateAsync,
    isPending,
    error,
    lastResult,
    translateBackendKey,
  };
}

export type RefundMutationInput = {
  paymentId: string;
  amount: number;
  reason: string;
};

export type RefundResponsePayload = {
  success: boolean;
  errorKey?: string | null;
  messageKey?: string | null;
  paymentId?: string | null;
};

export function useRefund(onSuccess?: () => void) {
  const { t } = useTranslation();
  const [isPending, setIsPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastResult, setLastResult] = useState<RefundResponsePayload | null>(null);

  const translateBackendKey = useCallback(
    (key?: string | null, fallback?: string) => {
      if (!key) return fallback ?? '';
      const i18nKey = paymentHistoryLabelKeyToI18n(key);
      const translated = t(i18nKey, { defaultValue: '' });
      return translated || fallback || key;
    },
    [t]
  );

  const mutateAsync = useCallback(
    async (input: RefundMutationInput): Promise<RefundResponsePayload> => {
      setIsPending(true);
      setError(null);
      try {
        const response = await paymentService.refundPayment(
          input.paymentId,
          input.amount,
          input.reason
        );
        const result: RefundResponsePayload = response.success
          ? {
              success: true,
              messageKey: 'messages.refundSuccess',
              paymentId: response.paymentId || null,
            }
          : {
              success: false,
              errorKey: 'errors.stornoFailed',
            };
        setLastResult(result);
        if (!result.success) {
          setError(
            translateBackendKey(result.errorKey, 'Rückerstattung konnte nicht durchgeführt werden')
          );
        } else {
          onSuccess?.();
        }
        return result;
      } catch (e) {
        const msg = readApiErrorMessage(e);
        setError(msg);
        const failed: RefundResponsePayload = { success: false, errorKey: 'errors.stornoFailed' };
        setLastResult(failed);
        return failed;
      } finally {
        setIsPending(false);
      }
    },
    [onSuccess, translateBackendKey]
  );

  return {
    mutateAsync,
    isPending,
    error,
    lastResult,
    translateBackendKey,
  };
}

export function usePaymentHistoryLabels() {
  const { t } = useTranslation();

  const resolveLabel = useCallback(
    (labelKey: string, fallback = '') => {
      const i18nKey = paymentHistoryLabelKeyToI18n(labelKey);
      const translated = t(i18nKey, { defaultValue: '' });
      return translated || fallback || labelKey;
    },
    [t]
  );

  return { resolveLabel };
}

/** Loads history and wires storno/refund success to silent refresh (POS screen convenience). */
export function usePaymentHistoryScreen(options: UsePaymentHistoryOptions = {}) {
  const history = usePaymentHistory(options);
  const { resolveLabel } = usePaymentHistoryLabels();
  const onSuccess = () => {
    void history.refresh({ silent: true });
  };
  const storno = useStorno(onSuccess);
  const refund = useRefund(onSuccess);

  return { ...history, storno, refund, resolveLabel };
}

export type { PaymentHistoryItem, PaymentHistoryAvailableAction, PaymentHistoryResponse };
