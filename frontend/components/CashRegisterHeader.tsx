// Türkçe Açıklama: Cash register header'ı için ayrı component
// Karmaşık cash-register.tsx dosyasından header logic'ini ayırır

import React from 'react';
import { View, Text, StyleSheet } from 'react-native';

interface CashRegisterHeaderProps {
  selectedTable: number;
  recoveryLoading: boolean;
}

export const CashRegisterHeader: React.FC<CashRegisterHeaderProps> = ({
  selectedTable,
  recoveryLoading,
}) => {
  return (
    <View style={styles.header}>
      <Text style={styles.headerTitle}>Cash Register</Text>
      <Text style={styles.headerSubtitle}>Table Management & Payments</Text>
      {selectedTable && (
        <Text style={styles.activeTableInfo}>Active Table: {selectedTable}</Text>
      )}
      {recoveryLoading && (
        <Text style={styles.recoveryLoadingText}>🔄 Loading table orders...</Text>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  header: {
    backgroundColor: '#2196F3',
    padding: 20,
    alignItems: 'center',
  },
  headerTitle: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#fff',
    marginBottom: 5,
  },
  headerSubtitle: {
    fontSize: 16,
    color: '#fff',
    opacity: 0.9,
  },
  activeTableInfo: {
    fontSize: 16,
    color: '#fff',
    marginTop: 5,
    opacity: 0.9,
  },
  recoveryLoadingText: {
    fontSize: 12,
    color: '#2196F3',
    fontStyle: 'italic',
    marginTop: 5,
  },
});
