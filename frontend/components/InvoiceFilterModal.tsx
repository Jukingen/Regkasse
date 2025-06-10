import React, { useState } from 'react';
import {
  Modal,
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  TextInput,
  ScrollView,
  Switch,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { InvoiceFilter } from '../services/api/invoiceService';

interface InvoiceFilterModalProps {
  visible: boolean;
  onClose: () => void;
  onApplyFilters: (filters: InvoiceFilter) => void;
  currentFilters?: InvoiceFilter;
}

const InvoiceFilterModal: React.FC<InvoiceFilterModalProps> = ({
  visible,
  onClose,
  onApplyFilters,
  currentFilters,
}) => {
  const { t } = useTranslation();
  const [filters, setFilters] = useState<InvoiceFilter>(currentFilters || {});

  const handleApply = () => {
    onApplyFilters(filters);
    onClose();
  };

  const handleReset = () => {
    const emptyFilters: InvoiceFilter = {};
    setFilters(emptyFilters);
    onApplyFilters(emptyFilters);
    onClose();
  };

  const updateFilter = (key: keyof InvoiceFilter, value: any) => {
    setFilters(prev => ({
      ...prev,
      [key]: value
    }));
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent={true}
      onRequestClose={onClose}
    >
      <View style={styles.overlay}>
        <View style={styles.modal}>
          <View style={styles.header}>
            <Text style={styles.title}>{t('invoice.filter.title')}</Text>
            <TouchableOpacity onPress={onClose} style={styles.closeButton}>
              <Ionicons name="close" size={24} color="#666" />
            </TouchableOpacity>
          </View>

          <ScrollView style={styles.content}>
            {/* Tarih Filtreleri */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>{t('invoice.filter.date_range')}</Text>
              
              <View style={styles.dateRow}>
                <Text style={styles.label}>{t('invoice.filter.start_date')}</Text>
                <TextInput
                  style={styles.dateInput}
                  value={filters.startDate || ''}
                  onChangeText={(text) => updateFilter('startDate', text)}
                  placeholder="YYYY-MM-DD"
                />
              </View>
              
              <View style={styles.dateRow}>
                <Text style={styles.label}>{t('invoice.filter.end_date')}</Text>
                <TextInput
                  style={styles.dateInput}
                  value={filters.endDate || ''}
                  onChangeText={(text) => updateFilter('endDate', text)}
                  placeholder="YYYY-MM-DD"
                />
              </View>
            </View>

            {/* Arama */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>{t('invoice.filter.search')}</Text>
              <TextInput
                style={styles.searchInput}
                value={filters.searchQuery || ''}
                onChangeText={(text) => updateFilter('searchQuery', text)}
                placeholder={t('invoice.filter.search_placeholder')}
              />
            </View>

            {/* Durum Filtreleri */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>{t('invoice.filter.status')}</Text>
              
              <View style={styles.filterRow}>
                <Text style={styles.filterLabel}>{t('invoice.status.paid')}</Text>
                <Switch
                  value={filters.invoiceStatus === 'paid'}
                  onValueChange={(value) => updateFilter('invoiceStatus', value ? 'paid' : undefined)}
                />
              </View>
              
              <View style={styles.filterRow}>
                <Text style={styles.filterLabel}>{t('invoice.status.pending')}</Text>
                <Switch
                  value={filters.invoiceStatus === 'pending'}
                  onValueChange={(value) => updateFilter('invoiceStatus', value ? 'pending' : undefined)}
                />
              </View>
              
              <View style={styles.filterRow}>
                <Text style={styles.filterLabel}>{t('invoice.status.overdue')}</Text>
                <Switch
                  value={filters.invoiceStatus === 'overdue'}
                  onValueChange={(value) => updateFilter('invoiceStatus', value ? 'overdue' : undefined)}
                />
              </View>
              
              <View style={styles.filterRow}>
                <Text style={styles.filterLabel}>{t('invoice.status.cancelled')}</Text>
                <Switch
                  value={filters.invoiceStatus === 'cancelled'}
                  onValueChange={(value) => updateFilter('invoiceStatus', value ? 'cancelled' : undefined)}
                />
              </View>
            </View>

            {/* Ödeme Yöntemi */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>{t('invoice.filter.payment_method')}</Text>
              
              <View style={styles.filterRow}>
                <Text style={styles.filterLabel}>{t('payment.cash')}</Text>
                <Switch
                  value={filters.paymentMethod === 'cash'}
                  onValueChange={(value) => updateFilter('paymentMethod', value ? 'cash' : undefined)}
                />
              </View>
              
              <View style={styles.filterRow}>
                <Text style={styles.filterLabel}>{t('payment.card')}</Text>
                <Switch
                  value={filters.paymentMethod === 'card'}
                  onValueChange={(value) => updateFilter('paymentMethod', value ? 'card' : undefined)}
                />
              </View>
              
              <View style={styles.filterRow}>
                <Text style={styles.filterLabel}>{t('payment.voucher')}</Text>
                <Switch
                  value={filters.paymentMethod === 'voucher'}
                  onValueChange={(value) => updateFilter('paymentMethod', value ? 'voucher' : undefined)}
                />
              </View>
              
              <View style={styles.filterRow}>
                <Text style={styles.filterLabel}>{t('payment.mixed')}</Text>
                <Switch
                  value={filters.paymentMethod === 'mixed'}
                  onValueChange={(value) => updateFilter('paymentMethod', value ? 'mixed' : undefined)}
                />
              </View>
            </View>

            {/* Tutar Aralığı */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>{t('invoice.filter.amount_range')}</Text>
              
              <View style={styles.amountRow}>
                <Text style={styles.label}>{t('invoice.filter.min_amount')}</Text>
                <TextInput
                  style={styles.amountInput}
                  value={filters.minAmount?.toString() || ''}
                  onChangeText={(text) => updateFilter('minAmount', text ? parseFloat(text) : undefined)}
                  keyboardType="numeric"
                  placeholder="0.00"
                />
              </View>
              
              <View style={styles.amountRow}>
                <Text style={styles.label}>{t('invoice.filter.max_amount')}</Text>
                <TextInput
                  style={styles.amountInput}
                  value={filters.maxAmount?.toString() || ''}
                  onChangeText={(text) => updateFilter('maxAmount', text ? parseFloat(text) : undefined)}
                  keyboardType="numeric"
                  placeholder="999999.99"
                />
              </View>
            </View>

            {/* Hızlı Filtreler */}
            <View style={styles.section}>
              <Text style={styles.sectionTitle}>{t('invoice.filter.quick_filters')}</Text>
              
              <View style={styles.quickFilters}>
                <TouchableOpacity
                  style={styles.quickFilterButton}
                  onPress={() => {
                    const today = new Date().toISOString().split('T')[0];
                    updateFilter('startDate', today);
                    updateFilter('endDate', today);
                  }}
                >
                  <Text style={styles.quickFilterText}>{t('invoice.filter.today')}</Text>
                </TouchableOpacity>
                
                <TouchableOpacity
                  style={styles.quickFilterButton}
                  onPress={() => {
                    const today = new Date();
                    const weekAgo = new Date(today.getTime() - 7 * 24 * 60 * 60 * 1000);
                    updateFilter('startDate', weekAgo.toISOString().split('T')[0]);
                    updateFilter('endDate', today.toISOString().split('T')[0]);
                  }}
                >
                  <Text style={styles.quickFilterText}>{t('invoice.filter.last_week')}</Text>
                </TouchableOpacity>
                
                <TouchableOpacity
                  style={styles.quickFilterButton}
                  onPress={() => {
                    const today = new Date();
                    const monthAgo = new Date(today.getTime() - 30 * 24 * 60 * 60 * 1000);
                    updateFilter('startDate', monthAgo.toISOString().split('T')[0]);
                    updateFilter('endDate', today.toISOString().split('T')[0]);
                  }}
                >
                  <Text style={styles.quickFilterText}>{t('invoice.filter.last_month')}</Text>
                </TouchableOpacity>
                
                <TouchableOpacity
                  style={styles.quickFilterButton}
                  onPress={() => {
                    updateFilter('invoiceStatus', 'overdue');
                  }}
                >
                  <Text style={styles.quickFilterText}>{t('invoice.filter.overdue')}</Text>
                </TouchableOpacity>
              </View>
            </View>
          </ScrollView>

          {/* Butonlar */}
          <View style={styles.footer}>
            <TouchableOpacity style={styles.resetButton} onPress={handleReset}>
              <Text style={styles.resetButtonText}>{t('invoice.filter.reset')}</Text>
            </TouchableOpacity>
            
            <TouchableOpacity style={styles.applyButton} onPress={handleApply}>
              <Text style={styles.applyButtonText}>{t('invoice.filter.apply')}</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  modal: {
    backgroundColor: 'white',
    borderRadius: 16,
    width: '95%',
    height: '90%',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  title: {
    fontSize: 20,
    fontWeight: '600',
  },
  closeButton: {
    padding: 4,
  },
  content: {
    flex: 1,
    padding: 20,
  },
  section: {
    marginBottom: 24,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 12,
  },
  dateRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 12,
  },
  label: {
    fontSize: 14,
    fontWeight: '500',
    flex: 1,
  },
  dateInput: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    width: 120,
  },
  searchInput: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
  },
  filterRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
  },
  filterLabel: {
    fontSize: 14,
    color: '#666',
  },
  amountRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 12,
  },
  amountInput: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    width: 100,
    textAlign: 'center',
  },
  quickFilters: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  quickFilterButton: {
    backgroundColor: '#f0f0f0',
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  quickFilterText: {
    fontSize: 14,
    color: '#666',
  },
  footer: {
    flexDirection: 'row',
    padding: 20,
    borderTopWidth: 1,
    borderTopColor: '#e0e0e0',
    gap: 12,
  },
  resetButton: {
    flex: 1,
    padding: 16,
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 8,
    alignItems: 'center',
  },
  resetButtonText: {
    fontSize: 16,
    color: '#666',
  },
  applyButton: {
    flex: 2,
    backgroundColor: '#2196F3',
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
  },
  applyButtonText: {
    fontSize: 16,
    fontWeight: '600',
    color: 'white',
  },
});

export default InvoiceFilterModal; 