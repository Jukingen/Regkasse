import React, { useState } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Alert,
} from 'react-native';
import { apiClient } from '../services/api/config';

/**
 * Basit API Test Component'i
 * Türkçe açıklama: API bağlantısını test etmek için basit component
 */
export const SimpleApiTest: React.FC = () => {
  const [isLoading, setIsLoading] = useState(false);
  const [result, setResult] = useState<string>('');

  const testHealth = async () => {
    setIsLoading(true);
    setResult('');
    
    try {
      console.log('🧪 Testing health endpoint...');
      const response = await apiClient.get('/health');
      console.log('✅ Health test successful:', response);
      setResult(`✅ Başarılı: ${JSON.stringify(response, null, 2)}`);
    } catch (error: any) {
      console.error('❌ Health test failed:', error);
      setResult(`❌ Hata: ${error.message || 'Bilinmeyen hata'}`);
      
      // Detaylı hata bilgisi
      if (error.code === 'ECONNABORTED') {
        Alert.alert('Timeout Hatası', 'API isteği zaman aşımına uğradı. Backend çalışıyor mu?');
      } else if (error.status === 404) {
        Alert.alert('404 Hatası', 'Endpoint bulunamadı. URL doğru mu?');
      } else if (error.status === 0) {
        Alert.alert('Bağlantı Hatası', 'Backend\'e bağlanılamıyor. Backend çalışıyor mu?');
      }
    } finally {
      setIsLoading(false);
    }
  };

  const testPing = async () => {
    setIsLoading(true);
    setResult('');
    
    try {
      console.log('🏓 Testing ping endpoint...');
      const response = await apiClient.get('/test/ping');
      console.log('✅ Ping test successful:', response);
      setResult(`✅ Ping başarılı: ${JSON.stringify(response, null, 2)}`);
    } catch (error: any) {
      console.error('❌ Ping test failed:', error);
      setResult(`❌ Ping hatası: ${error.message || 'Bilinmeyen hata'}`);
    } finally {
      setIsLoading(false);
    }
  };

  const clearResult = () => {
    setResult('');
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>🔌 Basit API Test</Text>
      
      <View style={styles.buttonContainer}>
        <TouchableOpacity
          style={[styles.button, styles.healthButton]}
          onPress={testHealth}
          disabled={isLoading}
        >
          <Text style={styles.buttonText}>
            {isLoading ? '🔄 Test Ediliyor...' : '🏥 Health Test'}
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.button, styles.pingButton]}
          onPress={testPing}
          disabled={isLoading}
        >
          <Text style={styles.buttonText}>
            {isLoading ? '🔄 Test Ediliyor...' : '🏓 Ping Test'}
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.button, styles.clearButton]}
          onPress={clearResult}
        >
          <Text style={styles.buttonText}>🗑️ Temizle</Text>
        </TouchableOpacity>
      </View>

      {result ? (
        <View style={styles.resultContainer}>
          <Text style={styles.resultTitle}>📋 Test Sonucu:</Text>
          <Text style={styles.resultText}>{result}</Text>
        </View>
      ) : (
        <View style={styles.noResultContainer}>
          <Text style={styles.noResultText}>
            Test yapmak için yukarıdaki butonlardan birini kullanın
          </Text>
        </View>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 16,
    backgroundColor: '#f5f5f5',
  },
  title: {
    fontSize: 20,
    fontWeight: 'bold',
    textAlign: 'center',
    marginBottom: 20,
    color: '#333',
  },
  buttonContainer: {
    gap: 12,
    marginBottom: 20,
  },
  button: {
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
    justifyContent: 'center',
  },
  healthButton: {
    backgroundColor: '#007AFF',
  },
  pingButton: {
    backgroundColor: '#34C759',
  },
  clearButton: {
    backgroundColor: '#FF3B30',
  },
  buttonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: '600',
  },
  resultContainer: {
    backgroundColor: 'white',
    padding: 16,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#ddd',
    flex: 1,
  },
  resultTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 12,
    color: '#333',
  },
  resultText: {
    fontSize: 12,
    fontFamily: 'monospace',
    color: '#333',
    backgroundColor: '#f8f8f8',
    padding: 8,
    borderRadius: 4,
    flex: 1,
  },
  noResultContainer: {
    backgroundColor: 'white',
    padding: 20,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#ddd',
    alignItems: 'center',
    justifyContent: 'center',
    flex: 1,
  },
  noResultText: {
    color: '#999',
    fontStyle: 'italic',
    textAlign: 'center',
  },
});

export default SimpleApiTest;
