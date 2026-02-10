// Türkçe Açıklama: Rapor filtreleme paneli - tarih, kategori, ürün ve diğer filtreleme seçenekleri için dinamik komponent.

import React, { useState, useEffect } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Modal, ScrollView, TextInput } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import DateTimePicker from '@react-native-community/datetimepicker';

export interface ReportFilter {
  startDate?: Date;
  endDate?: Date;
  categoryId?: string;
  productId?: string;
  userId?: string;
  paymentMethod?: string;
  minAmount?: number;
  maxAmount?: number;
  isActive?: boolean;
  searchTerm?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
}

interface ReportFilterPanelProps {
  filter: ReportFilter;
  onFilterChange: (filter: ReportFilter) => void;
  onApplyFilter: () => void;
  onResetFilter: () => void;
  categories?: Array<{ id: string; name: string }>;
  products?: Array<{ id: string; name: string }>;
  paymentMethods?: string[];
  showAdvancedFilters?: boolean;
}

const ReportFilterPanel: React.FC<ReportFilterPanelProps> = ({
  filter,
  onFilterChange,
  onApplyFilter,
  onResetFilter,
  categories = [],
  products = [],
  paymentMethods = ['Cash', 'Card', 'Voucher'],
  showAdvancedFilters = false
}) => {
  const { t } = useTranslation();
  const [showDatePicker, setShowDatePicker] = useState<'start' | 'end' | null>(null);
  const [showAdvancedModal, setShowAdvancedModal] = useState(false);

  const handleDateChange = (event: any, selectedDate?: Date) => {
    setShowDatePicker(null);
    if (selectedDate) {
      if (showDatePicker === 'start') {
        onFilterChange({ ...filter, startDate: selectedDate });
      } else if (showDatePicker === 'end') {
        onFilterChange({ ...filter, endDate: selectedDate });
      }
    }
  };

  const formatDate = (date?: Date) => {
    if (!date) return '';
    return date.toLocaleDateString('de-DE');
  };

  const getActiveFilterCount = () => {
    let count = 0;
    if (filter.startDate) count++;
    if (filter.endDate) count++;
    if (filter.categoryId) count++;
    if (filter.productId) count++;
    if (filter.paymentMethod) count++;
    if (filter.minAmount) count++;
    if (filter.maxAmount) count++;
    if (filter.searchTerm) count++;
    return count;
  };

  const resetFilter = () => {
    onResetFilter();
    setShowAdvancedModal(false);
  };

  return (
    <View style={styles.container}>
      {/* Ana Filtre Alanı */}
      <View style={styles.mainFilterArea}>
        {/* Tarih Filtreleri */}
        <View style={styles.dateSection}>
          <Text style={styles.sectionTitle}>{t('report.dateRange', 'Tarih Aralığı')}</Text>
          <View style={styles.dateRow}>
            <TouchableOpacity
              style={styles.dateButton}
              onPress={() => setShowDatePicker('start')}
            >
              <Ionicons name="calendar" size={16} color="#666" />
              <Text style={styles.dateButtonText}>
                {filter.startDate ? formatDate(filter.startDate) : t('report.startDate', 'Başlangıç')}
              </Text>
            </TouchableOpacity>
            <Text style={styles.dateSeparator}>-</Text>
            <TouchableOpacity
              style={styles.dateButton}
              onPress={() => setShowDatePicker('end')}
            >
              <Ionicons name="calendar" size={16} color="#666" />
              <Text style={styles.dateButtonText}>
                {filter.endDate ? formatDate(filter.endDate) : t('report.endDate', 'Bitiş')}
              </Text>
            </TouchableOpacity>
          </View>
        </View>

        {/* Hızlı Tarih Seçenekleri */}
        <View style={styles.quickDateSection}>
          <ScrollView horizontal showsHorizontalScrollIndicator={false}>
            <TouchableOpacity
              style={styles.quickDateButton}
              onPress={() => {
                const today = new Date();
                onFilterChange({ ...filter, startDate: today, endDate: today });
              }}
            >
              <Text style={styles.quickDateButtonText}>{t('report.today', 'Bugün')}</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.quickDateButton}
              onPress={() => {
                const yesterday = new Date();
                yesterday.setDate(yesterday.getDate() - 1);
                onFilterChange({ ...filter, startDate: yesterday, endDate: yesterday });
              }}
            >
              <Text style={styles.quickDateButtonText}>{t('report.yesterday', 'Dün')}</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.quickDateButton}
              onPress={() => {
                const weekAgo = new Date();
                weekAgo.setDate(weekAgo.getDate() - 7);
                onFilterChange({ ...filter, startDate: weekAgo, endDate: new Date() });
              }}
            >
              <Text style={styles.quickDateButtonText}>{t('report.lastWeek', 'Son 7 Gün')}</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.quickDateButton}
              onPress={() => {
                const monthAgo = new Date();
                monthAgo.setMonth(monthAgo.getMonth() - 1);
                onFilterChange({ ...filter, startDate: monthAgo, endDate: new Date() });
              }}
            >
              <Text style={styles.quickDateButtonText}>{t('report.lastMonth', 'Son 30 Gün')}</Text>
            </TouchableOpacity>
          </ScrollView>
        </View>

        {/* Arama */}
        <View style={styles.searchSection}>
          <View style={styles.searchContainer}>
            <Ionicons name="search" size={16} color="#666" style={styles.searchIcon} />
            <TextInput
              style={styles.searchInput}
              placeholder={t('report.searchPlaceholder', 'Ara...')}
              value={filter.searchTerm}
              onChangeText={(text) => onFilterChange({ ...filter, searchTerm: text })}
            />
            {filter.searchTerm && (
              <TouchableOpacity
                onPress={() => onFilterChange({ ...filter, searchTerm: '' })}
                style={styles.clearButton}
              >
                <Ionicons name="close-circle" size={16} color="#666" />
              </TouchableOpacity>
            )}
          </View>
        </View>

        {/* Filtre Butonları */}
        <View style={styles.filterButtonsRow}>
          <TouchableOpacity
            style={styles.advancedFilterButton}
            onPress={() => setShowAdvancedModal(true)}
          >
            <Ionicons name="options" size={16} color="#1976d2" />
            <Text style={styles.advancedFilterButtonText}>
              {t('report.advancedFilters', 'Gelişmiş Filtreler')}
            </Text>
            {getActiveFilterCount() > 0 && (
              <View style={styles.filterBadge}>
                <Text style={styles.filterBadgeText}>{getActiveFilterCount()}</Text>
              </View>
            )}
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.applyButton}
            onPress={onApplyFilter}
          >
            <Text style={styles.applyButtonText}>{t('report.apply', 'Uygula')}</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={styles.resetButton}
            onPress={resetFilter}
          >
            <Text style={styles.resetButtonText}>{t('report.reset', 'Sıfırla')}</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Gelişmiş Filtreler Modal */}
      <Modal
        visible={showAdvancedModal}
        animationType="slide"
        transparent
        onRequestClose={() => setShowAdvancedModal(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{t('report.advancedFilters', 'Gelişmiş Filtreler')}</Text>
              <TouchableOpacity
                onPress={() => setShowAdvancedModal(false)}
                style={styles.closeButton}
              >
                <Ionicons name="close" size={24} color="#666" />
              </TouchableOpacity>
            </View>

            <ScrollView style={styles.modalBody}>
              {/* Kategori Filtresi */}
              <View style={styles.filterSection}>
                <Text style={styles.filterLabel}>{t('report.category', 'Kategori')}</Text>
                <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                  <TouchableOpacity
                    style={[
                      styles.filterChip,
                      !filter.categoryId && styles.filterChipActive
                    ]}
                    onPress={() => onFilterChange({ ...filter, categoryId: undefined })}
                  >
                    <Text style={[
                      styles.filterChipText,
                      !filter.categoryId && styles.filterChipTextActive
                    ]}>
                      {t('report.allCategories', 'Tümü')}
                    </Text>
                  </TouchableOpacity>
                  {categories.map((category) => (
                    <TouchableOpacity
                      key={category.id}
                      style={[
                        styles.filterChip,
                        filter.categoryId === category.id && styles.filterChipActive
                      ]}
                      onPress={() => onFilterChange({ ...filter, categoryId: category.id })}
                    >
                      <Text style={[
                        styles.filterChipText,
                        filter.categoryId === category.id && styles.filterChipTextActive
                      ]}>
                        {category.name}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </ScrollView>
              </View>

              {/* Ödeme Yöntemi Filtresi */}
              <View style={styles.filterSection}>
                <Text style={styles.filterLabel}>{t('report.paymentMethod', 'Ödeme Yöntemi')}</Text>
                <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                  <TouchableOpacity
                    style={[
                      styles.filterChip,
                      !filter.paymentMethod && styles.filterChipActive
                    ]}
                    onPress={() => onFilterChange({ ...filter, paymentMethod: undefined })}
                  >
                    <Text style={[
                      styles.filterChipText,
                      !filter.paymentMethod && styles.filterChipTextActive
                    ]}>
                      {t('report.allMethods', 'Tümü')}
                    </Text>
                  </TouchableOpacity>
                  {paymentMethods.map((method) => (
                    <TouchableOpacity
                      key={method}
                      style={[
                        styles.filterChip,
                        filter.paymentMethod === method && styles.filterChipActive
                      ]}
                      onPress={() => onFilterChange({ ...filter, paymentMethod: method })}
                    >
                      <Text style={[
                        styles.filterChipText,
                        filter.paymentMethod === method && styles.filterChipTextActive
                      ]}>
                        {method}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </ScrollView>
              </View>

              {/* Tutar Aralığı */}
              <View style={styles.filterSection}>
                <Text style={styles.filterLabel}>{t('report.amountRange', 'Tutar Aralığı')}</Text>
                <View style={styles.amountRow}>
                  <View style={styles.amountInputContainer}>
                    <Text style={styles.amountLabel}>{t('report.minAmount', 'Min')}</Text>
                    <TextInput
                      style={styles.amountInput}
                      placeholder="0"
                      keyboardType="numeric"
                      value={filter.minAmount?.toString()}
                      onChangeText={(text) => {
                        const value = parseFloat(text) || undefined;
                        onFilterChange({ ...filter, minAmount: value });
                      }}
                    />
                  </View>
                  <Text style={styles.amountSeparator}>-</Text>
                  <View style={styles.amountInputContainer}>
                    <Text style={styles.amountLabel}>{t('report.maxAmount', 'Max')}</Text>
                    <TextInput
                      style={styles.amountInput}
                      placeholder="∞"
                      keyboardType="numeric"
                      value={filter.maxAmount?.toString()}
                      onChangeText={(text) => {
                        const value = parseFloat(text) || undefined;
                        onFilterChange({ ...filter, maxAmount: value });
                      }}
                    />
                  </View>
                </View>
              </View>

              {/* Sıralama */}
              <View style={styles.filterSection}>
                <Text style={styles.filterLabel}>{t('report.sortBy', 'Sıralama')}</Text>
                <View style={styles.sortRow}>
                  <TouchableOpacity
                    style={[
                      styles.sortButton,
                      filter.sortBy === 'date' && styles.sortButtonActive
                    ]}
                    onPress={() => onFilterChange({ ...filter, sortBy: 'date' })}
                  >
                    <Text style={[
                      styles.sortButtonText,
                      filter.sortBy === 'date' && styles.sortButtonTextActive
                    ]}>
                      {t('report.sortByDate', 'Tarih')}
                    </Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={[
                      styles.sortButton,
                      filter.sortBy === 'amount' && styles.sortButtonActive
                    ]}
                    onPress={() => onFilterChange({ ...filter, sortBy: 'amount' })}
                  >
                    <Text style={[
                      styles.sortButtonText,
                      filter.sortBy === 'amount' && styles.sortButtonTextActive
                    ]}>
                      {t('report.sortByAmount', 'Tutar')}
                    </Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={styles.sortOrderButton}
                    onPress={() => onFilterChange({
                      ...filter,
                      sortOrder: filter.sortOrder === 'asc' ? 'desc' : 'asc'
                    })}
                  >
                    <Ionicons
                      name={filter.sortOrder === 'asc' ? 'arrow-up' : 'arrow-down'}
                      size={16}
                      color="#666"
                    />
                  </TouchableOpacity>
                </View>
              </View>
            </ScrollView>

            <View style={styles.modalFooter}>
              <TouchableOpacity
                style={styles.modalResetButton}
                onPress={resetFilter}
              >
                <Text style={styles.modalResetButtonText}>{t('report.reset', 'Sıfırla')}</Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={styles.modalApplyButton}
                onPress={() => {
                  onApplyFilter();
                  setShowAdvancedModal(false);
                }}
              >
                <Text style={styles.modalApplyButtonText}>{t('report.apply', 'Uygula')}</Text>
              </TouchableOpacity>
            </View>
          </View>
        </View>
      </Modal>

      {/* Tarih Seçici */}
      {showDatePicker && (
        <DateTimePicker
          value={showDatePicker === 'start' ? (filter.startDate || new Date()) : (filter.endDate || new Date())}
          mode="date"
          display="default"
          onChange={handleDateChange}
        />
      )}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: '#fff',
    borderRadius: 8,
    marginBottom: 16,
    elevation: 2,
  },
  mainFilterArea: {
    padding: 16,
  },
  dateSection: {
    marginBottom: 12,
  },
  sectionTitle: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
    marginBottom: 8,
  },
  dateRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  dateButton: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 6,
    gap: 6,
  },
  dateButtonText: {
    fontSize: 14,
    color: '#333',
  },
  dateSeparator: {
    fontSize: 16,
    color: '#666',
  },
  quickDateSection: {
    marginBottom: 12,
  },
  quickDateButton: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    backgroundColor: '#f5f5f5',
    borderRadius: 16,
    marginRight: 8,
  },
  quickDateButtonText: {
    fontSize: 12,
    color: '#666',
  },
  searchSection: {
    marginBottom: 12,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 6,
    paddingHorizontal: 12,
  },
  searchIcon: {
    marginRight: 8,
  },
  searchInput: {
    flex: 1,
    paddingVertical: 8,
    fontSize: 14,
  },
  clearButton: {
    padding: 4,
  },
  filterButtonsRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  advancedFilterButton: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderWidth: 1,
    borderColor: '#1976d2',
    borderRadius: 6,
    gap: 6,
    position: 'relative',
  },
  advancedFilterButtonText: {
    fontSize: 14,
    color: '#1976d2',
  },
  filterBadge: {
    position: 'absolute',
    top: -4,
    right: -4,
    backgroundColor: '#d32f2f',
    borderRadius: 10,
    minWidth: 20,
    height: 20,
    alignItems: 'center',
    justifyContent: 'center',
  },
  filterBadgeText: {
    fontSize: 10,
    color: '#fff',
    fontWeight: 'bold',
  },
  applyButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    backgroundColor: '#1976d2',
    borderRadius: 6,
  },
  applyButtonText: {
    fontSize: 14,
    color: '#fff',
    fontWeight: '600',
  },
  resetButton: {
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 6,
  },
  resetButtonText: {
    fontSize: 14,
    color: '#666',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.5)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    backgroundColor: '#fff',
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    maxHeight: '80%',
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
  },
  closeButton: {
    padding: 4,
  },
  modalBody: {
    padding: 16,
  },
  filterSection: {
    marginBottom: 20,
  },
  filterLabel: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
    marginBottom: 8,
  },
  filterChip: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 16,
    marginRight: 8,
  },
  filterChipActive: {
    backgroundColor: '#1976d2',
    borderColor: '#1976d2',
  },
  filterChipText: {
    fontSize: 12,
    color: '#666',
  },
  filterChipTextActive: {
    color: '#fff',
  },
  amountRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  amountInputContainer: {
    flex: 1,
  },
  amountLabel: {
    fontSize: 12,
    color: '#666',
    marginBottom: 4,
  },
  amountInput: {
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 6,
    paddingHorizontal: 12,
    paddingVertical: 8,
    fontSize: 14,
  },
  amountSeparator: {
    fontSize: 16,
    color: '#666',
  },
  sortRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  sortButton: {
    flex: 1,
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 6,
    alignItems: 'center',
  },
  sortButtonActive: {
    backgroundColor: '#1976d2',
    borderColor: '#1976d2',
  },
  sortButtonText: {
    fontSize: 14,
    color: '#666',
  },
  sortButtonTextActive: {
    color: '#fff',
  },
  sortOrderButton: {
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 6,
    alignItems: 'center',
  },
  modalFooter: {
    flexDirection: 'row',
    padding: 16,
    borderTopWidth: 1,
    borderTopColor: '#eee',
    gap: 12,
  },
  modalResetButton: {
    flex: 1,
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 6,
    backgroundColor: '#f5f5f5',
    alignItems: 'center',
  },
  modalResetButtonText: {
    fontSize: 14,
    fontWeight: '500',
    color: '#666',
  },
  modalApplyButton: {
    flex: 1,
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 6,
    backgroundColor: '#1976d2',
    alignItems: 'center',
  },
  modalApplyButtonText: {
    fontSize: 14,
    fontWeight: '500',
    color: '#fff',
  },
});

export default ReportFilterPanel; 