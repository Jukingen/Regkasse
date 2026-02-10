// Türkçe Açıklama: Sepet ürünlerini birden fazla kişiye/gruba bölebilen split bill (ayrılmış hesap) bölümü. Her bölüm için ödeme yöntemi ve tutar atanabilir.

import React, { useState } from 'react';
import { View, Text, TouchableOpacity, TextInput, StyleSheet, FlatList } from 'react-native';

type PaymentMethodKey = 'cash' | 'card' | 'voucher' | 'contactless';

type SplitEntry = {
  id: string;
  name: string;
  amount: string;
  method: PaymentMethodKey;
};

const PAYMENT_METHODS: { key: PaymentMethodKey; label: string }[] = [
  { key: 'cash', label: 'Nakit' },
  { key: 'card', label: 'Kredi Kartı' },
  { key: 'voucher', label: 'Kupon' },
  { key: 'contactless', label: 'Temassız' },
];

type SplitBillSectionProps = {
  totalAmount: number;
  onSplitChange: (splits: SplitEntry[]) => void;
};

const SplitBillSection: React.FC<SplitBillSectionProps> = ({ totalAmount, onSplitChange }) => {
  const [splits, setSplits] = useState<SplitEntry[]>([
    { id: '1', name: 'Kişi 1', amount: totalAmount.toFixed(2), method: 'cash' },
  ]);

  // Toplam girilen tutar
  const enteredTotal = splits.reduce((sum, s) => sum + (parseFloat(s.amount) || 0), 0);

  // Bölüm ekle
  const addSplit = () => {
    setSplits(prev => [
      ...prev,
      { id: (prev.length + 1).toString(), name: `Kişi ${prev.length + 1}`, amount: '', method: 'cash' },
    ]);
  };

  // Bölüm sil
  const removeSplit = (id: string) => {
    setSplits(prev => prev.length > 1 ? prev.filter(s => s.id !== id) : prev);
  };

  // Bölümde değişiklik
  const updateSplit = (id: string, field: keyof SplitEntry, value: string) => {
    setSplits(prev =>
      prev.map(s => (s.id === id ? { ...s, [field]: value } : s))
    );
  };

  // Her değişiklikte parent'a bildir
  React.useEffect(() => {
    onSplitChange(splits);
  }, [splits]);

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Hesabı Böl</Text>
      <FlatList
        data={splits}
        keyExtractor={item => item.id}
        renderItem={({ item }) => (
          <View style={styles.splitRow}>
            <TextInput
              style={styles.nameInput}
              value={item.name}
              onChangeText={val => updateSplit(item.id, 'name', val)}
            />
            <TextInput
              style={styles.amountInput}
              keyboardType="decimal-pad"
              placeholder="0.00"
              value={item.amount}
              onChangeText={val => updateSplit(item.id, 'amount', val.replace(',', '.'))}
            />
            <Text style={styles.euro}>€</Text>
            <View style={styles.methodBox}>
              {PAYMENT_METHODS.map(m => (
                <TouchableOpacity
                  key={m.key}
                  style={[
                    styles.methodBtn,
                    item.method === m.key && styles.methodBtnActive,
                  ]}
                  onPress={() => updateSplit(item.id, 'method', m.key)}
                >
                  <Text style={item.method === m.key ? styles.methodTextActive : styles.methodText}>
                    {m.label}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
            <TouchableOpacity onPress={() => removeSplit(item.id)} style={styles.removeBtn}>
              <Text style={{ color: '#d32f2f', fontWeight: 'bold' }}>✕</Text>
            </TouchableOpacity>
          </View>
        )}
        ListFooterComponent={
          <TouchableOpacity onPress={addSplit} style={styles.addBtn}>
            <Text style={styles.addBtnText}>+ Kişi Ekle</Text>
          </TouchableOpacity>
        }
      />
      <View style={styles.summaryRow}>
        <Text style={enteredTotal < totalAmount ? styles.missing : styles.ok}>
          Girilen Toplam: {enteredTotal.toFixed(2)} € / {totalAmount.toFixed(2)} €
        </Text>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: { backgroundColor: '#f7f7f7', borderRadius: 10, padding: 10, marginBottom: 8 },
  title: { fontSize: 15, fontWeight: 'bold', marginBottom: 6 },
  splitRow: { flexDirection: 'row', alignItems: 'center', marginBottom: 6 },
  nameInput: { width: 70, borderWidth: 1, borderColor: '#ccc', borderRadius: 6, padding: 4, fontSize: 13, marginRight: 4 },
  amountInput: { width: 60, borderWidth: 1, borderColor: '#ccc', borderRadius: 6, padding: 4, fontSize: 13, textAlign: 'right', marginRight: 2 },
  euro: { fontSize: 13, marginRight: 4 },
  methodBox: { flexDirection: 'row', marginRight: 4 },
  methodBtn: { backgroundColor: '#eee', borderRadius: 6, paddingHorizontal: 6, paddingVertical: 2, marginRight: 2 },
  methodBtnActive: { backgroundColor: '#1976d2' },
  methodText: { color: '#333', fontSize: 12 },
  methodTextActive: { color: '#fff', fontWeight: 'bold', fontSize: 12 },
  removeBtn: { marginLeft: 2, padding: 2 },
  addBtn: { marginTop: 4, alignSelf: 'flex-start', backgroundColor: '#27ae60', borderRadius: 6, paddingHorizontal: 10, paddingVertical: 4 },
  addBtnText: { color: '#fff', fontWeight: 'bold', fontSize: 13 },
  summaryRow: { marginTop: 6, alignItems: 'flex-end' },
  missing: { color: '#d32f2f', fontWeight: 'bold' },
  ok: { color: '#388e3c', fontWeight: 'bold' },
});

export default SplitBillSection; 