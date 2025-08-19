import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, TouchableOpacity, Alert, ActivityIndicator } from 'react-native';

import { networkService, NetworkStatus } from '../services/api/networkService';
import { pendingInvoicesService, PendingInvoicesResponse } from '../services/api/pendingInvoicesService';

interface PendingInvoicesIndicatorProps {
  showDetails?: boolean;
  onStatusChange?: (pendingCount: number) => void;
}

export const PendingInvoicesIndicator: React.FC<PendingInvoicesIndicatorProps> = ({
  showDetails = false,
  onStatusChange
}) => {
  const [pendingData, setPendingData] = useState<PendingInvoicesResponse | null>(null);
  const [networkStatus, setNetworkStatus] = useState<NetworkStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    checkPendingInvoices();
    checkNetworkStatus();
    
    // OPTIMIZATION: Periyodik kontrolü daha az sıklıkta yap
    const interval = setInterval(() => {
      checkPendingInvoices();
      checkNetworkStatus();
    }, 5 * 60 * 1000); // 5 dakika

    return () => clearInterval(interval);
  }, []);

  const checkPendingInvoices = async () => {
    try {
      setLoading(true);
      const data = await pendingInvoicesService.getPendingInvoices();
      setPendingData(data);
      onStatusChange?.(data.pendingCount);
    } catch (error) {
      console.error('Pending invoices check failed:', error);
    } finally {
      setLoading(false);
    }
  };

  const checkNetworkStatus = async () => {
    try {
      const status = await networkService.getNetworkStatus();
      setNetworkStatus(status);
    } catch (error) {
      console.error('Network status check failed:', error);
    }
  };

  const handleSubmitAll = async () => {
    if (!networkStatus?.canSubmitToFinanzOnline) {
      Alert.alert(
        'Bağlantı Hatası',
        'FinanzOnline bağlantısı yok. Faturalar gönderilemez.',
        [{ text: 'Tamam', style: 'default' }]
      );
      return;
    }

    Alert.alert(
      'Bekleyen Faturaları Gönder',
      `${pendingData?.pendingCount || 0} adet bekleyen fatura FinanzOnline'a gönderilecek. Devam etmek istiyor musunuz?`,
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Gönder',
          style: 'default',
          onPress: async () => {
            try {
              setSubmitting(true);
              await pendingInvoicesService.submitPendingInvoices();
              Alert.alert('Başarılı', 'Bekleyen faturalar gönderildi');
              await checkPendingInvoices();
            } catch (error) {
              Alert.alert('Hata', 'Faturalar gönderilemedi');
            } finally {
              setSubmitting(false);
            }
          }
        }
      ]
    );
  };

  const handleRetryInvoice = async (invoiceId: string, invoiceNumber: string) => {
    if (!networkStatus?.canSubmitToFinanzOnline) {
      Alert.alert(
        'Bağlantı Hatası',
        'FinanzOnline bağlantısı yok. Fatura gönderilemez.',
        [{ text: 'Tamam', style: 'default' }]
      );
      return;
    }

    try {
      setSubmitting(true);
      await pendingInvoicesService.retryInvoice(invoiceId);
      Alert.alert('Başarılı', `Fatura ${invoiceNumber} gönderildi`);
      await checkPendingInvoices();
    } catch (error) {
      Alert.alert('Hata', 'Fatura gönderilemedi');
    } finally {
      setSubmitting(false);
    }
  };

  const getStatusColor = () => {
    if (!pendingData || pendingData.pendingCount === 0) return '#4CAF50'; // Yeşil
    if (networkStatus?.canSubmitToFinanzOnline) return '#FF9800'; // Turuncu
    return '#F44336'; // Kırmızı
  };

  const getStatusIcon = () => {
    if (!pendingData || pendingData.pendingCount === 0) return '✅';
    if (networkStatus?.canSubmitToFinanzOnline) return '⚠️';
    return '❌';
  };

  if (loading && !pendingData) {
    return (
      <View style={styles.container}>
        <ActivityIndicator size="small" color="#666" />
        <Text style={styles.loadingText}>Bekleyen faturalar kontrol ediliyor...</Text>
      </View>
    );
  }

  if (!pendingData || pendingData.pendingCount === 0) {
    return null; // Bekleyen fatura yoksa gösterme
  }

  return (
    <View style={styles.container}>
      <View style={styles.headerRow}>
        <Text style={styles.statusIcon}>{getStatusIcon()}</Text>
        <Text style={[styles.statusText, { color: getStatusColor() }]}>
          {pendingData.pendingCount} adet bekleyen fatura
        </Text>
        {networkStatus?.canSubmitToFinanzOnline && (
          <TouchableOpacity
            style={styles.submitButton}
            onPress={handleSubmitAll}
            disabled={submitting}
          >
            {submitting ? (
              <ActivityIndicator size="small" color="white" />
            ) : (
              <Text style={styles.submitButtonText}>Gönder</Text>
            )}
          </TouchableOpacity>
        )}
      </View>

      {showDetails && (
        <View style={styles.detailsContainer}>
          <Text style={styles.detailsTitle}>Bekleyen Faturalar:</Text>
          {pendingData.invoices.slice(0, 5).map((invoice) => (
            <View key={invoice.id} style={styles.invoiceRow}>
              <View style={styles.invoiceInfo}>
                <Text style={styles.invoiceNumber}>{invoice.invoiceNumber}</Text>
                <Text style={styles.invoiceDate}>
                  {new Date(invoice.invoiceDate).toLocaleDateString('tr-TR')}
                </Text>
                <Text style={styles.invoiceAmount}>
                  {invoice.totalAmount.toFixed(2)} €
                </Text>
              </View>
              {networkStatus?.canSubmitToFinanzOnline && (
                <TouchableOpacity
                  style={styles.retryButton}
                  onPress={() => handleRetryInvoice(invoice.id, invoice.invoiceNumber)}
                  disabled={submitting}
                >
                  <Text style={styles.retryButtonText}>Yeniden Dene</Text>
                </TouchableOpacity>
              )}
            </View>
          ))}
          {pendingData.invoices.length > 5 && (
            <Text style={styles.moreText}>
              ... ve {pendingData.invoices.length - 5} fatura daha
            </Text>
          )}
        </View>
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    padding: 10,
    backgroundColor: '#fff3cd',
    borderRadius: 8,
    margin: 5,
    borderWidth: 1,
    borderColor: '#ffeaa7',
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  statusIcon: {
    fontSize: 16,
    marginRight: 8,
  },
  statusText: {
    fontSize: 14,
    fontWeight: '500',
    flex: 1,
  },
  submitButton: {
    backgroundColor: '#007AFF',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 4,
  },
  submitButtonText: {
    color: 'white',
    fontSize: 12,
    fontWeight: '500',
  },
  loadingText: {
    fontSize: 14,
    color: '#666',
    fontStyle: 'italic',
    marginLeft: 8,
  },
  detailsContainer: {
    marginTop: 10,
    paddingTop: 10,
    borderTopWidth: 1,
    borderTopColor: '#ffeaa7',
  },
  detailsTitle: {
    fontSize: 12,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 8,
  },
  invoiceRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 4,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  invoiceInfo: {
    flex: 1,
  },
  invoiceNumber: {
    fontSize: 12,
    fontWeight: '500',
    color: '#333',
  },
  invoiceDate: {
    fontSize: 11,
    color: '#666',
  },
  invoiceAmount: {
    fontSize: 11,
    fontWeight: '500',
    color: '#007AFF',
  },
  retryButton: {
    backgroundColor: '#28a745',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 3,
  },
  retryButtonText: {
    color: 'white',
    fontSize: 10,
    fontWeight: '500',
  },
  moreText: {
    fontSize: 11,
    color: '#666',
    fontStyle: 'italic',
    textAlign: 'center',
    marginTop: 4,
  },
}); 