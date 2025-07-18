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
import { checkTseStatus, TseStatus } from '../services/api/invoiceService';

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
    
    if (autoRefresh) {
      const interval = setInterval(checkTseStatusAndUpdate, 30000); // 30 saniyede bir
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
      'TSE Device Status',
      `Connection: ${tseStatus.isConnected ? 'Connected' : 'Disconnected'}
Serial Number: ${tseStatus.serialNumber || 'N/A'}
Certificate: ${tseStatus.certificateStatus}
Memory: ${tseStatus.memoryStatus}
Last Signature: ${tseStatus.lastSignatureTime ? new Date(tseStatus.lastSignatureTime).toLocaleString() : 'N/A'}
Can Create Invoices: ${tseStatus.canCreateInvoices ? 'Yes' : 'No'}
${tseStatus.errorMessage ? `Error: ${tseStatus.errorMessage}` : ''}`,
      [
        { text: 'Refresh', onPress: handleRefresh },
        { text: 'OK', style: 'default' },
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
    if (!tseStatus) return 'TSE Unknown';
    
    if (tseStatus.canCreateInvoices) return 'TSE Ready';
    if (tseStatus.isConnected) return 'TSE Warning';
    return 'TSE Error';
  };

  if (loading) {
    return (
      <View style={styles.container}>
        <ActivityIndicator size="small" color={Colors.light.primary} />
        <Text style={styles.loadingText}>Checking TSE...</Text>
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
      
      {error && (
        <Text style={styles.errorText}>{error}</Text>
      )}
      
      {tseStatus?.errorMessage && (
        <Text style={styles.errorText}>{tseStatus.errorMessage}</Text>
      )}
      
      {showDetails && (
        <View style={styles.detailsContainer}>
          <Text style={styles.detailText}>
            Serial: {tseStatus?.serialNumber || 'N/A'}
          </Text>
          <Text style={styles.detailText}>
            Cert: {tseStatus?.certificateStatus || 'N/A'}
          </Text>
          <Text style={styles.detailText}>
            Memory: {tseStatus?.memoryStatus || 'N/A'}
          </Text>
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