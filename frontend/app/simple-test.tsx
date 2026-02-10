import { View, Text, StyleSheet } from 'react-native';
import React from 'react';

console.log('🧪 SIMPLE TEST: Module loaded');

export default function SimpleTest() {
  console.log('🧪 SIMPLE TEST: Component rendering');

  return (
    <View style={styles.container}>
      <Text style={styles.text}>✅ SIMPLE TEST PAGE (FIXED)</Text>
      <Text style={styles.subtext}>If you see this, config is fixed and render is working!</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#f0f0f0',
  },
  text: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#007AFF',
    marginBottom: 16,
  },
  subtext: {
    fontSize: 16,
    color: '#666',
  },
});
