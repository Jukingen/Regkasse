import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Alert,
  ActivityIndicator,
} from 'react-native';

import { Colors, Spacing, BorderRadius } from '../constants/Colors';
import { checkTseStatus, TseStatus } from '../services/api/tseService';

interface TseStatusIndicatorProps {
  onStatusChange?: (status: TseStatus) => void;
  showDetails?: boolean;
  autoRefresh?: boolean;
}

export const TseStatusIndicator: React.FC<TseStatusIndicatorProps> = ({
  onStatusChange,
  showDetails = false,
  autoRefresh = true,
}) => {
  const { t } = useTranslation();
  const [tseStatus, setTseStatus] = useState<TseStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    checkTseStatusAndUpdate();
    
    // OPTIMIZATION: autoRefresh true ise sadece 2 dakikada bir kontrol et (30 saniye yerine)
    if (autoRefresh) {
      const interval = setInterval(checkTseStatusAndUpdate, 2 * 60 * 1000); // 2 dakika
      return () => clearInterval(interval);
    }
  }, [autoRefresh]);

  const checkTseStatusAndUpdate = async () => {
    try {
      setLoading(true);
      setError(null);
      const status = await checkTseStatus();
      setTseStatus(status);
      onStatusChange?.(status);
    } catch (error) {
      console.error('TSE status check failed:', error);
      setError('TSE durumu kontrol edilemiyor');
      const fallbackStatus: TseStatus = {
        isConnected: false,
        serialNumber: '',
        certificateStatus: 'UNKNOWN',
        memoryStatus: 'UNKNOWN',
        lastSignatureTime: '',
        canCreateInvoices: false,
        errorMessage: 'TSE durumu kontrol edilemiyor',
      };
      setTseStatus(fallbackStatus);
      onStatusChange?.(fallbackStatus);
    } finally {
      setLoading(false);
    }
  };

  const handleRefresh = () => {
    checkTseStatusAndUpdate();
  };

  const handleShowDetails = () => {
    if (!tseStatus) return;

    Alert.alert(
      t('tse.deviceStatus', 'TSE Device Status'),
      t('tse.deviceStatusDetails',
        `Connection: ${tseStatus.isConnected ? t('tse.connected', 'Connected') : t('tse.disconnected', 'Disconnected')}
Serial Number: ${tseStatus.serialNumber || t('tse.na', 'N/A')}
Certificate: ${tseStatus.certificateStatus}
Memory: ${tseStatus.memoryStatus}
Last Signature: ${tseStatus.lastSignatureTime ? new Date(tseStatus.lastSignatureTime).toLocaleString() : t('tse.na', 'N/A')}
Can Create Invoices: ${tseStatus.canCreateInvoices ? t('tse.yes', 'Yes') : t('tse.no', 'No')}
${tseStatus.errorMessage ? `${t('tse.error', 'Error')}: ${tseStatus.errorMessage}` : ''}`),
      [
        { text: t('common.refresh', 'Refresh'), onPress: handleRefresh },
        { text: t('common.ok', 'OK'), style: 'default' },
      ]
    );
  };

  const getStatusColor = () => {
    if (!tseStatus) return Colors.light.error;
    
    if (tseStatus.canCreateInvoices) return Colors.light.success;
    if (tseStatus.isConnected) return Colors.light.warning;
    return Colors.light.error;
  };

  const getStatusIcon = () => {
    if (!tseStatus) return 'hardware-chip-outline';
    
    if (tseStatus.canCreateInvoices) return 'checkmark-circle';
    if (tseStatus.isConnected) return 'warning';
    return 'close-circle';
  };

  const getStatusText = () => {
    if (!tseStatus) return t('tse.unknown', 'TSE Unknown');
    
    if (tseStatus.canCreateInvoices) return t('tse.ready', 'TSE Ready');
    if (tseStatus.isConnected) return t('tse.warning', 'TSE Warning');
    return t('tse.errorStatus', 'TSE Error');
  };

  if (loading) {
    return (
      <View style={styles.container}>
        <ActivityIndicator size="small" color={Colors.light.primary} />
        <Text style={styles.loadingText}>{t('tse.checking', 'Checking TSE...')}</Text>
      </View>
    );
  }

  return (
    <TouchableOpacity
      style={[styles.container, { borderColor: getStatusColor() }]}
      onPress={handleShowDetails}
      disabled={!showDetails}
    >
      <View style={styles.statusContainer}>
        <Ionicons
          name={getStatusIcon() as any}
          size={20}
          color={getStatusColor()}
        />
        <Text style={[styles.statusText, { color: getStatusColor() }]}>
          {getStatusText()}
        </Text>
      </View>
      {/* Sadece bir kez göster */}
      {error && (!tseStatus?.errorMessage || error !== tseStatus.errorMessage) && (
        <Text style={styles.errorText}>{error}</Text>
      )}
      {tseStatus?.errorMessage && (
        <Text style={styles.errorText}>{tseStatus.errorMessage}</Text>
      )}
      
      {showDetails && (
        <View style={styles.detailsContainer}>
          <Text style={styles.detailText}>{t('tse.serial', 'Serial')}: {tseStatus?.serialNumber || t('tse.na', 'N/A')}</Text>
          <Text style={styles.detailText}>{t('tse.cert', 'Cert')}: {tseStatus?.certificateStatus || t('tse.na', 'N/A')}</Text>
          <Text style={styles.detailText}>{t('tse.memory', 'Memory')}: {tseStatus?.memoryStatus || t('tse.na', 'N/A')}</Text>
        </View>
      )}
    </TouchableOpacity>
  );
};

const styles = StyleSheet.create({
  container: {
    padding: Spacing.sm,
    borderWidth: 1,
    borderRadius: BorderRadius.sm,
    backgroundColor: Colors.light.background,
    marginVertical: Spacing.xs,
  },
  statusContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.xs,
  },
  statusText: {
    fontSize: 14,
    fontWeight: '600',
  },
  loadingText: {
    fontSize: 12,
    color: Colors.light.textSecondary,
    marginLeft: Spacing.xs,
  },
  errorText: {
    fontSize: 12,
    color: Colors.light.error,
    marginTop: Spacing.xs,
  },
  detailsContainer: {
    marginTop: Spacing.xs,
    paddingTop: Spacing.xs,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  detailText: {
    fontSize: 11,
    color: Colors.light.textSecondary,
    marginBottom: 2,
  },
}); 