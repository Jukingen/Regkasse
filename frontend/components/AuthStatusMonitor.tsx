// Bu component, auth durumunu gerçek zamanlı izlemek ve otomatik logout sorununu tespit etmek için kullanılır
// Sadece development modunda görünür

import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, ScrollView } from 'react-native';
import { useAuth } from '../contexts/AuthContext';

interface AuthLog {
  timestamp: Date;
  action: string;
  details: string;
  userState: any;
}

export const AuthStatusMonitor: React.FC = () => {
  const { user, isAuthenticated, isLoading } = useAuth();
  const [authLogs, setAuthLogs] = useState<AuthLog[]>([]);
  const [renderCount, setRenderCount] = useState(0);

  // Her render'da sayacı artır
  useEffect(() => {
    setRenderCount(prev => prev + 1);
  });

  // Auth durumu değişikliklerini izle
  useEffect(() => {
    const log = {
      timestamp: new Date(),
      action: 'Auth State Changed',
      details: `isAuthenticated: ${isAuthenticated}, hasUser: ${!!user}, isLoading: ${isLoading}`,
      userState: user ? { id: user.id, email: user.email, role: user.role } : null
    };

    setAuthLogs(prev => [log, ...prev.slice(0, 19)]); // Son 20 log'u tut
  }, [isAuthenticated, user, isLoading]);

  // Sadece development modunda göster
  if (__DEV__ === false) {
    return null;
  }

  const getStatusColor = () => {
    if (isLoading) return '#FFA500'; // Orange
    if (isAuthenticated && user) return '#4CAF50'; // Green
    return '#F44336'; // Red
  };

  const getStatusText = () => {
    if (isLoading) return '🔄 Loading';
    if (isAuthenticated && user) return '✅ Authenticated';
    return '❌ Not Authenticated';
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>🔍 Auth Status Monitor</Text>
      
      {/* Current Status */}
      <View style={[styles.statusSection, { borderLeftColor: getStatusColor() }]}>
        <Text style={styles.statusTitle}>Current Status</Text>
        <Text style={styles.statusText}>{getStatusText()}</Text>
        <Text style={styles.statusText}>User: {user ? user.email : 'None'}</Text>
        <Text style={styles.statusText}>Role: {user?.role || 'None'}</Text>
        <Text style={styles.statusText}>Render Count: {renderCount}</Text>
        <Text style={styles.statusText}>Last Update: {new Date().toLocaleTimeString()}</Text>
      </View>

      {/* Auth Logs */}
      <View style={styles.logsSection}>
        <Text style={styles.logsTitle}>Auth Logs (Last 20)</Text>
        <ScrollView style={styles.logsContainer} showsVerticalScrollIndicator={false}>
          {authLogs.map((log, index) => (
            <View key={index} style={styles.logItem}>
              <Text style={styles.logTimestamp}>
                {log.timestamp.toLocaleTimeString()}
              </Text>
              <Text style={styles.logAction}>{log.action}</Text>
              <Text style={styles.logDetails}>{log.details}</Text>
              {log.userState && (
                <Text style={styles.logUserState}>
                  User: {log.userState.email} ({log.userState.role})
                </Text>
              )}
            </View>
          ))}
        </ScrollView>
      </View>

      {/* Warning */}
      <View style={styles.warningSection}>
        <Text style={styles.warningText}>
          ⚠️ Bu monitor sadece development modunda görünür
        </Text>
        <Text style={styles.warningText}>
          Otomatik logout sorununu izlemek için auth logları takip edin
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
  statusSection: {
    backgroundColor: '#fff',
    padding: 12,
    borderRadius: 6,
    marginBottom: 16,
    borderLeftWidth: 4,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.1,
    shadowRadius: 2,
    elevation: 2,
  },
  statusTitle: {
    fontSize: 14,
    fontWeight: '600',
    marginBottom: 8,
    color: '#495057',
  },
  statusText: {
    fontSize: 12,
    color: '#6c757d',
    marginBottom: 4,
  },
  logsSection: {
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
  logsTitle: {
    fontSize: 14,
    fontWeight: '600',
    marginBottom: 8,
    color: '#495057',
  },
  logsContainer: {
    maxHeight: 200,
  },
  logItem: {
    backgroundColor: '#f8f9fa',
    padding: 8,
    borderRadius: 4,
    marginBottom: 6,
    borderLeftWidth: 3,
    borderLeftColor: '#007bff',
  },
  logTimestamp: {
    fontSize: 10,
    color: '#6c757d',
    fontWeight: '500',
  },
  logAction: {
    fontSize: 11,
    color: '#495057',
    fontWeight: '600',
    marginTop: 2,
  },
  logDetails: {
    fontSize: 10,
    color: '#6c757d',
    marginTop: 2,
  },
  logUserState: {
    fontSize: 10,
    color: '#28a745',
    marginTop: 2,
    fontStyle: 'italic',
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

export default AuthStatusMonitor;
