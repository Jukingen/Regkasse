import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, Alert } from 'react-native';

import { networkService, NetworkStatus } from '../services/api/networkService';

interface NetworkStatusIndicatorProps {
  showDetails?: boolean;
  onStatusChange?: (status: NetworkStatus) => void;
}

export const NetworkStatusIndicator: React.FC<NetworkStatusIndicatorProps> = ({
  showDetails = false,
  onStatusChange
}) => {
  const [networkStatus, setNetworkStatus] = useState<NetworkStatus | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    checkNetworkStatus();
    
    // OPTIMIZATION: Periyodik kontrolü daha az sıklıkta yap
    const interval = setInterval(checkNetworkStatus, 2 * 60 * 1000); // 2 dakika

    return () => clearInterval(interval);
  }, []);

  const checkNetworkStatus = async () => {
    try {
      setLoading(true);
      const status = await networkService.getNetworkStatus();
      setNetworkStatus(status);
      onStatusChange?.(status);
    } catch (error) {
      console.error('Network status check failed:', error);
      // Fallback durumu
      const fallbackStatus: NetworkStatus = {
        isInternetAvailable: false,
        isFinanzOnlineAvailable: false,
        lastChecked: new Date().toISOString(),
        status: 'DISCONNECTED',
        canProcessInvoices: false,
        canSubmitToFinanzOnline: false,
        recommendations: ['Bağlantı kontrolü başarısız']
      };
      setNetworkStatus(fallbackStatus);
      onStatusChange?.(fallbackStatus);
    } finally {
      setLoading(false);
    }
  };

  const getStatusColor = () => {
    if (!networkStatus) return '#999';
    
    switch (networkStatus.status) {
      case 'FULLY_CONNECTED':
        return '#4CAF50'; // Yeşil
      case 'INTERNET_ONLY':
        return '#FF9800'; // Turuncu
      case 'DISCONNECTED':
        return '#F44336'; // Kırmızı
      default:
        return '#999'; // Gri
    }
  };

  const getStatusIcon = () => {
    if (!networkStatus) return '❓';
    
    switch (networkStatus.status) {
      case 'FULLY_CONNECTED':
        return '✅';
      case 'INTERNET_ONLY':
        return '⚠️';
      case 'DISCONNECTED':
        return '❌';
      default:
        return '❓';
    }
  };

  const handleStatusPress = () => {
    if (networkStatus) {
      Alert.alert(
        'Network Durumu',
        networkService.getStatusMessage(networkStatus),
        [
          { text: 'Tamam', style: 'default' },
          { text: 'Yenile', onPress: checkNetworkStatus }
        ]
      );
    }
  };

  if (loading && !networkStatus) {
    return (
      <View style={styles.container}>
        <Text style={styles.loadingText}>Bağlantı kontrol ediliyor...</Text>
      </View>
    );
  }

  if (!networkStatus) {
    return null;
  }

  return (
    <View style={styles.container}>
      <View style={styles.statusRow}>
        <Text style={styles.statusIcon}>{getStatusIcon()}</Text>
        <Text 
          style={[styles.statusText, { color: getStatusColor() }]}
          onPress={handleStatusPress}
        >
          {networkService.getStatusMessage(networkStatus)}
        </Text>
      </View>

      {showDetails && (
        <View style={styles.detailsContainer}>
          <Text style={styles.detailText}>
            İnternet: {networkStatus.isInternetAvailable ? '✅' : '❌'}
          </Text>
          <Text style={styles.detailText}>
            FinanzOnline: {networkStatus.isFinanzOnlineAvailable ? '✅' : '❌'}
          </Text>
          <Text style={styles.detailText}>
            Fiş Kesme: {networkStatus.canProcessInvoices ? '✅' : '❌'}
          </Text>
          
          {networkStatus.recommendations.length > 0 && (
            <View style={styles.recommendationsContainer}>
              <Text style={styles.recommendationsTitle}>Öneriler:</Text>
              {networkStatus.recommendations.map((rec, index) => (
                <Text key={index} style={styles.recommendationText}>
                  • {rec}
                </Text>
              ))}
            </View>
          )}
        </View>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    padding: 10,
    backgroundColor: '#f5f5f5',
    borderRadius: 8,
    margin: 5,
  },
  statusRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  statusIcon: {
    fontSize: 16,
    marginRight: 8,
  },
  statusText: {
    fontSize: 14,
    fontWeight: '500',
  },
  loadingText: {
    fontSize: 14,
    color: '#666',
    fontStyle: 'italic',
  },
  detailsContainer: {
    marginTop: 10,
    paddingTop: 10,
    borderTopWidth: 1,
    borderTopColor: '#ddd',
  },
  detailText: {
    fontSize: 12,
    color: '#666',
    marginBottom: 2,
  },
  recommendationsContainer: {
    marginTop: 8,
  },
  recommendationsTitle: {
    fontSize: 12,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 4,
  },
  recommendationText: {
    fontSize: 11,
    color: '#666',
    marginBottom: 2,
    paddingLeft: 8,
  },
}); 