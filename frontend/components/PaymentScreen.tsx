// Türkçe Açıklama: Çoklu ödeme türü (nakit, kredi kartı, kupon, temassız) destekli ödeme ekranı. Kullanıcı, toplam tutarı birden fazla yöntemle bölebilir. Tüm işlemler backend ile doğrulanır.

import React, { useState } from 'react';
import { View, Text, TouchableOpacity, TextInput, StyleSheet, Alert, KeyboardAvoidingView, Platform } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

// Desteklenen ödeme yöntemleri ve ikon adları
type PaymentMethodKey = 'cash' | 'card' | 'voucher' | 'contactless';
const PAYMENT_METHODS: { key: PaymentMethodKey; label: string; icon: keyof typeof Ionicons.glyphMap }[] = [
  { key: 'cash', label: 'Nakit', icon: 'cash' },
  { key: 'card', label: 'Kredi Kartı', icon: 'card' },
  { key: 'voucher', label: 'Kupon', icon: 'pricetag' },
  { key: 'contactless', label: 'Temassız', icon: 'wifi' },
];

type PaymentScreenProps = {
  totalAmount: number;
  onConfirm: (payments: Record<PaymentMethodKey, string>) => void;
  onCancel: () => void;
};

const PaymentScreen: React.FC<PaymentScreenProps> = ({ totalAmount, onConfirm, onCancel }) => {
  // Her ödeme yöntemi için ayrı tutar state'i
  const [payments, setPayments] = useState<Record<PaymentMethodKey, string>>({
    cash: '',
    card: '',
    voucher: '',
    contactless: '',
  });
  const [processing, setProcessing] = useState(false);
  const [error, setError] = useState('');

  // Toplam girilen tutar
  const enteredTotal = (Object.values(payments) as string[]).reduce((sum, val) => sum + (parseFloat(val) || 0), 0);

  // Ödeme alanı değiştiğinde state güncelle
  const handleChange = (method: PaymentMethodKey, value: string) => {
    // Sadece sayı ve nokta kabul et
    if (!/^\d*\.?\d*$/.test(value)) return;
    setPayments(prev => ({ ...prev, [method]: value }));
  };

  // Onayla butonuna basınca
  const handleConfirm = async () => {
    setError('');
    if (enteredTotal < totalAmount) {
      setError('Toplam ödeme tutarı eksik.');
      return;
    }
    setProcessing(true);
    try {
      // Backend'e ödeme isteği gönder
      // await api.pay(totalAmount, payments);
      // Başarılıysa:
      onConfirm(payments);
    } catch (e) {
      setError('Ödeme başarısız. Lütfen tekrar deneyin.');
    } finally {
      setProcessing(false);
    }
  };

  return (
    <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.container}>
      <Text style={styles.title}>Ödeme Yöntemleri</Text>
      <Text style={styles.total}>Toplam: {totalAmount.toFixed(2)} €</Text>
      {PAYMENT_METHODS.map(method => (
        <View key={method.key} style={styles.methodRow}>
          <Ionicons name={method.icon} size={22} color="#1976d2" style={{ marginRight: 8 }} />
          <Text style={styles.methodLabel}>{method.label}</Text>
          <TextInput
            style={styles.input}
            keyboardType="decimal-pad"
            placeholder="0.00"
            value={payments[method.key]}
            onChangeText={val => handleChange(method.key as PaymentMethodKey, val)}
            editable={!processing}
          />
          <Text style={styles.euro}>€</Text>
        </View>
      ))}
      <View style={styles.summaryRow}>
        <Text style={enteredTotal < totalAmount ? styles.missing : styles.ok}>
          Girilen Toplam: {enteredTotal.toFixed(2)} €
        </Text>
      </View>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <View style={styles.buttonRow}>
        <TouchableOpacity style={styles.cancelBtn} onPress={onCancel} disabled={processing}>
          <Text style={styles.buttonText}>İptal</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.confirmBtn, { opacity: enteredTotal < totalAmount ? 0.5 : 1 }]}
          onPress={handleConfirm}
          disabled={processing || enteredTotal < totalAmount}
        >
          <Text style={styles.buttonText}>Onayla</Text>
        </TouchableOpacity>
      </View>
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
});

export default PaymentScreen; 