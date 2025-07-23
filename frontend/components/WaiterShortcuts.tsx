// Türkçe Açıklama: Garsonların hızlıca ödeme ve fatura oluşturabilmesi için kısayol butonları. Tek dokunuşla yaygın işlemler yapılabilir.

import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

type WaiterShortcutsProps = {
  totalAmount: number;
  onQuickAction: (action: string, amount?: number) => void;
};

const WaiterShortcuts: React.FC<WaiterShortcutsProps> = ({
  totalAmount,
  onQuickAction
}) => {
  const shortcuts = [
    {
      key: 'full_cash',
      label: 'Tam Nakit',
      icon: 'cash' as const,
      color: '#27ae60',
      action: () => onQuickAction('full_cash', totalAmount)
    },
    {
      key: 'full_card',
      label: 'Tam Kart',
      icon: 'card' as const,
      color: '#1976d2',
      action: () => onQuickAction('full_card', totalAmount)
    },
    {
      key: 'split_half',
      label: 'Yarı-Yarı',
      icon: 'git-branch' as const,
      color: '#f39c12',
      action: () => onQuickAction('split_half', totalAmount / 2)
    },
    {
      key: 'add_tip_10',
      label: '+%10 Bahşiş',
      icon: 'add-circle' as const,
      color: '#9b59b6',
      action: () => onQuickAction('add_tip', totalAmount * 0.1)
    },
    {
      key: 'print_receipt',
      label: 'Fiş Yazdır',
      icon: 'print' as const,
      color: '#34495e',
      action: () => onQuickAction('print_receipt')
    },
    {
      key: 'email_receipt',
      label: 'E-posta Gönder',
      icon: 'mail' as const,
      color: '#e67e22',
      action: () => onQuickAction('email_receipt')
    }
  ];

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Garson Kısayolları</Text>
      <View style={styles.grid}>
        {shortcuts.map((shortcut) => (
          <TouchableOpacity
            key={shortcut.key}
            style={[styles.shortcutButton, { backgroundColor: shortcut.color }]}
            onPress={shortcut.action}
          >
            <Ionicons name={shortcut.icon} size={24} color="#fff" />
            <Text style={styles.shortcutLabel}>{shortcut.label}</Text>
          </TouchableOpacity>
        ))}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: '#f8f9fa',
    borderRadius: 12,
    padding: 16,
    margin: 8
  },
  title: {
    fontSize: 16,
    fontWeight: 'bold',
    marginBottom: 12,
    textAlign: 'center'
  },
  grid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-between'
  },
  shortcutButton: {
    width: '48%',
    height: 60,
    borderRadius: 8,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 8,
    elevation: 2
  },
  shortcutLabel: {
    color: '#fff',
    fontSize: 12,
    fontWeight: 'bold',
    marginTop: 4,
    textAlign: 'center'
  }
});

export default WaiterShortcuts; 