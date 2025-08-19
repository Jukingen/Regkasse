// Bu component, auth durumunu detaylı debug etmek için kullanılır
// Sadece development modunda görünür

import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import AsyncStorage from '@react-native-async-storage/async-storage';

import { useAuth } from '../contexts/AuthContext';

export const AuthDebugger: React.FC = () => {
  const { user, isAuthenticated, isLoading, checkAuthStatus } = useAuth();
  const [storageData, setStorageData] = useState<any>({});
  const [renderCount, setRenderCount] = useState(0);

  // Her render'da sayacı artır
  useEffect(() => {
    setRenderCount(prev => prev + 1);
  });

  // AsyncStorage'ı kontrol et
  const checkStorage = async () => {
    try {
      const token = await AsyncStorage.getItem('token');
      const refreshToken = await AsyncStorage.getItem('refreshToken');
      const storedUser = await AsyncStorage.getItem('user');
      
      setStorageData({
        token: token ? `${token.substring(0, 20)}...` : 'None',
        refreshToken: refreshToken ? `${refreshToken.substring(0, 20)}...` : 'None',
        storedUser: storedUser ? JSON.parse(storedUser) : 'None'
      });
    } catch (error) {
      console.error('Storage check failed:', error);
    }
  };

  // Component mount olduğunda storage'ı kontrol et
  useEffect(() => {
    checkStorage();
  }, []);

  // Auth durumu değiştiğinde storage'ı tekrar kontrol et
  useEffect(() => {
    checkStorage();
  }, [isAuthenticated, user]);

  // Sadece development modunda göster
  if (__DEV__ === false) {
    return null;
  }

  const handleManualAuthCheck = () => {
    Alert.alert(
      'Manual Auth Check',
      'checkAuthStatus fonksiyonunu manuel olarak çağırmak istiyor musunuz?',
      [
        { text: 'İptal', style: 'cancel' },
        { 
          text: 'Evet', 
          onPress: async () => {
            console.log('🔍 Manuel auth check başlatılıyor...');
            await checkAuthStatus();
            await checkStorage(); // Storage'ı güncelle
          }
        }
      ]
    );
  };

  const handleClearStorage = () => {
    Alert.alert(
      'Clear Storage',
      'AsyncStorage\'daki tüm auth verilerini temizlemek istiyor musunuz?',
      [
        { text: 'İptal', style: 'cancel' },
        { 
          text: 'Evet', 
          style: 'destructive',
          onPress: async () => {
            try {
              await AsyncStorage.multiRemove(['token', 'refreshToken', 'user', 'tokenExpiry']);
              console.log('🔴 Storage cleared');
              await checkStorage(); // Storage'ı güncelle
            } catch (error) {
              console.error('Storage clear failed:', error);
            }
          }
        }
      ]
    );
  };

  const handleCheckStorage = () => {
    checkStorage();
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>🔍 Auth Debugger</Text>
      
      {/* Current State */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Current State</Text>
        <Text style={styles.infoText}>
          isAuthenticated: {isAuthenticated ? '✅ True' : '❌ False'}
        </Text>
        <Text style={styles.infoText}>
          isLoading: {isLoading ? '🔄 True' : '⏸️ False'}
        </Text>
        <Text style={styles.infoText}>
          hasUser: {user ? '✅ True' : '❌ False'}
        </Text>
        <Text style={styles.infoText}>
          User Email: {user?.email || 'None'}
        </Text>
        <Text style={styles.infoText}>
          User Role: {user?.role || 'None'}
        </Text>
        <Text style={styles.infoText}>
          Render Count: {renderCount}
        </Text>
      </View>

      {/* Storage Data */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Storage Data</Text>
        <Text style={styles.infoText}>
          Token: {storageData.token || 'Loading...'}
        </Text>
        <Text style={styles.infoText}>
          Refresh Token: {storageData.refreshToken || 'Loading...'}
        </Text>
        <Text style={styles.infoText}>
          Stored User: {storageData.storedUser ? 'Present' : 'None'}
        </Text>
      </View>

      {/* Actions */}
      <View style={styles.buttonSection}>
        <TouchableOpacity style={styles.button} onPress={handleManualAuthCheck}>
          <Ionicons name="refresh" size={16} color="white" />
          <Text style={styles.buttonText}>Auth Check</Text>
        </TouchableOpacity>
        
        <TouchableOpacity style={styles.button} onPress={handleCheckStorage}>
          <Ionicons name="eye" size={16} color="white" />
          <Text style={styles.buttonText}>Check Storage</Text>
        </TouchableOpacity>
        
        <TouchableOpacity style={styles.dangerButton} onPress={handleClearStorage}>
          <Ionicons name="trash" size={16} color="white" />
          <Text style={styles.buttonText}>Clear Storage</Text>
        </TouchableOpacity>
      </View>

      {/* Warning */}
      <View style={styles.warningSection}>
        <Text style={styles.warningText}>
          ⚠️ Bu debugger sadece development modunda görünür
        </Text>
        <Text style={styles.warningText}>
          Auth durumunu ve storage verilerini izlemek için kullanın
        </Text>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: '#f8f9fa',
    padding: 16,
    margin: 16,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#dee2e6',
  },
  title: {
    fontSize: 18,
    fontWeight: 'bold',
    textAlign: 'center',
    marginBottom: 16,
    color: '#495057',
  },
  section: {
    backgroundColor: '#fff',
    padding: 12,
    borderRadius: 6,
    marginBottom: 16,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.1,
    shadowRadius: 2,
    elevation: 2,
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: '600',
    marginBottom: 8,
    color: '#495057',
  },
  infoText: {
    fontSize: 12,
    color: '#6c757d',
    marginBottom: 4,
  },
  buttonSection: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    marginBottom: 16,
  },
  button: {
    backgroundColor: '#007AFF',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 6,
    flexDirection: 'row',
    alignItems: 'center',
  },
  dangerButton: {
    backgroundColor: '#FF3B30',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 6,
    flexDirection: 'row',
    alignItems: 'center',
  },
  buttonText: {
    color: 'white',
    fontSize: 12,
    marginLeft: 4,
    fontWeight: '500',
  },
  warningSection: {
    backgroundColor: '#fff3cd',
    padding: 8,
    borderRadius: 4,
    borderLeftWidth: 3,
    borderLeftColor: '#ffc107',
  },
  warningText: {
    fontSize: 10,
    color: '#856404',
    marginBottom: 2,
  },
});

export default AuthDebugger;
