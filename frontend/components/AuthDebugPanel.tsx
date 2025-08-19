// Bu component, auth durumunu debug etmek ve otomatik logout sorununu izlemek için kullanılır
// Sadece development modunda görünür

import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import { useAuth } from '../contexts/AuthContext';

export const AuthDebugPanel: React.FC = () => {
  const { user, isAuthenticated, isLoading } = useAuth();
  const [renderCount, setRenderCount] = useState(0);
  const [lastRender, setLastRender] = useState(Date.now());

  // Her render'da sayacı artır
  useEffect(() => {
    setRenderCount(prev => prev + 1);
    setLastRender(Date.now());
  });

  // Sadece development modunda göster
  if (__DEV__ === false) {
    return null;
  }

  const handleForceLogout = () => {
    Alert.alert(
      'Force Logout',
      'Bu işlem kullanıcıyı zorla logout yapacak. Devam etmek istiyor musunuz?',
      [
        { text: 'İptal', style: 'cancel' },
        { 
          text: 'Evet', 
          style: 'destructive',
          onPress: () => {
            // AsyncStorage'ı temizle
            const clearStorage = async () => {
              try {
                const { AsyncStorage } = await import('@react-native-async-storage/async-storage');
                await AsyncStorage.multiRemove(['token', 'refreshToken', 'user', 'tokenExpiry']);
                console.log('🔴 Force logout: Storage cleared');
              } catch (error) {
                console.error('Force logout storage clear failed:', error);
              }
            };
            clearStorage();
          }
        }
      ]
    );
  };

  const handleCheckAuth = () => {
    Alert.alert(
      'Auth Status',
      `User: ${user ? 'Logged In' : 'Not Logged In'}
Email: ${user?.email || 'N/A'}
Role: ${user?.role || 'N/A'}
Token: ${user?.token ? 'Present' : 'Missing'}
Render Count: ${renderCount}
Last Render: ${new Date(lastRender).toLocaleTimeString()}`,
      [{ text: 'Tamam' }]
    );
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>🔍 Auth Debug Panel</Text>
      
      <View style={styles.infoSection}>
        <Text style={styles.infoText}>
          Status: {isLoading ? '🔄 Loading' : isAuthenticated ? '✅ Authenticated' : '❌ Not Authenticated'}
        </Text>
        <Text style={styles.infoText}>
          User: {user ? user.email : 'None'}
        </Text>
        <Text style={styles.infoText}>
          Role: {user?.role || 'None'}
        </Text>
        <Text style={styles.infoText}>
          Render Count: {renderCount}
        </Text>
        <Text style={styles.infoText}>
          Last Render: {new Date(lastRender).toLocaleTimeString()}
        </Text>
      </View>

      <View style={styles.buttonSection}>
        <TouchableOpacity style={styles.button} onPress={handleCheckAuth}>
          <Ionicons name="information-circle" size={16} color="white" />
          <Text style={styles.buttonText}>Auth Status</Text>
        </TouchableOpacity>
        
        <TouchableOpacity style={styles.dangerButton} onPress={handleForceLogout}>
          <Ionicons name="log-out" size={16} color="white" />
          <Text style={styles.buttonText}>Force Logout</Text>
        </TouchableOpacity>
      </View>

      <View style={styles.warningSection}>
        <Text style={styles.warningText}>
          ⚠️ Bu panel sadece development modunda görünür
        </Text>
        <Text style={styles.warningText}>
          Otomatik logout sorununu izlemek için render count'u takip edin
        </Text>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: '#f0f0f0',
    padding: 16,
    margin: 16,
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#ddd',
  },
  title: {
    fontSize: 16,
    fontWeight: 'bold',
    textAlign: 'center',
    marginBottom: 12,
    color: '#333',
  },
  infoSection: {
    marginBottom: 16,
  },
  infoText: {
    fontSize: 12,
    color: '#666',
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
    backgroundColor: '#FFF3CD',
    padding: 8,
    borderRadius: 4,
    borderLeftWidth: 3,
    borderLeftColor: '#FFC107',
  },
  warningText: {
    fontSize: 10,
    color: '#856404',
    marginBottom: 2,
  },
});

export default AuthDebugPanel;
