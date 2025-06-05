import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  TextInput,
  Alert,
  ActivityIndicator
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { customerService, Customer } from '../../services/api/customerService';

export default function CustomersScreen() {
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);

  const fetchCustomers = async () => {
    try {
      setIsLoading(true);
      const data = await customerService.getAllCustomers();
      setCustomers(data);
    } catch (error) {
      Alert.alert('Hata', 'Müşteriler yüklenirken bir hata oluştu');
    } finally {
      setIsLoading(false);
      setIsRefreshing(false);
    }
  };

  useEffect(() => {
    fetchCustomers();
  }, []);

  const handleSearch = async () => {
    if (!searchQuery.trim()) {
      fetchCustomers();
      return;
    }

    try {
      setIsLoading(true);
      const results = await customerService.searchCustomers(searchQuery);
      setCustomers(results);
    } catch (error) {
      Alert.alert('Hata', 'Müşteri araması sırasında bir hata oluştu');
    } finally {
      setIsLoading(false);
    }
  };

  const handleRefresh = () => {
    setIsRefreshing(true);
    fetchCustomers();
  };

  const getCustomerDisplayName = (customer: Customer) => {
    if (customer.customerType === 'Business') {
      return customer.companyName || 'İsimsiz Şirket';
    }
    return `${customer.firstName || ''} ${customer.lastName || ''}`.trim() || 'İsimsiz Müşteri';
  };

  const renderCustomer = ({ item }: { item: Customer }) => (
    <TouchableOpacity style={styles.customerItem}>
      <View style={styles.customerInfo}>
        <Text style={styles.customerName}>{getCustomerDisplayName(item)}</Text>
        <Text style={styles.customerDetails}>{item.email}</Text>
        <Text style={styles.customerDetails}>{item.phone}</Text>
      </View>
      <View style={styles.customerType}>
        <Text style={styles.customerTypeText}>
          {item.customerType === 'Business' ? 'Firma' : 'Bireysel'}
        </Text>
      </View>
      <TouchableOpacity style={styles.editButton}>
        <Ionicons name="create-outline" size={24} color="#007AFF" />
      </TouchableOpacity>
    </TouchableOpacity>
  );

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <TextInput
          style={styles.searchInput}
          placeholder="Müşteri Ara..."
          value={searchQuery}
          onChangeText={setSearchQuery}
        />
        <TouchableOpacity style={styles.addButton}>
          <Ionicons name="add" size={24} color="white" />
        </TouchableOpacity>
      </View>

      <FlatList
        data={customers.filter(customer => {
          const searchLower = searchQuery.toLowerCase();
          const displayName = getCustomerDisplayName(customer).toLowerCase();
          return (
            displayName.includes(searchLower) ||
            customer.email.toLowerCase().includes(searchLower) ||
            customer.phone.includes(searchQuery)
          );
        })}
        renderItem={renderCustomer}
        keyExtractor={item => item.id.toString()}
        style={styles.customerList}
        refreshing={isRefreshing}
        onRefresh={handleRefresh}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
    padding: 10,
  },
  header: {
    flexDirection: 'row',
    marginBottom: 10,
    gap: 10,
  },
  searchInput: {
    flex: 1,
    height: 40,
    backgroundColor: 'white',
    borderRadius: 8,
    paddingHorizontal: 10,
  },
  addButton: {
    width: 40,
    height: 40,
    backgroundColor: '#007AFF',
    borderRadius: 8,
    justifyContent: 'center',
    alignItems: 'center',
  },
  customerList: {
    flex: 1,
  },
  customerItem: {
    backgroundColor: 'white',
    padding: 15,
    marginBottom: 8,
    borderRadius: 8,
    flexDirection: 'row',
    alignItems: 'center',
  },
  customerInfo: {
    flex: 2,
  },
  customerName: {
    fontSize: 16,
    fontWeight: 'bold',
  },
  customerDetails: {
    fontSize: 14,
    color: '#666',
    marginTop: 2,
  },
  customerType: {
    backgroundColor: '#E1F5FE',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 4,
    marginRight: 10,
  },
  customerTypeText: {
    color: '#0288D1',
    fontSize: 12,
    fontWeight: 'bold',
  },
  editButton: {
    padding: 5,
  },
}); 