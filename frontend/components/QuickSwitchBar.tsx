import React, { useState, useEffect } from 'react';
import {
  View,
  ScrollView,
  TouchableOpacity,
  Text,
  StyleSheet,
  Image,
  Alert,
  ActivityIndicator,
} from 'react-native';

import { apiClient } from '../services/api/config';

export interface Waiter {
  id: string;
  name: string;
  avatar?: string;
  isActive: boolean;
}

export interface Table {
  id: string;
  number: number;
  name: string;
  status: 'empty' | 'occupied' | 'reserved';
  currentCartId?: string;
  currentTotal?: number;
}

export interface OpenCart {
  cartId: string;
  tableNumber: string;
  waiterName?: string;
  totalAmount: number;
  itemCount: number;
  createdAt: string;
}

interface QuickSwitchBarProps {
  onSelectWaiter: (waiter: Waiter) => void;
  onSelectTable: (table: Table) => void;
  onSelectCart: (cart: OpenCart) => void;
  selectedWaiterId?: string;
  selectedTableId?: string;
}

const QuickSwitchBar: React.FC<QuickSwitchBarProps> = ({
  onSelectWaiter,
  onSelectTable,
  onSelectCart,
  selectedWaiterId,
  selectedTableId,
}) => {
  const [waiters, setWaiters] = useState<Waiter[]>([]);
  const [tables, setTables] = useState<Table[]>([]);
  const [openCarts, setOpenCarts] = useState<OpenCart[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    Promise.all([fetchWaiters(), fetchTables(), fetchOpenCarts()])
      .finally(() => setLoading(false));
  }, []);

  const fetchWaiters = async () => {
    try {
      const response = await apiClient.get<Waiter[]>('/users/waiters');
      setWaiters(response.filter((w: any) => w.isActive));
    } catch (error) {
      console.error('Garsonlar yüklenemedi:', error);
    }
  };

  const fetchTables = async () => {
    try {
      const response = await apiClient.get<Table[]>('/table');
      setTables(response);
    } catch (error) {
      console.error('Masalar yüklenemedi:', error);
    }
  };

  const fetchOpenCarts = async () => {
    try {
      const response = await apiClient.get<OpenCart[]>('/cart/open');
      setOpenCarts(response);
    } catch (error) {
      console.error('Açık sepetler yüklenemedi:', error);
    }
  };

  const handleWaiterSelect = (waiter: Waiter) => {
    onSelectWaiter(waiter);
    Alert.alert('Garson Seçildi', `${waiter.name} seçildi`);
  };

  const handleTableSelect = (table: Table) => {
    onSelectTable(table);
    if (table.status === 'occupied') {
      Alert.alert('Masa Seçildi', `Masa ${table.number} seçildi (${table.currentTotal?.toFixed(2)} €)`);
    } else {
      Alert.alert('Masa Seçildi', `Masa ${table.number} seçildi (Boş)`);
    }
  };

  const handleCartSelect = (cart: OpenCart) => {
    onSelectCart(cart);
    Alert.alert('Sepet Seçildi', `Sepet ${cart.cartId.slice(-4)} seçildi (${cart.totalAmount.toFixed(2)} €)`);
  };

  if (loading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#007AFF" />
        <Text style={styles.loadingText}>Yükleniyor...</Text>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* Garsonlar */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Garsonlar</Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.scrollView}>
          {waiters.map(waiter => (
            <TouchableOpacity
              key={waiter.id}
              style={[
                styles.waiterButton,
                selectedWaiterId === waiter.id && styles.selectedItem
              ]}
              onPress={() => handleWaiterSelect(waiter)}
            >
              {waiter.avatar ? (
                <Image source={{ uri: waiter.avatar }} style={styles.waiterAvatar} />
              ) : (
                <View style={styles.waiterAvatarPlaceholder}>
                  <Text style={styles.waiterInitial}>{waiter.name.charAt(0)}</Text>
                </View>
              )}
              <Text style={styles.waiterName}>{waiter.name}</Text>
            </TouchableOpacity>
          ))}
        </ScrollView>
      </View>
      {/* Masalar */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Masalar</Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.scrollView}>
          {tables.map(table => (
            <TouchableOpacity
              key={table.id}
              style={[
                styles.tableButton,
                table.status === 'occupied' && styles.occupiedTable,
                table.status === 'reserved' && styles.reservedTable,
                selectedTableId === table.id && styles.selectedItem
              ]}
              onPress={() => handleTableSelect(table)}
            >
              <Text style={styles.tableNumber}>Masa {table.number}</Text>
              <Text style={[
                styles.tableStatus,
                table.status === 'occupied' && styles.occupiedStatus,
                table.status === 'reserved' && styles.reservedStatus
              ]}>
                {table.status === 'empty' ? 'Boş' : table.status === 'occupied' ? 'Dolu' : 'Rezerve'}
              </Text>
              {table.currentTotal && (
                <Text style={styles.tableTotal}>{table.currentTotal.toFixed(2)} €</Text>
              )}
            </TouchableOpacity>
          ))}
        </ScrollView>
      </View>
      {/* Açık Sepetler */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Açık Sepetler ({openCarts.length})</Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.scrollView}>
          {openCarts.map(cart => (
            <TouchableOpacity
              key={cart.cartId}
              style={styles.cartButton}
              onPress={() => handleCartSelect(cart)}
            >
              <Text style={styles.cartId}>Sepet {cart.cartId.slice(-4)}</Text>
              <Text style={styles.cartTable}>Masa {cart.tableNumber}</Text>
              <Text style={styles.cartTotal}>{cart.totalAmount.toFixed(2)} €</Text>
              <Text style={styles.cartItems}>{cart.itemCount} ürün</Text>
              {cart.waiterName && (
                <Text style={styles.cartWaiter}>{cart.waiterName}</Text>
              )}
            </TouchableOpacity>
          ))}
          {openCarts.length === 0 && (
            <Text style={styles.emptyText}>Açık sepet yok</Text>
          )}
        </ScrollView>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: { backgroundColor: '#f8f9fa', padding: 8 },
  section: { marginBottom: 16 },
  sectionTitle: { fontSize: 18, fontWeight: 'bold', marginBottom: 8 },
  scrollView: { flexDirection: 'row' },
  waiterButton: { alignItems: 'center', marginRight: 12, padding: 8, borderRadius: 8, backgroundColor: '#fff', borderWidth: 1, borderColor: '#eee' },
  selectedItem: { borderColor: '#007AFF', borderWidth: 2 },
  waiterAvatar: { width: 40, height: 40, borderRadius: 20, marginBottom: 4 },
  waiterAvatarPlaceholder: { width: 40, height: 40, borderRadius: 20, backgroundColor: '#eee', alignItems: 'center', justifyContent: 'center', marginBottom: 4 },
  waiterInitial: { fontSize: 18, color: '#888' },
  waiterName: { fontSize: 14 },
  tableButton: { alignItems: 'center', marginRight: 12, padding: 8, borderRadius: 8, backgroundColor: '#fff', borderWidth: 1, borderColor: '#eee' },
  occupiedTable: { backgroundColor: '#ffe0e0' },
  reservedTable: { backgroundColor: '#e0e0ff' },
  tableNumber: { fontSize: 16, fontWeight: 'bold' },
  tableStatus: { fontSize: 12 },
  occupiedStatus: { color: '#e74c3c' },
  reservedStatus: { color: '#2980b9' },
  tableTotal: { fontSize: 14, color: '#27ae60' },
  cartButton: { alignItems: 'center', marginRight: 12, padding: 8, borderRadius: 8, backgroundColor: '#fff', borderWidth: 1, borderColor: '#eee' },
  cartId: { fontSize: 14, fontWeight: 'bold' },
  cartTable: { fontSize: 12 },
  cartTotal: { fontSize: 14, color: '#27ae60' },
  cartItems: { fontSize: 12 },
  cartWaiter: { fontSize: 12, color: '#888' },
  emptyText: { fontSize: 14, color: '#888', marginTop: 8 },
  loadingContainer: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 32 },
  loadingText: { marginTop: 12, fontSize: 16 },
});

export default QuickSwitchBar; 