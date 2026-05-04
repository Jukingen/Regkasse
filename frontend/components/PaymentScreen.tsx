// Multi-method payment screen; confirms via POST /api/pos/payment.

import React, { useState, useEffect, useMemo } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  TextInput,
  StyleSheet,
  Alert,
  KeyboardAvoidingView,
  Platform,
  ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import paymentService, {
  type PaymentItem,
  type PaymentRequest,
  type PosPaymentMethodCode,
} from '../services/api/paymentService';
import { cartService } from '../services/api/cartService';
import { customerService } from '../services/api/customerService';
import { WALK_IN_CUSTOMER_ID_FALLBACK } from '../constants/walkInCustomer';
import { receiptPrinter } from '../services/receiptPrinter';
import { usePosCashRegisterAssignment } from '../hooks/usePosCashRegisterAssignment';
import { validateAmount } from '../utils/validation';
import { isPaymentError, getPaymentErrorMessage } from '../features/payment/paymentErrors';
import { PaymentCancelResponse } from '../types/cart';
import {
  buildPosRegisterGateContext,
  registerGateAlertMessage,
  registerGateBannerDetail,
  registerGateBannerTitle,
  registerGateFooterHint,
} from '../utils/posRegisterGateCopy';
import { getFormattingLocaleForTextLocale } from '../i18n/localeUtils';

// Desteklenen ödeme yöntemleri ve ikon adları
type PaymentMethodKey = 'cash' | 'card' | 'voucher' | 'contactless' | 'transfer';
const PAYMENT_METHODS: { key: PaymentMethodKey; labelKey: string; icon: keyof typeof Ionicons.glyphMap }[] = [
  { key: 'cash', labelKey: 'payment:methods.cash', icon: 'cash' },
  { key: 'card', labelKey: 'payment:methods.card', icon: 'card' },
  { key: 'voucher', labelKey: 'payment:methods.voucher', icon: 'pricetag' },
  { key: 'transfer', labelKey: 'payment:methods.transfer', icon: 'swap-horizontal' },
  { key: 'contactless', labelKey: 'payment:methods.contactless', icon: 'wifi' },
];

/** One line per cart row for POST /api/pos/payment (same contract as PaymentModal). */
export type PaymentScreenCartLine = {
  productId: string;
  quantity: number;
  /** Cart API returns numeric TaxType (1–4) or legacy string labels. */
  taxType?: string | number;
};

type PaymentScreenProps = {
  totalAmount: number;
  paymentSessionId?: string;
  /** Cart lines for fiscal payment payload (required for real API call). */
  cartLines: PaymentScreenCartLine[];
  /** Table number for GET /pos/cart/current and payment request. */
  tableNumber: number;
  /** Called after fiscal success, cart complete, reset, and print. */
  onPaymentSuccess?: (paymentId: string) => void;
  /** Legacy: still invoked after successful flow (for callers that only need split amounts). */
  onConfirm: (payments: Record<PaymentMethodKey, string>) => void;
  onCancel: () => void;
  onPaymentCancelled?: (response: PaymentCancelResponse) => void;
  /** When false, cash register resolution pauses (e.g. modal closed) to avoid stale state. */
  cashRegisterResolutionActive?: boolean;
};

function mapMethodForBackend(key: PaymentMethodKey): PosPaymentMethodCode {
  if (key === 'contactless') return 'card';
  return key as PosPaymentMethodCode;
}

function pickDominantMethod(payments: Record<PaymentMethodKey, string>): PosPaymentMethodCode {
  let best: PaymentMethodKey = 'cash';
  let max = -1;
  for (const m of PAYMENT_METHODS) {
    const v = parseFloat(payments[m.key]) || 0;
    if (v > max) {
      max = v;
      best = m.key;
    }
  }
  return mapMethodForBackend(best);
}

