import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TouchableOpacity,
  TextInput,
  ScrollView,
  Alert,
  ActivityIndicator,
} from 'react-native';

import { Colors, Spacing, BorderRadius } from '../constants/Colors';
import { customerService, Customer } from '../services/api/customerService';

interface CustomerModalProps {
  visible: boolean;
  onClose: () => void;
  onCustomerSelected: (customer: Customer) => void;
  selectedCustomer?: Customer;
}

export default function CustomerModal({
  visible,
  onClose,
  onCustomerSelected,
  selectedCustomer,
}: CustomerModalProps) {
  const { t } = useTranslation(['customers', 'common']);
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [filteredCustomers, setFilteredCustomers] = useState<Customer[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<'Regular' | 'Premium' | 'VIP' | 'all'>('all');

  useEffect(() => {
    if (visible) {
      loadCustomers();
    }
  }, [visible]);

  useEffect(() => {
    filterCustomers();
  }, [customers, searchQuery, selectedCategory]);

  const loadCustomers = async () => {
    try {
      setLoading(true);
      const allCustomers = await customerService.getAll();
      setCustomers(allCustomers);
    } catch (error) {
      console.error('Failed to load customers:', error);
      Alert.alert(t('common:error'), t('customers:errorLoading', 'Failed to load customers'));
    } finally {
      setLoading(false);
    }
  };

  const filterCustomers = () => {
    let filtered = customers;

    // Kategori filtresi
    if (selectedCategory !== 'all') {
      filtered = filtered.filter(customer => customer.category === selectedCategory);
    }

    // Arama filtresi
    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(customer =>
        customer.name.toLowerCase().includes(query) ||
        customer.email?.toLowerCase().includes(query) ||
        customer.phone?.includes(query)
      );
    }

    setFilteredCustomers(filtered);
  };

  const handleCustomerSelect = (customer: Customer) => {
    onCustomerSelected(customer);
    onClose();
  };

  const getCategoryDisplayName = (category: string) => {
    switch (category) {
      case 'Regular':
        return t('customers:regular');
      case 'VIP':
        return t('customers:vip');
      case 'Premium':
        return t('customers:premium');
      default:
        return category;
    }
  };

  const getCategoryColor = (category: string) => {
    switch (category) {
      case 'VIP':
        return Colors.light.warning;
      case 'Premium':
        return Colors.light.primary;
      default:
        return Colors.light.success;
    }
  };

  const formatCurrency = (amount: number) => {
    return `€${amount.toFixed(2)}`;
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent
      onRequestClose={onClose}
    >
      <View style={styles.overlay}>
        <View style={styles.container}>
          <View style={styles.header}>
            <Text style={styles.title}>
              <Ionicons name="people" size={24} color={Colors.light.primary} />
              {' '}{t('customers:selectionTitle')}
            </Text>
            <TouchableOpacity style={styles.closeButton} onPress={onClose}>
              <Ionicons name="close" size={24} color={Colors.light.textSecondary} />
            </TouchableOpacity>
          </View>

          <View style={styles.searchSection}>
            <TextInput
              style={styles.searchInput}
              value={searchQuery}
              onChangeText={setSearchQuery}
              placeholder={t('customers:searchPlaceholder')}
              autoCapitalize="none"
              autoCorrect={false}
            />
          </View>

          <ScrollView horizontal style={styles.categoryFilter} showsHorizontalScrollIndicator={false}>
            <TouchableOpacity
              style={[
                styles.categoryChip,
                selectedCategory === 'all' && styles.categoryChipActive
              ]}
              onPress={() => setSelectedCategory('all')}
            >
              <Text style={[
                styles.categoryChipText,
                selectedCategory === 'all' && styles.categoryChipTextActive
              ]}>
                {t('customers:all')}
              </Text>
            </TouchableOpacity>
            {(['Regular', 'Premium', 'VIP'] as const).map((category) => (
              <TouchableOpacity
                key={category}
                style={[
                  styles.categoryChip,
                  selectedCategory === category && styles.categoryChipActive
                ]}
                onPress={() => setSelectedCategory(category)}
              >
                <Text style={[
                  styles.categoryChipText,
                  selectedCategory === category && styles.categoryChipTextActive
                ]}>
                  {getCategoryDisplayName(category)}
                </Text>
              </TouchableOpacity>
            ))}
          </ScrollView>

          <ScrollView style={styles.customersList}>
            {loading ? (
              <ActivityIndicator size="large" color={Colors.light.primary} />
            ) : filteredCustomers.length === 0 ? (
              <Text style={styles.noCustomersText}>
                {searchQuery.trim() ? t('customers:noSearchResults') : t('customers:noResults')}
              </Text>
            ) : (
              filteredCustomers.map((customer) => (
                <TouchableOpacity
                  key={customer.id}
                  style={[
                    styles.customerItem,
                    selectedCustomer?.id === customer.id && styles.customerItemSelected
                  ]}
                  onPress={() => handleCustomerSelect(customer)}
                >
                  <View style={styles.customerHeader}>
                    <View style={styles.customerInfo}>
                      <Text style={styles.customerName}>{customer.name}</Text>
                      <View style={styles.customerBadges}>
                        <View style={[
                          styles.categoryBadge,
                          { backgroundColor: getCategoryColor(customer.category) + '20' }
                        ]}>
                          <Text style={[
                            styles.categoryBadgeText,
                            { color: getCategoryColor(customer.category) }
                          ]}>
                            {getCategoryDisplayName(customer.category)}
                          </Text>
                        </View>
                        {customer.discountPercentage > 0 && (
                          <View style={[styles.categoryBadge, { backgroundColor: Colors.light.success + '20' }]}>
                            <Text style={[styles.categoryBadgeText, { color: Colors.light.success }]}>
                              %{customer.discountPercentage} {t('customers:discount')}
                            </Text>
                          </View>
                        )}
                      </View>
                    </View>
                  </View>

                  <View style={styles.customerDetails}>
                    {customer.email && (
                      <Text style={styles.customerDetail}>
                        <Ionicons name="mail" size={14} color={Colors.light.textSecondary} />
                        {' '}{customer.email}
                      </Text>
                    )}
                    {customer.phone && (
                      <Text style={styles.customerDetail}>
                        <Ionicons name="call" size={14} color={Colors.light.textSecondary} />
                        {' '}{customer.phone}
                      </Text>
                    )}
                    {customer.address && (
                      <Text style={styles.customerDetail}>
                        <Ionicons name="location" size={14} color={Colors.light.textSecondary} />
                        {' '}{customer.address}
                      </Text>
                    )}
                  </View>
                </TouchableOpacity>
              ))
            )}
          </ScrollView>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  container: {
    width: '90%',
    maxWidth: 500,
    maxHeight: '80%',
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.lg,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 2,
    },
    shadowOpacity: 0.25,
    shadowRadius: 3.84,
    elevation: 5,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  title: {
    fontSize: 20,
    fontWeight: '600',
    color: Colors.light.text,
    flexDirection: 'row',
    alignItems: 'center',
  },
  closeButton: {
    padding: Spacing.xs,
  },
  searchSection: {
    padding: Spacing.lg,
    paddingBottom: Spacing.md,
  },
  searchInput: {
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    fontSize: 16,
  },
  categoryFilter: {
    paddingHorizontal: Spacing.lg,
    paddingBottom: Spacing.md,
  },
  categoryChip: {
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    borderRadius: 20,
    backgroundColor: Colors.light.surface,
    borderWidth: 1,
    borderColor: Colors.light.border,
    marginRight: Spacing.sm,
  },
  categoryChipActive: {
    backgroundColor: Colors.light.primary,
    borderColor: Colors.light.primary,
  },
  categoryChipText: {
    fontSize: 14,
    color: Colors.light.textSecondary,
  },
  categoryChipTextActive: {
    color: 'white',
    fontWeight: '500',
  },
  customersList: {
    flex: 1,
    paddingHorizontal: Spacing.lg,
  },
  noCustomersText: {
    textAlign: 'center',
    color: Colors.light.textSecondary,
    fontSize: 16,
    paddingVertical: Spacing.xl,
  },
  customerItem: {
    backgroundColor: Colors.light.surface,
    padding: Spacing.md,
    marginBottom: Spacing.sm,
    borderRadius: BorderRadius.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  customerItemSelected: {
    borderColor: Colors.light.primary,
    backgroundColor: Colors.light.primary + '10',
  },
  customerHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: Spacing.sm,
  },
  customerInfo: {
    flex: 1,
  },
  customerName: {
    fontSize: 18,
    fontWeight: '600',
    color: Colors.light.text,
    marginBottom: Spacing.xs,
  },
  customerBadges: {
    flexDirection: 'row',
    gap: Spacing.xs,
  },
  categoryBadge: {
    paddingHorizontal: Spacing.sm,
    paddingVertical: Spacing.xs,
    borderRadius: BorderRadius.sm,
  },
  categoryBadgeText: {
    fontSize: 12,
    fontWeight: '500',
  },
  customerStats: {
    alignItems: 'flex-end',
  },
  loyaltyPoints: {
    fontSize: 14,
    color: Colors.light.primary,
    fontWeight: '500',
  },
  totalSpent: {
    fontSize: 14,
    color: Colors.light.success,
    fontWeight: '500',
  },
  customerDetails: {
    gap: Spacing.xs,
  },
  customerDetail: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    flexDirection: 'row',
    alignItems: 'center',
  },
}); 