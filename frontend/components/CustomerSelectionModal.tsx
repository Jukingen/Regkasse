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
  FlatList,
  ActivityIndicator,
  Alert,
} from 'react-native';

import { Colors, Spacing, BorderRadius } from '../constants/Colors';
import { customerService, Customer } from '../services/api/customerService';

interface CustomerSelectionModalProps {
  visible: boolean;
  onClose: () => void;
  onCustomerSelected: (customer: Customer) => void;
  selectedCustomer?: Customer;
}

export default function CustomerSelectionModal({
  visible,
  onClose,
  onCustomerSelected,
  selectedCustomer,
}: CustomerSelectionModalProps) {
  const { t } = useTranslation();
  const [searchQuery, setSearchQuery] = useState('');
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [filteredCustomers, setFilteredCustomers] = useState<Customer[]>([]);
  const [loading, setLoading] = useState(false);
  const [selectedCategory, setSelectedCategory] = useState<'Regular' | 'Premium' | 'VIP' | 'All'>('All');

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
      const data = await customerService.getCustomers();
      setCustomers(data.filter(c => c.isActive));
    } catch (error) {
      Alert.alert('Error', 'Failed to load customers');
    } finally {
      setLoading(false);
    }
  };

  const filterCustomers = () => {
    let filtered = customers;

    // Kategori filtresi
    if (selectedCategory !== 'All') {
      filtered = filtered.filter(c => c.category === selectedCategory);
    }

    // Arama filtresi
    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase();
      filtered = filtered.filter(c =>
        c.name.toLowerCase().includes(query) ||
        c.email.toLowerCase().includes(query) ||
        c.phone.includes(query) ||
        c.taxNumber.includes(query)
      );
    }

    setFilteredCustomers(filtered);
  };

  const handleCustomerSelect = (customer: Customer) => {
    onCustomerSelected(customer);
    onClose();
  };

  const getCategoryColor = (category: string) => {
    switch (category) {
      case 'VIP': return Colors.light.warning;
      case 'Premium': return Colors.light.primary;
      default: return Colors.light.success;
    }
  };

  const getCategoryIcon = (category: string) => {
    switch (category) {
      case 'VIP': return 'star';
      case 'Premium': return 'diamond';
      default: return 'person';
    }
  };

  const renderCustomerItem = ({ item }: { item: Customer }) => (
    <TouchableOpacity
      style={[
        styles.customerItem,
        selectedCustomer?.id === item.id && styles.selectedCustomerItem
      ]}
      onPress={() => handleCustomerSelect(item)}
    >
      <View style={styles.customerInfo}>
        <View style={styles.customerHeader}>
          <Ionicons
            name={getCategoryIcon(item.category) as any}
            size={20}
            color={getCategoryColor(item.category)}
          />
          <Text style={styles.customerName}>{item.name}</Text>
          <View style={[styles.categoryBadge, { backgroundColor: getCategoryColor(item.category) }]}>
            <Text style={styles.categoryText}>{item.category}</Text>
          </View>
        </View>
        
        <Text style={styles.customerEmail}>{item.email}</Text>
        <Text style={styles.customerPhone}>{item.phone}</Text>
        
        {item.discountPercentage > 0 && (
          <View style={styles.discountInfo}>
            <Ionicons name="pricetag" size={16} color={Colors.light.success} />
            <Text style={styles.discountText}>
              {item.discountPercentage}% discount
            </Text>
          </View>
        )}
      </View>
      
      <Ionicons name="chevron-forward" size={20} color={Colors.light.textSecondary} />
    </TouchableOpacity>
  );

  const renderCategoryFilter = () => (
    <View style={styles.categoryFilter}>
      {(['All', 'Regular', 'Premium', 'VIP'] as const).map(category => (
        <TouchableOpacity
          key={category}
          style={[
            styles.categoryFilterButton,
            selectedCategory === category && styles.categoryFilterButtonActive
          ]}
          onPress={() => setSelectedCategory(category)}
        >
          <Text style={[
            styles.categoryFilterText,
            selectedCategory === category && styles.categoryFilterTextActive
          ]}>
            {category}
          </Text>
        </TouchableOpacity>
      ))}
    </View>
  );

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
    >
      <View style={styles.container}>
        {/* Header */}
        <View style={styles.header}>
          <TouchableOpacity onPress={onClose} style={styles.closeButton}>
            <Ionicons name="close" size={24} color={Colors.light.text} />
          </TouchableOpacity>
          <Text style={styles.title}>Select Customer</Text>
          <View style={styles.placeholder} />
        </View>

        {/* Search Bar */}
        <View style={styles.searchContainer}>
          <Ionicons name="search" size={20} color={Colors.light.textSecondary} />
          <TextInput
            style={styles.searchInput}
            placeholder="Search customers..."
            value={searchQuery}
            onChangeText={setSearchQuery}
            placeholderTextColor={Colors.light.textSecondary}
          />
          {searchQuery.length > 0 && (
            <TouchableOpacity onPress={() => setSearchQuery('')}>
              <Ionicons name="close-circle" size={20} color={Colors.light.textSecondary} />
            </TouchableOpacity>
          )}
        </View>

        {/* Category Filter */}
        {renderCategoryFilter()}

        {/* Customer List */}
        {loading ? (
          <View style={styles.loadingContainer}>
            <ActivityIndicator size="large" color={Colors.light.primary} />
            <Text style={styles.loadingText}>Loading customers...</Text>
          </View>
        ) : (
          <FlatList
            data={filteredCustomers}
            renderItem={renderCustomerItem}
            keyExtractor={(item) => item.id}
            style={styles.customerList}
            showsVerticalScrollIndicator={false}
            ListEmptyComponent={
              <View style={styles.emptyContainer}>
                <Ionicons name="people-outline" size={64} color={Colors.light.textSecondary} />
                <Text style={styles.emptyText}>No customers found</Text>
                <Text style={styles.emptySubtext}>
                  Try adjusting your search or category filter
                </Text>
              </View>
            }
          />
        )}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: Spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  closeButton: {
    padding: Spacing.xs,
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
    color: Colors.light.text,
  },
  placeholder: {
    width: 40,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    margin: Spacing.lg,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  searchInput: {
    flex: 1,
    marginLeft: Spacing.sm,
    fontSize: 16,
    color: Colors.light.text,
  },
  categoryFilter: {
    flexDirection: 'row',
    paddingHorizontal: Spacing.lg,
    marginBottom: Spacing.md,
  },
  categoryFilterButton: {
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    marginRight: Spacing.sm,
    borderRadius: BorderRadius.md,
    backgroundColor: Colors.light.surface,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  categoryFilterButtonActive: {
    backgroundColor: Colors.light.primary,
    borderColor: Colors.light.primary,
  },
  categoryFilterText: {
    fontSize: 14,
    color: Colors.light.textSecondary,
  },
  categoryFilterTextActive: {
    color: 'white',
    fontWeight: '600',
  },
  customerList: {
    flex: 1,
    paddingHorizontal: Spacing.lg,
  },
  customerItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    marginBottom: Spacing.sm,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  selectedCustomerItem: {
    borderColor: Colors.light.primary,
    backgroundColor: Colors.light.primary + '10',
  },
  customerInfo: {
    flex: 1,
  },
  customerHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: Spacing.xs,
  },
  customerName: {
    fontSize: 16,
    fontWeight: '600',
    color: Colors.light.text,
    marginLeft: Spacing.xs,
    flex: 1,
  },
  categoryBadge: {
    paddingHorizontal: Spacing.sm,
    paddingVertical: Spacing.xs,
    borderRadius: BorderRadius.sm,
  },
  categoryText: {
    fontSize: 12,
    color: 'white',
    fontWeight: '600',
  },
  customerEmail: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    marginBottom: Spacing.xs,
  },
  customerPhone: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    marginBottom: Spacing.xs,
  },
  discountInfo: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  discountText: {
    fontSize: 12,
    color: Colors.light.success,
    marginLeft: Spacing.xs,
    fontWeight: '500',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: Spacing.md,
    fontSize: 16,
    color: Colors.light.textSecondary,
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: Spacing.xl,
  },
  emptyText: {
    fontSize: 18,
    color: Colors.light.text,
    marginTop: Spacing.md,
    textAlign: 'center',
  },
  emptySubtext: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    marginTop: Spacing.sm,
    textAlign: 'center',
  },
}); 