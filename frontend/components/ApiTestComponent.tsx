import React, { useState } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  ScrollView,
  Alert,
  TextInput,
} from 'react-native';

import { apiClient } from '../services/api/config';

/**
 * API bağlantısını test etmek için basit component
 * Türkçe açıklama: Bu component API bağlantısını test etmek için kullanılır
 */
export const ApiTestComponent: React.FC = () => {
  const [testResults, setTestResults] = useState<string[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [echoData, setEchoData] = useState('{"test": "data"}');

  // Test sonuçlarını logla
  const addLog = (message: string) => {
    setTestResults((prev) => [...prev, `${new Date().toLocaleTimeString()}: ${message}`]);
  };

  // Ping testi
  const testPing = async () => {
    setIsLoading(true);
    try {
      addLog('Ping testi başlatılıyor...');
      const response = await apiClient.get('/test/ping');
      addLog(`✅ Ping başarılı: ${JSON.stringify(response)}`);
    } catch (error: any) {
      addLog(`❌ Ping hatası: ${error.message || 'Bilinmeyen hata'}`);
      console.error('Ping error:', error);
    } finally {
      setIsLoading(false);
    }
  };

  // Echo testi
  const testEcho = async () => {
    setIsLoading(true);
    try {
      addLog('Echo testi başlatılıyor...');
      const data = JSON.parse(echoData);
      const response = await apiClient.post('/test/echo', data);
      addLog(`✅ Echo başarılı: ${JSON.stringify(response)}`);
    } catch (error: any) {
      addLog(`❌ Echo hatası: ${error.message || 'Bilinmeyen hata'}`);
      console.error('Echo error:', error);
    } finally {
      setIsLoading(false);
    }
  };

  // Math testi
  const testMath = async () => {
    setIsLoading(true);
    try {
      addLog('Math testi başlatılıyor...');
      const response = await apiClient.get('/test/math/10/5');
      addLog(`✅ Math başarılı: ${JSON.stringify(response)}`);
    } catch (error: any) {
      addLog(`❌ Math hatası: ${error.message || 'Bilinmeyen hata'}`);
      console.error('Math error:', error);
    } finally {
      setIsLoading(false);
    }
  };

  // System info testi
  const testSystemInfo = async () => {
    setIsLoading(true);
    try {
      addLog('System info testi başlatılıyor...');
      const response = await apiClient.get('/test/system');
      addLog(`✅ System info başarılı: ${JSON.stringify(response)}`);
    } catch (error: any) {
      addLog(`❌ System info hatası: ${error.message || 'Bilinmeyen hata'}`);
      console.error('System info error:', error);
    } finally {
      setIsLoading(false);
    }
  };

  // Tüm testleri çalıştır
  const runAllTests = async () => {
    setIsLoading(true);
    setTestResults([]);
    addLog('=== TÜM TESTLER BAŞLATILIYOR ===');

    await testPing();
    await testEcho();
    await testMath();
    await testSystemInfo();

    addLog('=== TÜM TESTLER TAMAMLANDI ===');
    setIsLoading(false);
  };

  // Logları temizle
  const clearLogs = () => {
    setTestResults([]);
  };

  return (
    <ScrollView style={styles.container}>
      <Text style={styles.title}>🔌 API Bağlantı Testi</Text>

      {/* Test Butonları */}
      <View style={styles.buttonContainer}>
        <TouchableOpacity
          style={[styles.button, styles.primaryButton]}
          onPress={runAllTests}
          disabled={isLoading}>
          <Text style={styles.buttonText}>
            {isLoading ? '🔄 Testler Çalışıyor...' : '🚀 Tüm Testleri Çalıştır'}
          </Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.button, styles.secondaryButton]}
          onPress={testPing}
          disabled={isLoading}>
          <Text style={styles.buttonText}>🏓 Ping Testi</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.button, styles.secondaryButton]}
          onPress={testEcho}
          disabled={isLoading}>
          <Text style={styles.buttonText}>📢 Echo Testi</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.button, styles.secondaryButton]}
          onPress={testMath}
          disabled={isLoading}>
          <Text style={styles.buttonText}>🧮 Math Testi</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.button, styles.secondaryButton]}
          onPress={testSystemInfo}
          disabled={isLoading}>
          <Text style={styles.buttonText}>💻 System Info</Text>
        </TouchableOpacity>

        <TouchableOpacity style={[styles.button, styles.clearButton]} onPress={clearLogs}>
          <Text style={styles.buttonText}>🗑️ Logları Temizle</Text>
        </TouchableOpacity>
      </View>

      {/* Echo Data Input */}
      <View style={styles.inputContainer}>
        <Text style={styles.label}>Echo Test Verisi (JSON):</Text>
        <TextInput
          style={styles.input}
          value={echoData}
          onChangeText={setEchoData}
          placeholder='{"test": "data"}'
          multiline
        />
      </View>

      {/* Test Sonuçları */}
      <View style={styles.resultsContainer}>
        <Text style={styles.resultsTitle}>📋 Test Sonuçları:</Text>
        {testResults.length === 0 ? (
          <Text style={styles.noResults}>Henüz test çalıştırılmadı</Text>
        ) : (
          testResults.map((log, index) => (
            <Text key={index} style={styles.logEntry}>
              {log}
            </Text>
          ))
        )}
      </View>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    padding: 16,
    backgroundColor: '#f5f5f5',
  },
  title: {
    fontSize: 24,
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
  primaryButton: {
    backgroundColor: '#007AFF',
  },
  secondaryButton: {
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
  inputContainer: {
    marginBottom: 20,
  },
  label: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 8,
    color: '#333',
  },
  input: {
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 8,
    padding: 12,
    fontSize: 14,
    backgroundColor: 'white',
    minHeight: 60,
  },
  resultsContainer: {
    backgroundColor: 'white',
    padding: 16,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#ddd',
  },
  resultsTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 12,
    color: '#333',
  },
  noResults: {
    color: '#999',
    fontStyle: 'italic',
    textAlign: 'center',
    padding: 20,
  },
  logEntry: {
    fontSize: 12,
    fontFamily: 'monospace',
    marginBottom: 4,
    color: '#333',
    backgroundColor: '#f8f8f8',
    padding: 8,
    borderRadius: 4,
  },
});

export default ApiTestComponent;