const PaymentScreen: React.FC<PaymentScreenProps> = ({
  totalAmount,
  paymentSessionId,
  cartLines,
  tableNumber,
  onPaymentSuccess,
  onConfirm,
  onCancel,
  onPaymentCancelled,
  cashRegisterResolutionActive = true,
}) => {
  const { t, i18n } = useTranslation(['checkout', 'payment', 'common', 'settings']);
  const {
    cashRegisterId,
    cashRegisterResolved,
    settingsLoadFailed,
    retryUserSettingsLoad,
    registerPicklist,
    registerListLoading,
    registerListFailureKind,
    registerListEmptyReason,
    refetchRegisterList,
    savingRegisterId,
    hasValidCashRegisterId,
    isRegisterGateBlockingPayment,
    handlePersistCashRegister,
    refreshPosReadiness,
    posReadinessLoading,
    posReadinessError,
    posReadinessNextAction,
    posReadinessMessageCode,
    posReadinessRegisterStatus,
  } = usePosCashRegisterAssignment(cashRegisterResolutionActive);

  const registerGateCtx = useMemo(
    () =>
      buildPosRegisterGateContext({
        settingsLoadFailed,
        registerListFailureKind,
        registerListLoading,
        registerPicklistCount: registerPicklist.length,
        registerListEmptyReason,
        readiness: {
          loading: posReadinessLoading,
          error: posReadinessError,
          nextAction: posReadinessNextAction,
          messageCode: posReadinessMessageCode,
          registerStatus: posReadinessRegisterStatus,
        },
      }),
    [
      settingsLoadFailed,
      registerListFailureKind,
      registerListLoading,
      registerPicklist.length,
      registerListEmptyReason,
      posReadinessLoading,
      posReadinessError,
      posReadinessNextAction,
      posReadinessMessageCode,
      posReadinessRegisterStatus,
    ]
  );

  const [payments, setPayments] = useState<Record<PaymentMethodKey, string>>({
    cash: '',
    card: '',
    voucher: '',
    contactless: '',
    transfer: '',
  });
  const [processing, setProcessing] = useState(false);
  const [error, setError] = useState('');
  const [guestCustomerId, setGuestCustomerId] = useState<string>(WALK_IN_CUSTOMER_ID_FALLBACK);

  const enteredTotal = (Object.values(payments) as string[]).reduce((sum, val) => sum + (parseFloat(val) || 0), 0);

  useEffect(() => {
    customerService
      .getGuestCustomer()
      .then((id) => setGuestCustomerId(id))
      .catch(() => setGuestCustomerId(WALK_IN_CUSTOMER_ID_FALLBACK));
  }, []);

  const handleChange = (method: PaymentMethodKey, value: string) => {
    if (!/^\d*\.?\d*$/.test(value)) return;
    setPayments((prev) => ({ ...prev, [method]: value }));
  };

  const handlePaymentCancel = async () => {
    if (!paymentSessionId) {
      onCancel();
      return;
    }

    Alert.alert(
      t('checkout:posFlow.payment.alerts.cancelPaymentTitle'),
      t('checkout:posFlow.payment.alerts.cancelPaymentConfirm'),
      [
        { text: t('checkout:posFlow.payment.buttons.cancelNo'), style: 'cancel' },
        {
          text: t('checkout:posFlow.payment.buttons.cancelYes'),
          style: 'destructive',
          onPress: async () => {
            setProcessing(true);
            try {
              const cancelResponse = await paymentService.cancelPayment(paymentSessionId, t('checkout:posFlow.payment.cancelReasonByCashier'));

              const ok = cancelResponse && (cancelResponse as { success?: boolean }).success !== false;

              if (ok) {
                if (onPaymentCancelled) {
                  onPaymentCancelled(cancelResponse);
                }

                Alert.alert(
                  t('checkout:posFlow.payment.alerts.paymentCancelledTitle'),
                  t('checkout:posFlow.payment.cancelSummary', {
                    reason: cancelResponse.cancellationReason,
                    cancelledBy: cancelResponse.cancelledBy,
                    cancelledAt: new Date(cancelResponse.cancelledAt).toLocaleString(
                      getFormattingLocaleForTextLocale(i18n.resolvedLanguage || i18n.language)
                    ),
                  }),
                  [{ text: t('common:ok'), onPress: () => onCancel() }]
                );
              } else {
                Alert.alert(t('checkout:posFlow.payment.alerts.errorTitle'), t('checkout:posFlow.payment.alerts.cancelRequestFailed'));
              }
            } catch (err) {
              console.error('Payment cancellation error:', err);
              Alert.alert(t('checkout:posFlow.payment.alerts.errorTitle'), t('checkout:posFlow.payment.alerts.cancelFailed'));
            } finally {
              setProcessing(false);
            }
          },
        },
      ]
    );
  };

  const handleConfirm = async () => {
    // No Alert: confirm button is disabled while processing.
    if (processing) return;
    setError('');
    if (enteredTotal < totalAmount) {
      setError(t('checkout:posFlow.payment.errors.insufficientAmount'));
      return;
    }
    if (!tableNumber) {
      Alert.alert(t('checkout:posFlow.payment.alerts.errorTitle'), t('checkout:posFlow.payment.errors.missingTableNumber'));
      return;
    }
    if (!cartLines.length) {
      setError(t('checkout:posFlow.payment.errors.emptyCart'));
      return;
    }
    if (!cashRegisterResolved) {
      setError(t('checkout:posFlow.payment.loadingRegisterSettings'));
      return;
    }
    if (!hasValidCashRegisterId || !cashRegisterId) {
      Alert.alert(
        t('checkout:posFlow.payment.alerts.paymentNotPossibleTitle'),
        registerGateAlertMessage(registerGateCtx)
      );
      return;
    }
    if (!validateAmount(totalAmount)) {
      setError(t('checkout:posFlow.payment.errors.invalidAmount'));
      return;
    }

    setProcessing(true);
    try {
      let currentCartId: string;
      try {
        const cart = await cartService.getCurrentCart(tableNumber);
        if (!cart?.cartId) {
          throw new Error('Cart not found');
        }
        currentCartId = cart.cartId;
      } catch (cartErr) {
        console.error('[PaymentScreen] Cart fetch failed:', cartErr);
        Alert.alert(t('checkout:posFlow.payment.alerts.errorTitle'), t('checkout:posFlow.payment.errors.cartLoadFailed'));
        return;
      }

      const dominant = pickDominantMethod(payments);
      // taxType: paymentService.processPayment normalizes all items before POST / queue
      const paymentItems: PaymentItem[] = cartLines.map((line) => ({
        productId: line.productId,
        quantity: line.quantity,
        taxType: line.taxType as PaymentItem['taxType'],
      }));

      const shouldRequireTse = __DEV__ ? false : true;

      const idempotencyKey =
        typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
          ? crypto.randomUUID()
          : `${Date.now()}-${Math.random().toString(36).slice(2, 15)}`;

      const paymentRequest: PaymentRequest = {
        customerId: guestCustomerId,
        items: paymentItems,
        payment: {
          method: dominant,
          tseRequired: shouldRequireTse,
          amount: dominant === 'cash' ? parseFloat(payments.cash) || enteredTotal : undefined,
        },
        tableNumber: tableNumber || 1,
        totalAmount,
        cashRegisterId,
        notes: `Tisch ${tableNumber} - ${new Date().toLocaleString(
          getFormattingLocaleForTextLocale(i18n.resolvedLanguage || i18n.language),
        )}`,
        idempotencyKey,
      };

      const response = await paymentService.processPayment(paymentRequest);

      if (response.fiscalStatus === 'NON_FISCAL_PENDING') {
        Alert.alert(
          t('checkout:posFlow.payment.alerts.hintTitle'),
          t('checkout:posFlow.payment.alerts.fiscalPending')
        );
        return;
      }

      if (
        !response.success ||
        response.fiscalStatus !== 'FISCAL_COMPLETE' ||
        !response.paymentId
      ) {
        const msg =
          response.fiscalStatus === 'FAILED'
            ? response.message || response.error || t('checkout:posFlow.payment.errors.fiscalNotConfirmed')
            : response.message || response.error || t('checkout:posFlow.payment.errors.failed');
        Alert.alert(t('checkout:posFlow.payment.errors.failed'), msg);
        return;
      }

      if (response.invoicePersisted === false) {
        Alert.alert(
          t('checkout:posFlow.payment.alerts.hintTitle'),
          t('checkout:posFlow.payment.alerts.invoiceReconcileAttention')
        );
      }

      try {
        await cartService.completeCart(currentCartId, '');
      } catch (completeErr) {
        console.error('[PaymentScreen] completeCart failed:', completeErr);
        Alert.alert(t('checkout:posFlow.payment.alerts.hintTitle'), t('checkout:posFlow.payment.errors.completeCartFailed'));
      }

      try {
        await cartService.resetCartAfterPayment(currentCartId, 'Payment completed');
      } catch (resetErr) {
        console.warn('[PaymentScreen] resetCartAfterPayment:', resetErr);
      }

      try {
        await receiptPrinter.print(response.paymentId, {
          isDemoFiscal: response.tse?.isDemoFiscal ?? false,
        });
      } catch (printErr) {
        console.error('[PaymentScreen] Print failed:', printErr);
        Alert.alert(t('checkout:posFlow.payment.alerts.hintTitle'), t('checkout:posFlow.payment.errors.printFailed'));
      }

      onPaymentSuccess?.(response.paymentId);
      onConfirm(payments);
    } catch (e) {
      const message = isPaymentError(e)
        ? getPaymentErrorMessage(e.code)
        : e instanceof Error
          ? e.message
          : t('checkout:posFlow.payment.errors.genericFailed');
      setError(message);
      Alert.alert(t('checkout:posFlow.payment.alerts.errorTitle'), message);
    } finally {
      setProcessing(false);
    }
  };

  return (
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.container}>
      <Text style={styles.title}>{t('checkout:posFlow.payment.title')}</Text>
      <Text style={styles.total}>{t('checkout:posFlow.payment.totalLabel')}: {totalAmount.toFixed(2)} €</Text>

      {isRegisterGateBlockingPayment ? (
        <View style={styles.registerBanner}>
          <Text style={styles.registerBannerTitle}>
            {registerGateBannerTitle(registerGateCtx)}
          </Text>
          {!registerListLoading && !posReadinessLoading ? (
            <Text style={[styles.registerBannerMuted, { marginBottom: 4 }]}>{t('settings:registerGate.banner.intro')}</Text>
          ) : null}
          <Text style={styles.registerBannerText}>
            {registerGateBannerDetail(registerGateCtx)}
          </Text>
          {registerListLoading || posReadinessLoading ? (
            <ActivityIndicator color="#1976d2" style={{ marginVertical: 8 }} />
          ) : registerPicklist.length > 0 ? (
            <View style={styles.registerChipRow}>
              {registerPicklist.map((r) => (
                <TouchableOpacity
                  key={r.id}
                  style={[styles.registerChip, savingRegisterId === r.id && styles.registerChipDisabled]}
                  disabled={!!savingRegisterId}
                  onPress={() => handlePersistCashRegister(r.id)}
                >
                  <Text style={styles.registerChipText}>{r.registerNumber || r.id.slice(0, 8)}</Text>
                </TouchableOpacity>
              ))}
            </View>
          ) : settingsLoadFailed ? (
            <TouchableOpacity onPress={retryUserSettingsLoad} accessibilityRole="button">
              <Text style={styles.registerRetryLink}>{t('checkout:posFlow.payment.retryActions.retrySettings')}</Text>
            </TouchableOpacity>
          ) : posReadinessError ? (
            <TouchableOpacity onPress={() => refreshPosReadiness()} accessibilityRole="button">
              <Text style={styles.registerRetryLink}>{t('checkout:posFlow.payment.retryActions.retryReadiness')}</Text>
            </TouchableOpacity>
          ) : registerListFailureKind === 'network' || registerListFailureKind === 'unknown' ? (
            <TouchableOpacity onPress={refetchRegisterList} accessibilityRole="button">
              <Text style={styles.registerRetryLink}>{t('checkout:posFlow.payment.retryActions.reloadRegisterList')}</Text>
            </TouchableOpacity>
          ) : null}
        </View>
      ) : null}

      {!cashRegisterResolved ? (
        <Text style={styles.registerBannerMuted}>{t('checkout:posFlow.payment.loadingRegisterSettings')}</Text>
      ) : null}
      {PAYMENT_METHODS.map((method) => (
        <View key={method.key} style={styles.methodRow}>
          <Ionicons name={method.icon} size={22} color="#1976d2" style={{ marginRight: 8 }} />
          <Text style={styles.methodLabel}>{t(method.labelKey)}</Text>
          <TextInput
            style={styles.input}
            keyboardType="decimal-pad"
            placeholder={t('checkout:posFlow.payment.placeholderAmount')}
            value={payments[method.key]}
            onChangeText={(val) => handleChange(method.key as PaymentMethodKey, val)}
            editable={!processing}
          />
          <Text style={styles.euro}>€</Text>
        </View>
      ))}
      <View style={styles.summaryRow}>
        <Text style={enteredTotal < totalAmount ? styles.missing : styles.ok}>
          {t('checkout:posFlow.payment.enteredTotalLabel')}: {enteredTotal.toFixed(2)} €
        </Text>
      </View>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <View style={styles.buttonRow}>
        <TouchableOpacity style={styles.cancelBtn} onPress={handlePaymentCancel} disabled={processing}>
          <Text style={styles.buttonText}>{t('checkout:posFlow.payment.buttons.cancel')}</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[
            styles.confirmBtn,
            {
              opacity: enteredTotal < totalAmount || isRegisterGateBlockingPayment ? 0.5 : 1,
            },
          ]}
          onPress={handleConfirm}
          disabled={processing || enteredTotal < totalAmount || isRegisterGateBlockingPayment}
          accessibilityHint={
            isRegisterGateBlockingPayment && !processing
              ? registerGateFooterHint(registerGateCtx)
              : undefined
          }
        >
          <Text style={styles.buttonText}>{t('checkout:posFlow.payment.buttons.confirm')}</Text>
        </TouchableOpacity>
      </View>
      {isRegisterGateBlockingPayment ? (
        <Text style={styles.registerFooterHint}>
          {registerGateFooterHint(registerGateCtx)}
        </Text>
      ) : null}
    </KeyboardAvoidingView>
  );
};

