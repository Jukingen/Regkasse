import React from 'react';
import { View, StyleSheet } from 'react-native';
import SimpleApiTest from '../components/SimpleApiTest';

/**
 * Basit API Test Sayfası
 * Türkçe açıklama: Bu sayfa basit API testleri için kullanılır
 */
export default function SimpleTestPage() {
  return (
    <View style={styles.container}>
      <SimpleApiTest />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
});
