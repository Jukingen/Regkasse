import React from 'react';
import { View, StyleSheet } from 'react-native';
import ApiTestComponent from '../components/ApiTestComponent';

/**
 * API Test Sayfası
 * Türkçe açıklama: Bu sayfa API bağlantısını test etmek için kullanılır
 */
export default function ApiTestPage() {
  return (
    <View style={styles.container}>
      <ApiTestComponent />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
});
