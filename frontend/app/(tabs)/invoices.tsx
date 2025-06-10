import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Alert,
  RefreshControl,
  ScrollView,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { useRouter } from 'expo-router';
import { Invoice, InvoiceFilter, invoiceService } from '../../services/api/invoiceService';
import { useSystem } from '../../contexts/SystemContext';
import InvoiceList from '../../components/InvoiceList';
import InvoiceFilterModal from '../../components/InvoiceFilterModal';
import InvoiceReport from '../../components/InvoiceReport';

const InvoicesScreen: React.FC = () => {
  const { t } = useTranslation();
  const router = useRouter();
  const { systemConfig } = useSystem();
  const [selectedInvoice, setSelectedInvoice] = useState<Invoice | null>(null);
  const [filters, setFilters] = useState<InvoiceFilter>({});
  const [showFilterModal, setShowFilterModal] = useState(false);
  const [showReport, setShowReport] = useState(false);
  const [refreshing, setRefreshing] = useState(false);

  const handleRefresh = async () => {
    setRefreshing(true);
    // Fatura listesini yenile
    setTimeout(() => setRefreshing(false), 1000);
  };

  const handleInvoiceSelect = (invoice: Invoice) => {
    setSelectedInvoice(invoice);
    // Fatura detay sayfasına git
    router.push(`/invoice/${invoice.id}` as any);
  };

  const handleCreateInvoice = () => {
    // Fatura oluşturma sayfasına git
    router.push('/invoice/create' as any);
  };

  const handleApplyFilters = (newFilters: InvoiceFilter) => {
    setFilters(newFilters);
  };

  const handleExportReport = (report: any) => {
    Alert.alert(
      t('invoice.report.export.title'),
      t('invoice.report.export.message'),
      [
        { text: t('common.cancel'), style: 'cancel' },
        {
          text: t('invoice.report.export.pdf'),
          onPress: () => {
            // PDF export işlemi
            console.log('Exporting PDF report...');
          }
        },
        {
          text: t('invoice.report.export.excel'),
          onPress: () => {
            // Excel export işlemi
            console.log('Exporting Excel report...');
          }
        }
      ]
    );
  };

  const getActiveFiltersCount = () => {
    return Object.keys(filters).filter(key => filters[key as keyof InvoiceFilter] !== undefined).length;
  };

  return (
    <View style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <View style={styles.headerLeft}>
          <Text style={styles.title}>{t('invoice.title')}</Text>
          <Text style={styles.subtitle}>
            {t('invoice.subtitle')} - {systemConfig.operationMode}
          </Text>
        </View>
        <View style={styles.headerRight}>
          <TouchableOpacity
            style={styles.iconButton}
            onPress={() => setShowFilterModal(true)}
          >
            <Ionicons name="filter-outline" size={24} color="#2196F3" />
            {getActiveFiltersCount() > 0 && (
              <View style={styles.filterBadge}>
                <Text style={styles.filterBadgeText}>{getActiveFiltersCount()}</Text>
              </View>
            )}
          </TouchableOpacity>
          
          <TouchableOpacity
            style={styles.iconButton}
            onPress={() => setShowReport(!showReport)}
          >
            <Ionicons 
              name={showReport ? "list-outline" : "bar-chart-outline"} 
              size={24} 
              color="#4CAF50" 
            />
          </TouchableOpacity>
        </View>
      </View>

      {/* Filtre Özeti */}
      {getActiveFiltersCount() > 0 && (
        <View style={styles.filterSummary}>
          <Text style={styles.filterSummaryText}>
            {t('invoice.filters.active')}: {getActiveFiltersCount()}
          </Text>
          <TouchableOpacity
            onPress={() => setFilters({})}
            style={styles.clearFiltersButton}
          >
            <Text style={styles.clearFiltersText}>{t('invoice.filters.clear')}</Text>
          </TouchableOpacity>
        </View>
      )}

      {/* İçerik */}
      {showReport ? (
        <ScrollView
          style={styles.content}
          refreshControl={
            <RefreshControl
              refreshing={refreshing}
              onRefresh={handleRefresh}
              colors={['#2196F3']}
            />
          }
        >
          <InvoiceReport
            filters={filters}
            onExport={handleExportReport}
          />
        </ScrollView>
      ) : (
        <InvoiceList
          onInvoiceSelect={handleInvoiceSelect}
          onRefresh={handleRefresh}
          filters={filters}
          showActions={true}
        />
      )}

      {/* Fatura Oluştur Butonu */}
      <TouchableOpacity
        style={styles.fab}
        onPress={handleCreateInvoice}
      >
        <Ionicons name="add" size={24} color="white" />
      </TouchableOpacity>

      {/* Filtre Modal */}
      <InvoiceFilterModal
        visible={showFilterModal}
        onClose={() => setShowFilterModal(false)}
        onApplyFilters={handleApplyFilters}
        currentFilters={filters}
      />
    </View>
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
  headerLeft: {
    flex: 1,
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 14,
    color: '#666',
  },
  headerRight: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 16,
  },
  iconButton: {
    position: 'relative',
    padding: 8,
  },
  filterBadge: {
    position: 'absolute',
    top: 4,
    right: 4,
    backgroundColor: '#F44336',
    borderRadius: 10,
    minWidth: 20,
    height: 20,
    alignItems: 'center',
    justifyContent: 'center',
  },
  filterBadgeText: {
    color: 'white',
    fontSize: 12,
    fontWeight: '600',
  },
  filterSummary: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 20,
    paddingVertical: 12,
    backgroundColor: '#E3F2FD',
    borderBottomWidth: 1,
    borderBottomColor: '#BBDEFB',
  },
  filterSummaryText: {
    fontSize: 14,
    color: '#1976D2',
    fontWeight: '500',
  },
  clearFiltersButton: {
    paddingHorizontal: 12,
    paddingVertical: 4,
    backgroundColor: '#1976D2',
    borderRadius: 12,
  },
  clearFiltersText: {
    color: 'white',
    fontSize: 12,
    fontWeight: '600',
  },
  content: {
    flex: 1,
  },
  fab: {
    position: 'absolute',
    bottom: 24,
    right: 24,
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: '#2196F3',
    alignItems: 'center',
    justifyContent: 'center',
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 4,
    },
    shadowOpacity: 0.3,
    shadowRadius: 8,
    elevation: 8,
  },
});

export default InvoicesScreen; 