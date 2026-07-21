import React, { useCallback, useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Modal,
  View,
  Text,
  Pressable,
  TextInput,
  ActivityIndicator,
  StyleSheet,
  Alert,
} from 'react-native';

import { confirmCardPayment, createCardPaymentIntent } from '../services/api/cardPaymentService';

export type CardPaymentModalProps = {
  visible: boolean;
  amount: number;
  cashRegisterId: string;
  receiptNumber?: string;
  onSuccess: (transactionId: string) => void;
  onClose: () => void;
};

/** POS Kartenzahlung (Mock / Stripe-ready) — UI de-DE. */
export function CardPaymentModal({
  visible,
  amount,
  cashRegisterId,
  receiptNumber,
  onSuccess,
  onClose,
}: CardPaymentModalProps) {
  const { t } = useTranslation(['payment', 'common']);
  const [cardNumber, setCardNumber] = useState('');
  const [expiry, setExpiry] = useState('');
  const [cvc, setCvc] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!visible) return;
    setCardNumber('4242424242424242');
    setExpiry('');
    setCvc('');
    setError(null);
    setLoading(false);
  }, [visible]);

  const formatExpiry = useCallback((value: string) => {
    const digits = value.replace(/\D/g, '').slice(0, 4);
    if (digits.length <= 2) return digits;
    return `${digits.slice(0, 2)}/${digits.slice(2)}`;
  }, []);

  const handlePayment = async () => {
    const digits = cardNumber.replace(/\D/g, '');
    if (digits.length < 13) {
      setError(t('payment:cardPayment.invalidCardNumber'));
      return;
    }
    if (!cashRegisterId) {
      Alert.alert(t('common:error'), t('payment:cardPayment.noRegisterAssigned'));
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const intent = await createCardPaymentIntent({
        amount,
        cashRegisterId,
        receiptNumber,
      });

      const confirm = await confirmCardPayment({
        paymentIntentId: intent.id,
        paymentMethodId: digits,
      });

      if (!confirm.success || !confirm.transactionId) {
        setError(confirm.errorMessage ?? t('payment:cardPayment.declined'));
        return;
      }

      onSuccess(confirm.transactionId);
      onClose();
    } catch (e) {
      const msg = e instanceof Error ? e.message : t('payment:cardPayment.failed');
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <View style={styles.backdrop}>
        <View style={styles.container}>
          <Text style={styles.title}>{t('payment:cardPayment.title')}</Text>
          <Text style={styles.amount}>{amount.toFixed(2)} €</Text>
          <Text style={styles.hint}>{t('payment:cardPayment.testHint')}</Text>

          <TextInput
            style={styles.input}
            placeholder={t('payment:cardPayment.cardNumberPlaceholder')}
            value={cardNumber}
            onChangeText={setCardNumber}
            keyboardType="number-pad"
            maxLength={19}
            editable={!loading}
          />

          <View style={styles.row}>
            <TextInput
              style={[styles.input, styles.halfInput]}
              placeholder={t('payment:cardPayment.expiryPlaceholder')}
              value={expiry}
              onChangeText={(v) => {
                setExpiry(formatExpiry(v));
              }}
              keyboardType="number-pad"
              maxLength={5}
              editable={!loading}
            />
            <TextInput
              style={[styles.input, styles.halfInput]}
              placeholder={t('payment:cardPayment.cvcPlaceholder')}
              value={cvc}
              onChangeText={(v) => {
                setCvc(v.replace(/\D/g, '').slice(0, 4));
              }}
              keyboardType="number-pad"
              secureTextEntry
              maxLength={4}
              editable={!loading}
            />
          </View>

          {error ? <Text style={styles.error}>{error}</Text> : null}

          {loading ? (
            <ActivityIndicator size="large" style={styles.loader} />
          ) : (
            <Pressable style={styles.payButton} onPress={handlePayment}>
              <Text style={styles.payButtonText}>{t('payment:cardPayment.pay')}</Text>
            </Pressable>
          )}

          <Pressable onPress={onClose} disabled={loading} style={styles.cancelWrap}>
            <Text style={styles.cancelText}>{t('payment:cardPayment.cancel')}</Text>
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}

export default CardPaymentModal;

const styles = StyleSheet.create({
  backdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.45)',
    justifyContent: 'center',
    padding: 24,
  },
  container: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 20,
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 4,
  },
  amount: {
    fontSize: 20,
    fontWeight: '700',
    marginBottom: 4,
  },
  hint: {
    fontSize: 12,
    color: '#666',
    marginBottom: 16,
  },
  input: {
    borderWidth: 1,
    borderColor: '#ccc',
    borderRadius: 8,
    padding: 12,
    marginBottom: 12,
    fontSize: 16,
  },
  row: {
    flexDirection: 'row',
    gap: 12,
  },
  halfInput: {
    flex: 1,
  },
  error: {
    color: '#c0392b',
    marginBottom: 12,
  },
  loader: {
    marginVertical: 16,
  },
  payButton: {
    backgroundColor: '#1677ff',
    borderRadius: 8,
    paddingVertical: 14,
    alignItems: 'center',
    marginTop: 4,
  },
  payButtonText: {
    color: '#fff',
    fontWeight: '600',
    fontSize: 16,
  },
  cancelWrap: {
    marginTop: 16,
    alignItems: 'center',
  },
  cancelText: {
    color: '#666',
    fontSize: 15,
  },
});
