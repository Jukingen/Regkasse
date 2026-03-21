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
import { useAuth } from '../contexts/AuthContext';
import paymentService, {
  type PaymentItem,
  type PaymentRequest,
  type PosPaymentMethodCode,
} from '../services/api/paymentService';
import { cartService } from '../services/api/cartService';
import { customerService, GUEST_CUSTOMER_ID } from '../services/api/customerService';
import { receiptPrinter } from '../services/receiptPrinter';
import { usePosCashRegisterAssignment } from '../hooks/usePosCashRegisterAssignment';
import { validateAmount } from '../utils/validation';
import { isPaymentError, getPaymentErrorMessage } from '../features/payment/paymentErrors';
import { PaymentCancelResponse } from '../types/cart';
import { resolveCashierIdForPayment } from '../utils/paymentSessionUser';
import {
  buildPosRegisterGateContext,
  registerGateAlertMessage,
  registerGateBannerDetail,
  registerGateBannerIntro,
  registerGateBannerTitle,
  registerGateFooterHint,
} from '../utils/posRegisterGateCopy';

// Desteklenen ödeme yöntemleri ve ikon adları
type PaymentMethodKey = 'cash' | 'card' | 'voucher' | 'contactless' | 'transfer';
const PAYMENT_METHODS: { key: PaymentMethodKey; label: string; icon: keyof typeof Ionicons.glyphMap }[] = [
  { key: 'cash', label: 'Bargeld', icon: 'cash' },
  { key: 'card', label: 'Kreditkarte', icon: 'card' },
  { key: 'voucher', label: 'Gutschein', icon: 'pricetag' },
  { key: 'transfer', label: 'Überweisung', icon: 'swap-horizontal' },
  { key: 'contactless', label: 'Kontaktlos', icon: 'wifi' },
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
  const { user } = useAuth();
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
  const [guestCustomerId, setGuestCustomerId] = useState<string>(GUEST_CUSTOMER_ID);

  const enteredTotal = (Object.values(payments) as string[]).reduce((sum, val) => sum + (parseFloat(val) || 0), 0);

  useEffect(() => {
    customerService
      .getGuestCustomer()
      .then((id) => setGuestCustomerId(id))
      .catch(() => setGuestCustomerId(GUEST_CUSTOMER_ID));
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
      'Zahlung abbrechen',
      'Möchten Sie diese Zahlung wirklich abbrechen?',
      [
        { text: 'Nein', style: 'cancel' },
        {
          text: 'Ja, abbrechen',
          style: 'destructive',
          onPress: async () => {
            setProcessing(true);
            try {
              const cancelResponse = await paymentService.cancelPayment(paymentSessionId, 'Kasiyer tarafından iptal edildi');

              const ok = cancelResponse && (cancelResponse as { success?: boolean }).success !== false;

              if (ok) {
                if (onPaymentCancelled) {
                  onPaymentCancelled(cancelResponse);
                }

                Alert.alert(
                  'Zahlung abgebrochen',
                  `Die Zahlung wurde storniert.\n\nGrund: ${cancelResponse.cancellationReason}\nVon: ${cancelResponse.cancelledBy}\nZeit: ${new Date(cancelResponse.cancelledAt).toLocaleString('de-DE')}`,
                  [{ text: 'OK', onPress: () => onCancel() }]
                );
              } else {
                Alert.alert('Fehler', 'Stornierung der Zahlung fehlgeschlagen.');
              }
            } catch (err) {
              console.error('Payment cancellation error:', err);
              Alert.alert('Fehler', 'Stornierung fehlgeschlagen. Bitte erneut versuchen.');
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
      setError('Zahlungsbetrag unzureichend.');
      return;
    }
    if (!tableNumber) {
      Alert.alert('Fehler', 'Tischnummer fehlt.');
      return;
    }
    if (!cartLines.length) {
      setError('Warenkorb ist leer.');
      return;
    }
    if (!cashRegisterResolved) {
      setError('Kasseneinstellungen werden geladen…');
      return;
    }
    if (!hasValidCashRegisterId || !cashRegisterId) {
      Alert.alert(
        'Zahlung nicht möglich',
        registerGateAlertMessage(registerGateCtx)
      );
      return;
    }
    if (!validateAmount(totalAmount)) {
      setError('Ungültiger Betrag.');
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
        Alert.alert('Fehler', 'Warenkorb konnte nicht geladen werden. Bitte erneut versuchen.');
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

      const cashierId = await resolveCashierIdForPayment(user?.id);

      const paymentRequest: PaymentRequest = {
        customerId: guestCustomerId,
        items: paymentItems,
        payment: {
          method: dominant,
          tseRequired: shouldRequireTse,
          amount: dominant === 'cash' ? parseFloat(payments.cash) || enteredTotal : undefined,
        },
        tableNumber: tableNumber || 1,
        cashierId,
        totalAmount,
        cashRegisterId,
        notes: `Tisch ${tableNumber} - ${new Date().toLocaleString('de-DE')}`,
        idempotencyKey,
      };

      const response = await paymentService.processPayment(paymentRequest);

      if (response.fiscalStatus === 'NON_FISCAL_PENDING') {
        Alert.alert(
          'Hinweis',
          'Keine Verbindung zur Kasse. Die Zahlung ist nicht fiscal verbucht und steht in der Warteschlange.'
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
            ? response.message || response.error || 'Zahlung nicht fiscal bestätigt'
            : response.message || response.error || 'Zahlung fehlgeschlagen';
        Alert.alert('Zahlung fehlgeschlagen', msg);
        return;
      }

      if (response.invoicePersisted === false) {
        Alert.alert(
          'Hinweis',
          'Zahlung erfolgreich. Die Belegabstimmung erfordert jedoch Ihre Aufmerksamkeit.'
        );
      }

      try {
        await cartService.completeCart(currentCartId, '');
      } catch (completeErr) {
        console.error('[PaymentScreen] completeCart failed:', completeErr);
        Alert.alert('Hinweis', 'Zahlung OK, Warenkorb konnte nicht abgeschlossen werden. Bitte Administrator informieren.');
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
        Alert.alert('Hinweis', 'Zahlung OK, Druck fehlgeschlagen.');
      }

      onPaymentSuccess?.(response.paymentId);
      onConfirm(payments);
    } catch (e) {
      const message = isPaymentError(e)
        ? getPaymentErrorMessage(e.code)
        : e instanceof Error
          ? e.message
          : 'Zahlung fehlgeschlagen. Bitte erneut versuchen.';
      setError(message);
      Alert.alert('Fehler', message);
    } finally {
      setProcessing(false);
    }
  };

  return (
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.container}>
      <Text style={styles.title}>Zahlungsarten</Text>
      <Text style={styles.total}>Gesamt: {totalAmount.toFixed(2)} €</Text>

      {isRegisterGateBlockingPayment ? (
        <View style={styles.registerBanner}>
          <Text style={styles.registerBannerTitle}>
            {registerGateBannerTitle(registerGateCtx)}
          </Text>
          {!registerListLoading && !posReadinessLoading ? (
            <Text style={[styles.registerBannerMuted, { marginBottom: 4 }]}>{registerGateBannerIntro()}</Text>
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
              <Text style={styles.registerRetryLink}>Kasseneinstellungen erneut versuchen</Text>
            </TouchableOpacity>
          ) : posReadinessError ? (
            <TouchableOpacity onPress={() => refreshPosReadiness()} accessibilityRole="button">
              <Text style={styles.registerRetryLink}>Kassenbereitschaft erneut versuchen</Text>
            </TouchableOpacity>
          ) : registerListFailureKind === 'network' || registerListFailureKind === 'unknown' ? (
            <TouchableOpacity onPress={refetchRegisterList} accessibilityRole="button">
              <Text style={styles.registerRetryLink}>Kassenliste erneut laden</Text>
            </TouchableOpacity>
          ) : null}
        </View>
      ) : null}

      {!cashRegisterResolved ? (
        <Text style={styles.registerBannerMuted}>Kasseneinstellungen werden geladen…</Text>
      ) : null}
      {PAYMENT_METHODS.map((method) => (
        <View key={method.key} style={styles.methodRow}>
          <Ionicons name={method.icon} size={22} color="#1976d2" style={{ marginRight: 8 }} />
          <Text style={styles.methodLabel}>{method.label}</Text>
          <TextInput
            style={styles.input}
            keyboardType="decimal-pad"
            placeholder="0.00"
            value={payments[method.key]}
            onChangeText={(val) => handleChange(method.key as PaymentMethodKey, val)}
            editable={!processing}
          />
          <Text style={styles.euro}>€</Text>
        </View>
      ))}
      <View style={styles.summaryRow}>
        <Text style={enteredTotal < totalAmount ? styles.missing : styles.ok}>
          Eingegeben gesamt: {enteredTotal.toFixed(2)} €
        </Text>
      </View>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <View style={styles.buttonRow}>
        <TouchableOpacity style={styles.cancelBtn} onPress={handlePaymentCancel} disabled={processing}>
          <Text style={styles.buttonText}>Abbrechen</Text>
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
          <Text style={styles.buttonText}>Bestätigen</Text>
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
