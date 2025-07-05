import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Modal,
  FlatList,
  TextInput,
  Alert,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { customerService, Customer } from '../services/api/customerService';
import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';

interface CustomerSelectionProps {
  selectedCustomer: Customer | null;
  onCustomerSelect: (customer: Customer | null) => void;
  showCustomerInfo?: boolean;
}

const CustomerSelection: React.FC<CustomerSelectionProps> = ({
  selectedCustomer,
  onCustomerSelect,
  showCustomerInfo = true,
}) => {
  const { t } = useTranslation();
  const [showCustomerModal, setShowCustomerModal] = useState(false);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');

  // Müşterileri yükle
  const loadCustomers = async () => {
    try {
      setLoading(true);
      const customersData = await customerService.getAllCustomers();
      setCustomers(customersData);
    } catch (error) {
      console.error('Customers load failed:', error);
      Alert.alert(
        t('errors.load_failed'),
        t('errors.customers_load_failed'),
        [{ text: t('common.ok') }]
      );
    } finally {
      setLoading(false);
    }
  };

  // Filtrelenmiş müşteriler
  const filteredCustomers = (customers || []).filter(customer =>
    customer.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
    customer.email?.toLowerCase().includes(searchQuery.toLowerCase()) ||
    customer.phone?.includes(searchQuery) ||
    customer.customerNumber?.includes(searchQuery)
  );

  // Müşteri seç
  const handleCustomerSelect = (customer: Customer) => {
    onCustomerSelect(customer);
    setShowCustomerModal(false);
    setSearchQuery('');
  };

  // Müşteri seçimini kaldır
  const clearCustomerSelection = () => {
    onCustomerSelect(null);
  };

  // Modal açıldığında müşterileri yükle
  useEffect(() => {
    if (showCustomerModal) {
      loadCustomers();
    }
  }, [showCustomerModal]);

  const renderCustomerItem = ({ item }: { item: Customer }) => (
    <TouchableOpacity
      style={styles.customerItem}
      onPress={() => handleCustomerSelect(item)}
    >
      <View style={styles.customerAvatar}>
        <Ionicons 
          name="person" 
          size={24} 
          color={Colors.light.surface} 
        />
      </View>
      
      <View style={styles.customerInfo}>
        <Text style={styles.customerName}>{item.name}</Text>
        {item.customerNumber && (
          <Text style={styles.customerNumber}>
            {t('customer.number')}: {item.customerNumber}
          </Text>
        )}
        {item.email && (
          <Text style={styles.customerEmail}>{item.email}</Text>
        )}
        {item.phone && (
          <Text style={styles.customerPhone}>{item.phone}</Text>
        )}
        <Text style={styles.customerType}>
          {t(`customer.type.${item.type || 'individual'}`)}
        </Text>
      </View>
      
      <Ionicons name="chevron-forward" size={20} color={Colors.light.textSecondary} />
    </TouchableOpacity>
  );

  return (
    <View style={styles.container}>
      {/* Müşteri seçim butonu */}
      <TouchableOpacity
        style={styles.customerButton}
        onPress={() => setShowCustomerModal(true)}
        activeOpacity={0.7}
      >
        <View style={styles.customerButtonContent}>
          <Ionicons 
            name="person-outline" 
            size={20} 
            color={Colors.light.textSecondary} 
          />
          <Text style={styles.customerButtonText}>
            {selectedCustomer ? selectedCustomer.name : t('customer.select')}
          </Text>
        </View>
        
        {selectedCustomer && (
          <TouchableOpacity
            onPress={clearCustomerSelection}
            style={styles.clearCustomerButton}
          >
            <Ionicons name="close-circle" size={20} color={Colors.light.error} />
          </TouchableOpacity>
        )}
      </TouchableOpacity>

      {/* Müşteri bilgileri */}
      {selectedCustomer && showCustomerInfo && (
        <View style={styles.customerInfoCard}>
          <View style={styles.customerInfoHeader}>
            <Text style={styles.customerInfoTitle}>{t('customer.info')}</Text>
          </View>
          
          <View style={styles.customerInfoContent}>
            {selectedCustomer.customerNumber && (
              <View style={styles.infoRow}>
                <Text style={styles.infoLabel}>{t('customer.number')}:</Text>
                <Text style={styles.infoValue}>{selectedCustomer.customerNumber}</Text>
              </View>
            )}
            
            {selectedCustomer.email && (
              <View style={styles.infoRow}>
                <Text style={styles.infoLabel}>{t('customer.email')}:</Text>
                <Text style={styles.infoValue}>{selectedCustomer.email}</Text>
              </View>
            )}
            
            {selectedCustomer.phone && (
              <View style={styles.infoRow}>
                <Text style={styles.infoLabel}>{t('customer.phone')}:</Text>
                <Text style={styles.infoValue}>{selectedCustomer.phone}</Text>
              </View>
            )}
            
            <View style={styles.infoRow}>
              <Text style={styles.infoLabel}>{t('customer.type')}:</Text>
              <Text style={styles.infoValue}>
                {t(`customer.type.${selectedCustomer.type || 'individual'}`)}
              </Text>
            </View>
          </View>
        </View>
      )}

      {/* Müşteri seçim modalı */}
      <Modal
        visible={showCustomerModal}
        animationType="slide"
        transparent={true}
        onRequestClose={() => setShowCustomerModal(false)}
      >
        <View style={styles.modalOverlay}>
          <View style={styles.modalContent}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>{t('customer.select')}</Text>
              <TouchableOpacity
                onPress={() => setShowCustomerModal(false)}
                style={styles.closeButton}
              >
                <Ionicons name="close" size={24} color={Colors.light.text} />
              </TouchableOpacity>
            </View>

            <View style={styles.searchContainer}>
              <Ionicons name="search" size={20} color={Colors.light.textSecondary} />
              <TextInput
                style={styles.searchInput}
                placeholder={t('customer.search')}
                value={searchQuery}
                onChangeText={setSearchQuery}
              />
            </View>

            <FlatList
              data={filteredCustomers}
              renderItem={renderCustomerItem}
              keyExtractor={(item) => item.id}
              showsVerticalScrollIndicator={false}
              contentContainerStyle={styles.customerList}
              ListEmptyComponent={
                <View style={styles.emptyState}>
                  <Ionicons name="people-outline" size={48} color={Colors.light.textSecondary} />
                  <Text style={styles.emptyStateText}>
                    {loading ? t('common.loading') : t('customer.no_customers')}
                  </Text>
                </View>
              }
            />
          </View>
        </View>
      </Modal>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    marginBottom: Spacing.md,
  },
  customerButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
    minHeight: 44,
  },
  customerButtonContent: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
  },
  customerButtonText: {
    ...Typography.body,
    color: Colors.light.text,
    marginLeft: Spacing.sm,
  },
  clearCustomerButton: {
    padding: Spacing.xs,
  },
  customerInfoCard: {
    backgroundColor: Colors.light.cartBackground,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    marginTop: Spacing.sm,
  },
  customerInfoHeader: {
    marginBottom: Spacing.sm,
  },
  customerInfoTitle: {
    ...Typography.h3,
    color: Colors.light.text,
  },
  customerInfoContent: {
    gap: Spacing.xs,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  infoLabel: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
    fontWeight: '500',
  },
  infoValue: {
    ...Typography.bodySmall,
    color: Colors.light.text,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    backgroundColor: Colors.light.surface,
    borderTopLeftRadius: BorderRadius.xl,
    borderTopRightRadius: BorderRadius.xl,
    maxHeight: '80%',
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  modalTitle: {
    ...Typography.h3,
    color: Colors.light.text,
  },
  closeButton: {
    padding: Spacing.xs,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.md,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    margin: Spacing.md,
    marginTop: 0,
  },
  searchInput: {
    flex: 1,
    marginLeft: Spacing.sm,
    ...Typography.body,
    color: Colors.light.text,
  },
  customerList: {
    padding: Spacing.md,
  },
  customerItem: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    marginBottom: Spacing.sm,
  },
  customerAvatar: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: Colors.light.primary,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: Spacing.md,
  },
  customerInfo: {
    flex: 1,
  },
  customerName: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '600',
    marginBottom: Spacing.xs,
  },
  customerNumber: {
    ...Typography.caption,
    color: Colors.light.primary,
    fontWeight: '500',
  },
  customerEmail: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
  },
  customerPhone: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
  },
  customerType: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    fontStyle: 'italic',
  },
  emptyState: {
    alignItems: 'center',
    padding: Spacing.xl,
  },
  emptyStateText: {
    ...Typography.body,
    color: Colors.light.textSecondary,
    textAlign: 'center',
    marginTop: Spacing.sm,
  },
});

export default CustomerSelection; 