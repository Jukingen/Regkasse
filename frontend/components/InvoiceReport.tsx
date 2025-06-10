import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  ActivityIndicator,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { InvoiceReport as InvoiceReportType, InvoiceFilter, invoiceService } from '../services/api/invoiceService';

interface InvoiceReportProps {
  filters?: InvoiceFilter;
  onExport?: (report: InvoiceReportType) => void;
}

const InvoiceReport: React.FC<InvoiceReportProps> = ({
  filters,
  onExport,
}) => {
  const { t } = useTranslation();
  const [report, setReport] = useState<InvoiceReportType | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const loadReport = async () => {
    try {
      setLoading(true);
      setError(null);
      const reportData = await invoiceService.getInvoiceReport(filters || {});
      setReport(reportData);
    } catch (err) {
      console.error('Failed to load invoice report:', err);
      setError(t('invoice.report.error.load_failed'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadReport();
  }, [filters]);

  const handleExport = () => {
    if (report && onExport) {
      onExport(report);
    }
  };

  const formatCurrency = (amount: number) => {
    return `${amount.toFixed(2)}€`;
  };

  const formatPercentage = (value: number, total: number) => {
    if (total === 0) return '0%';
    return `${((value / total) * 100).toFixed(1)}%`;
  };

  if (loading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#2196F3" />
        <Text style={styles.loadingText}>{t('invoice.report.loading')}</Text>
      </View>
    );
  }

  if (error) {
    return (
      <View style={styles.errorContainer}>
        <Ionicons name="alert-circle-outline" size={64} color="#F44336" />
        <Text style={styles.errorTitle}>{t('invoice.report.error.title')}</Text>
        <Text style={styles.errorMessage}>{error}</Text>
        <TouchableOpacity style={styles.retryButton} onPress={loadReport}>
          <Text style={styles.retryButtonText}>{t('common.retry')}</Text>
        </TouchableOpacity>
      </View>
    );
  }

  if (!report) {
    return (
      <View style={styles.emptyContainer}>
        <Ionicons name="bar-chart-outline" size={64} color="#ccc" />
        <Text style={styles.emptyTitle}>{t('invoice.report.empty.title')}</Text>
        <Text style={styles.emptyMessage}>{t('invoice.report.empty.message')}</Text>
      </View>
    );
  }

  return (
    <ScrollView style={styles.container}>
      {/* Başlık ve Dışa Aktarma */}
      <View style={styles.header}>
        <View>
          <Text style={styles.title}>{t('invoice.report.title')}</Text>
          <Text style={styles.period}>{report.period}</Text>
        </View>
        <TouchableOpacity style={styles.exportButton} onPress={handleExport}>
          <Ionicons name="download-outline" size={20} color="white" />
          <Text style={styles.exportButtonText}>{t('invoice.report.export')}</Text>
        </TouchableOpacity>
      </View>

      {/* Genel İstatistikler */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{t('invoice.report.overview')}</Text>
        <View style={styles.statsGrid}>
          <View style={styles.statCard}>
            <Text style={styles.statValue}>{report.totalInvoices}</Text>
            <Text style={styles.statLabel}>{t('invoice.report.total_invoices')}</Text>
          </View>
          <View style={styles.statCard}>
            <Text style={styles.statValue}>{formatCurrency(report.totalAmount)}</Text>
            <Text style={styles.statLabel}>{t('invoice.report.total_amount')}</Text>
          </View>
          <View style={styles.statCard}>
            <Text style={styles.statValue}>{formatCurrency(report.paidAmount)}</Text>
            <Text style={styles.statLabel}>{t('invoice.report.paid_amount')}</Text>
          </View>
          <View style={styles.statCard}>
            <Text style={styles.statValue}>{formatCurrency(report.overdueAmount)}</Text>
            <Text style={styles.statLabel}>{t('invoice.report.overdue_amount')}</Text>
          </View>
          <View style={styles.statCard}>
            <Text style={styles.statValue}>{formatCurrency(report.averageInvoiceValue)}</Text>
            <Text style={styles.statLabel}>{t('invoice.report.average_value')}</Text>
          </View>
          <View style={styles.statCard}>
            <Text style={styles.statValue}>
              {formatPercentage(report.paidAmount, report.totalAmount)}
            </Text>
            <Text style={styles.statLabel}>{t('invoice.report.payment_rate')}</Text>
          </View>
        </View>
      </View>

      {/* Ödeme Yöntemi Dağılımı */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{t('invoice.report.payment_methods')}</Text>
        <View style={styles.paymentMethods}>
          {Object.entries(report.paymentMethodBreakdown).map(([method, amount]) => (
            <View key={method} style={styles.paymentMethodItem}>
              <View style={styles.paymentMethodInfo}>
                <Ionicons 
                  name={method === 'cash' ? 'cash-outline' : 
                        method === 'card' ? 'card-outline' : 
                        method === 'voucher' ? 'gift-outline' : 'swap-horizontal-outline'} 
                  size={20} 
                  color="#2196F3" 
                />
                <Text style={styles.paymentMethodName}>{t(`payment.${method}`)}</Text>
              </View>
              <View style={styles.paymentMethodAmount}>
                <Text style={styles.paymentAmount}>{formatCurrency(amount)}</Text>
                <Text style={styles.paymentPercentage}>
                  {formatPercentage(amount, report.totalAmount)}
                </Text>
              </View>
            </View>
          ))}
        </View>
      </View>

      {/* Durum Dağılımı */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>{t('invoice.report.status_breakdown')}</Text>
        <View style={styles.statusBreakdown}>
          {Object.entries(report.statusBreakdown).map(([status, count]) => (
            <View key={status} style={styles.statusItem}>
              <View style={styles.statusInfo}>
                <View style={[
                  styles.statusIndicator, 
                  { backgroundColor: status === 'paid' ? '#4CAF50' : 
                                   status === 'pending' ? '#FF9800' : 
                                   status === 'overdue' ? '#F44336' : '#9E9E9E' }
                ]} />
                <Text style={styles.statusName}>{t(`invoice.status.${status}`)}</Text>
              </View>
              <View style={styles.statusCount}>
                <Text style={styles.statusValue}>{count}</Text>
                <Text style={styles.statusPercentage}>
                  {formatPercentage(count, report.totalInvoices)}
                </Text>
              </View>
            </View>
          ))}
        </View>
      </View>

      {/* En İyi Müşteriler */}
      {report.topCustomers.length > 0 && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>{t('invoice.report.top_customers')}</Text>
          <View style={styles.topCustomers}>
            {report.topCustomers.slice(0, 5).map((customer, index) => (
              <View key={customer.customerId} style={styles.customerItem}>
                <View style={styles.customerRank}>
                  <Text style={styles.rankNumber}>{index + 1}</Text>
                </View>
                <View style={styles.customerInfo}>
                  <Text style={styles.customerName}>{customer.customerName}</Text>
                  <Text style={styles.customerDetails}>
                    {customer.invoiceCount} {t('invoice.report.invoices')}
                  </Text>
                </View>
                <View style={styles.customerAmount}>
                  <Text style={styles.customerTotal}>{formatCurrency(customer.totalAmount)}</Text>
                </View>
              </View>
            ))}
          </View>
        </View>
      )}

      {/* Günlük Dağılım */}
      {report.dailyBreakdown.length > 0 && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>{t('invoice.report.daily_breakdown')}</Text>
          <View style={styles.dailyBreakdown}>
            {report.dailyBreakdown.slice(-7).map((day) => (
              <View key={day.date} style={styles.dayItem}>
                <Text style={styles.dayDate}>{new Date(day.date).toLocaleDateString()}</Text>
                <View style={styles.dayStats}>
                  <Text style={styles.dayCount}>{day.invoiceCount}</Text>
                  <Text style={styles.dayAmount}>{formatCurrency(day.totalAmount)}</Text>
                </View>
              </View>
            ))}
          </View>
        </View>
      )}

      {/* Yenile Butonu */}
      <TouchableOpacity style={styles.refreshButton} onPress={loadReport}>
        <Ionicons name="refresh-outline" size={20} color="#2196F3" />
        <Text style={styles.refreshButtonText}>{t('invoice.report.refresh')}</Text>
      </TouchableOpacity>
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    backgroundColor: 'white',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 20,
    fontWeight: '600',
    marginBottom: 4,
  },
  period: {
    fontSize: 14,
    color: '#666',
  },
  exportButton: {
    backgroundColor: '#4CAF50',
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 8,
    gap: 8,
  },
  exportButtonText: {
    color: 'white',
    fontSize: 14,
    fontWeight: '600',
  },
  section: {
    margin: 16,
    backgroundColor: 'white',
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
  sectionTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 16,
  },
  statsGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 12,
  },
  statCard: {
    flex: 1,
    minWidth: '45%',
    backgroundColor: '#f8f9fa',
    borderRadius: 8,
    padding: 16,
    alignItems: 'center',
  },
  statValue: {
    fontSize: 18,
    fontWeight: '700',
    color: '#2196F3',
    marginBottom: 4,
  },
  statLabel: {
    fontSize: 12,
    color: '#666',
    textAlign: 'center',
  },
  paymentMethods: {
    gap: 12,
  },
  paymentMethodItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
  },
  paymentMethodInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  paymentMethodName: {
    fontSize: 14,
    fontWeight: '500',
  },
  paymentMethodAmount: {
    alignItems: 'flex-end',
  },
  paymentAmount: {
    fontSize: 14,
    fontWeight: '600',
    color: '#2196F3',
  },
  paymentPercentage: {
    fontSize: 12,
    color: '#666',
  },
  statusBreakdown: {
    gap: 12,
  },
  statusItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
  },
  statusInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  statusIndicator: {
    width: 12,
    height: 12,
    borderRadius: 6,
  },
  statusName: {
    fontSize: 14,
    fontWeight: '500',
  },
  statusCount: {
    alignItems: 'flex-end',
  },
  statusValue: {
    fontSize: 14,
    fontWeight: '600',
    color: '#2196F3',
  },
  statusPercentage: {
    fontSize: 12,
    color: '#666',
  },
  topCustomers: {
    gap: 12,
  },
  customerItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
  },
  customerRank: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: '#f0f0f0',
    alignItems: 'center',
    justifyContent: 'center',
    marginRight: 12,
  },
  rankNumber: {
    fontSize: 14,
    fontWeight: '600',
    color: '#666',
  },
  customerInfo: {
    flex: 1,
  },
  customerName: {
    fontSize: 14,
    fontWeight: '500',
    marginBottom: 2,
  },
  customerDetails: {
    fontSize: 12,
    color: '#666',
  },
  customerAmount: {
    alignItems: 'flex-end',
  },
  customerTotal: {
    fontSize: 14,
    fontWeight: '600',
    color: '#2196F3',
  },
  dailyBreakdown: {
    gap: 8,
  },
  dayItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  dayDate: {
    fontSize: 14,
    fontWeight: '500',
  },
  dayStats: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 16,
  },
  dayCount: {
    fontSize: 14,
    fontWeight: '600',
    color: '#666',
  },
  dayAmount: {
    fontSize: 14,
    fontWeight: '600',
    color: '#2196F3',
  },
  refreshButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 16,
    margin: 16,
    backgroundColor: 'white',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    gap: 8,
  },
  refreshButtonText: {
    fontSize: 16,
    color: '#2196F3',
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
  errorContainer: {
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
  emptyContainer: {
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
});

export default InvoiceReport; 