const styles = StyleSheet.create({
  container: { backgroundColor: '#fff', padding: 16, borderRadius: 12, alignItems: 'center' },
  title: { fontSize: 18, fontWeight: 'bold', marginBottom: 8 },
  total: { fontSize: 16, fontWeight: '600', marginBottom: 12 },
  methodRow: { flexDirection: 'row', alignItems: 'center', marginBottom: 8, width: '100%' },
  methodLabel: { flex: 1, fontSize: 15 },
  input: { width: 70, borderWidth: 1, borderColor: '#ccc', borderRadius: 6, padding: 6, fontSize: 15, textAlign: 'right', backgroundColor: '#f9f9f9' },
  euro: { marginLeft: 4, fontSize: 15 },
  summaryRow: { marginTop: 8, marginBottom: 4, alignSelf: 'flex-end' },
  missing: { color: '#d32f2f', fontWeight: 'bold' },
  ok: { color: '#388e3c', fontWeight: 'bold' },
  error: { color: '#d32f2f', marginTop: 4, marginBottom: 4 },
  buttonRow: { flexDirection: 'row', marginTop: 12 },
  cancelBtn: { backgroundColor: '#eee', borderRadius: 8, padding: 10, marginRight: 8 },
  confirmBtn: { backgroundColor: '#27ae60', borderRadius: 8, padding: 10 },
  buttonText: { color: '#222', fontWeight: 'bold', fontSize: 15 },
  registerBanner: {
    width: '100%',
    backgroundColor: '#fff8e1',
    borderRadius: 8,
    padding: 10,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: '#ffe082',
  },
  registerBannerTitle: { fontWeight: 'bold', color: '#e65100', marginBottom: 4 },
  registerBannerText: { fontSize: 12, color: '#5d4037', marginBottom: 6 },
  registerBannerMuted: { fontSize: 12, color: '#795548', fontStyle: 'italic', marginBottom: 6 },
  registerChipRow: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  registerRetryLink: { color: '#1565c0', fontWeight: '600', fontSize: 14, marginTop: 6, textDecorationLine: 'underline' },
  registerFooterHint: { fontSize: 11, color: '#666', marginTop: 8, textAlign: 'center', paddingHorizontal: 8 },
  registerChip: {
    paddingVertical: 8,
    paddingHorizontal: 12,
    borderRadius: 20,
    borderWidth: 1,
    borderColor: '#1976d2',
    backgroundColor: '#fff',
  },
  registerChipDisabled: { opacity: 0.6 },
  registerChipText: { color: '#1976d2', fontWeight: '600', fontSize: 13 },
});

export default PaymentScreen;
