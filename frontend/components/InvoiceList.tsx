import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  RefreshControl,
  Alert,
  ActivityIndicator,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { Invoice, InvoiceFilter, invoiceService } from '../services/api/invoiceService';
import { useSystem } from '../contexts/SystemContext';

interface InvoiceListProps {
  onInvoiceSelect: (invoice: Invoice) => void;
  onRefresh?: () => void;
  filters?: InvoiceFilter;
  showActions?: boolean;
}

const InvoiceList: React.FC<InvoiceListProps> = ({
  onInvoiceSelect,
  onRefresh,
  filters,
  showActions = true,
}) => {
  const { t } = useTranslation();
  const { systemConfig } = useSystem();
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [hasMore, setHasMore] = useState(true);
  const [offset, setOffset] = useState(0);
  const [error, setError] = useState<string | null>(null);

  const loadInvoices = async (reset = false) => {
    try {
      setError(null);
      const newOffset = reset ? 0 : offset;
      const result = await invoiceService.getInvoices(filters, 20, newOffset);
      
      if (reset) {
        setInvoices(result.invoices);
      } else {
        setInvoices([...invoices, ...result.invoices]);
      }
      
      setHasMore(result.hasMore);
      setOffset(newOffset + result.invoices.length);
    } catch (err) {
      console.error('Failed to load invoices:', err);
      setError(t('invoice.error.load_failed'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  };

  useEffect(() => {
    loadInvoices(true);
  }, [filters]);

  const handleRefresh = async () => {
    setRefreshing(true);
    setOffset(0);
    await loadInvoices(true);
    onRefresh?.();
  };

  const handleLoadMore = () => {
    if (!loading && hasMore) {
      loadInvoices();
    }
  };

  const handleInvoiceAction = async (invoice: Invoice, action: string) => {
    try {
      switch (action) {
        case 'print':
          await invoiceService.printInvoice(invoice);
          Alert.alert(t('invoice.success.title'), t('invoice.success.printed'));
          break;
        case 'email':
          if (invoice.customerEmail) {
            await invoiceService.emailInvoice(invoice.id, invoice.customerEmail);
            Alert.alert(t('invoice.success.title'), t('invoice.success.emailed'));
          } else {
            Alert.alert(t('invoice.error.title'), t('invoice.error.no_email'));
          }
          break;
        case 'duplicate':
          const duplicatedInvoice = await invoiceService.duplicateInvoice(invoice.id);
          Alert.alert(t('invoice.success.title'), t('invoice.success.duplicated'));
          break;
        case 'delete':
          Alert.alert(
            t('invoice.confirm.delete.title'),
            t('invoice.confirm.delete.message'),
            [
              { text: t('common.cancel'), style: 'cancel' },
              {
                text: t('common.delete'),
                style: 'destructive',
                onPress: async () => {
                  await invoiceService.deleteInvoice(invoice.id);
                  handleRefresh();
                }
              }
            ]
          );
          break;
      }
    } catch (error) {
      console.error('Invoice action failed:', error);
      Alert.alert(t('invoice.error.title'), t('invoice.error.action_failed'));
    }
  };

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'paid': return '#4CAF50';
      case 'pending': return '#FF9800';
      case 'overdue': return '#F44336';
      case 'cancelled': return '#9E9E9E';
      default: return '#666';
    }
  };

  const getPaymentMethodIcon = (method: string) => {
    switch (method) {
      case 'cash': return 'cash-outline';
      case 'card': return 'card-outline';
      case 'voucher': return 'gift-outline';
      case 'mixed': return 'swap-horizontal-outline';
      default: return 'card-outline';
    }
  };

  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString();
  };

  const renderInvoiceItem = ({ item }: { item: Invoice }) => (
    <TouchableOpacity
      style={styles.invoiceItem}
      onPress={() => onInvoiceSelect(item)}
    >
      <View style={styles.invoiceHeader}>
        <View style={styles.invoiceInfo}>
          <Text style={styles.invoiceNumber}>{item.invoiceNumber}</Text>
          <Text style={styles.invoiceDate}>{formatDate(item.issueDate)}</Text>
        </View>
        <View style={styles.invoiceStatus}>
          <View style={[styles.statusBadge, { backgroundColor: getStatusColor(item.invoiceStatus) }]}>
            <Text style={styles.statusText}>{t(`invoice.status.${item.invoiceStatus}`)}</Text>
          </View>
        </View>
      </View>

      <View style={styles.invoiceDetails}>
        <View style={styles.customerInfo}>
          <Text style={styles.customerName}>
            {item.customerName || t('invoice.no_customer')}
          </Text>
          {item.customerEmail && (
            <Text style={styles.customerEmail}>{item.customerEmail}</Text>
          )}
        </View>
        
        <View style={styles.paymentInfo}>
          <View style={styles.paymentMethod}>
            <Ionicons 
              name={getPaymentMethodIcon(item.paymentMethod)} 
              size={16} 
              color="#666" 
            />
            <Text style={styles.paymentText}>
              {t(`payment.${item.paymentMethod}`)}
            </Text>
          </View>
          <Text style={styles.totalAmount}>{item.totalAmount.toFixed(2)}€</Text>
        </View>
      </View>

      {showActions && (
        <View style={styles.invoiceActions}>
          <TouchableOpacity
            style={styles.actionButton}
            onPress={() => handleInvoiceAction(item, 'print')}
          >
            <Ionicons name="print-outline" size={16} color="#2196F3" />
            <Text style={styles.actionText}>{t('invoice.print')}</Text>
          </TouchableOpacity>
          
          {item.customerEmail && (
            <TouchableOpacity
              style={styles.actionButton}
              onPress={() => handleInvoiceAction(item, 'email')}
            >
              <Ionicons name="mail-outline" size={16} color="#4CAF50" />
              <Text style={styles.actionText}>{t('invoice.email')}</Text>
            </TouchableOpacity>
          )}
          
          <TouchableOpacity
            style={styles.actionButton}
            onPress={() => handleInvoiceAction(item, 'duplicate')}
          >
            <Ionicons name="copy-outline" size={16} color="#FF9800" />
            <Text style={styles.actionText}>{t('invoice.duplicate')}</Text>
          </TouchableOpacity>
          
          <TouchableOpacity
            style={styles.actionButton}
            onPress={() => handleInvoiceAction(item, 'delete')}
          >
            <Ionicons name="trash-outline" size={16} color="#F44336" />
            <Text style={styles.actionText}>{t('invoice.delete')}</Text>
          </TouchableOpacity>
        </View>
      )}

      {/* Offline indicator */}
      {systemConfig.operationMode === 'offline-only' && (
        <View style={styles.offlineIndicator}>
          <Ionicons name="cloud-offline-outline" size={12} color="#FF9800" />
          <Text style={styles.offlineText}>{t('common.offline')}</Text>
        </View>
      )}
    </TouchableOpacity>
  );

  const renderEmptyState = () => (
    <View style={styles.emptyState}>
      <Ionicons name="document-outline" size={64} color="#ccc" />
      <Text style={styles.emptyTitle}>{t('invoice.empty.title')}</Text>
      <Text style={styles.emptyMessage}>{t('invoice.empty.message')}</Text>
    </View>
  );

  const renderErrorState = () => (
    <View style={styles.errorState}>
      <Ionicons name="alert-circle-outline" size={64} color="#F44336" />
      <Text style={styles.errorTitle}>{t('invoice.error.title')}</Text>
      <Text style={styles.errorMessage}>{error}</Text>
      <TouchableOpacity style={styles.retryButton} onPress={handleRefresh}>
        <Text style={styles.retryButtonText}>{t('common.retry')}</Text>
      </TouchableOpacity>
    </View>
  );

  if (loading && invoices.length === 0) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#2196F3" />
        <Text style={styles.loadingText}>{t('common.loading')}</Text>
      </View>
    );
  }

  if (error && invoices.length === 0) {
    return renderErrorState();
  }

  return (
    <FlatList
      data={invoices}
      renderItem={renderInvoiceItem}
      keyExtractor={(item) => item.id}
      style={styles.container}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={handleRefresh}
          colors={['#2196F3']}
        />
      }
      onEndReached={handleLoadMore}
      onEndReachedThreshold={0.1}
      ListEmptyComponent={renderEmptyState}
      ListFooterComponent={
        loading && invoices.length > 0 ? (
          <View style={styles.loadingFooter}>
            <ActivityIndicator size="small" color="#2196F3" />
            <Text style={styles.loadingFooterText}>{t('common.loading_more')}</Text>
          </View>
        ) : null
      }
    />
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  invoiceItem: {
    backgroundColor: 'white',
    marginHorizontal: 16,
    marginVertical: 8,
    borderRadius: 12,
    padding: 16,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 2,
    },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  invoiceHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 12,
  },
  invoiceInfo: {
    flex: 1,
  },
  invoiceNumber: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 4,
  },
  invoiceDate: {
    fontSize: 14,
    color: '#666',
  },
  invoiceStatus: {
    alignItems: 'flex-end',
  },
  statusBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  statusText: {
    fontSize: 12,
    fontWeight: '600',
    color: 'white',
  },
  invoiceDetails: {
    marginBottom: 12,
  },
  customerInfo: {
    marginBottom: 8,
  },
  customerName: {
    fontSize: 14,
    fontWeight: '500',
    marginBottom: 2,
  },
  customerEmail: {
    fontSize: 12,
    color: '#666',
  },
  paymentInfo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  paymentMethod: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  paymentText: {
    fontSize: 12,
    color: '#666',
  },
  totalAmount: {
    fontSize: 18,
    fontWeight: '700',
    color: '#2196F3',
  },
  invoiceActions: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    paddingTop: 12,
    borderTopWidth: 1,
    borderTopColor: '#f0f0f0',
  },
  actionButton: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 8,
    gap: 4,
  },
  actionText: {
    fontSize: 12,
    color: '#666',
  },
  offlineIndicator: {
    position: 'absolute',
    top: 8,
    right: 8,
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#FFF3E0',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 8,
    gap: 2,
  },
  offlineText: {
    fontSize: 10,
    color: '#FF9800',
    fontWeight: '500',
  },
  emptyState: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
  },
  emptyTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#666',
    marginTop: 16,
    marginBottom: 8,
  },
  emptyMessage: {
    fontSize: 14,
    color: '#999',
    textAlign: 'center',
  },
  errorState: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 32,
  },
  errorTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#F44336',
    marginTop: 16,
    marginBottom: 8,
  },
  errorMessage: {
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
    marginBottom: 16,
  },
  retryButton: {
    backgroundColor: '#2196F3',
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
  },
  retryButtonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: '600',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 16,
    fontSize: 16,
    color: '#666',
  },
  loadingFooter: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    padding: 16,
    gap: 8,
  },
  loadingFooterText: {
    fontSize: 14,
    color: '#666',
  },
});

export default InvoiceList; 