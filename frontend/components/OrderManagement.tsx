import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  Alert,
  ActivityIndicator,
  TextInput,
} from 'react-native';
import { useOrders } from '../hooks/useOrders';
import { usePayment } from '../hooks/usePayment';

// Türkçe Açıklama: Sipariş yönetimi component'i - Masa bazlı siparişleri yönetir
interface OrderManagementProps {
  tableNumber: number;
  onOrderUpdate: () => void;
}

export default function OrderManagement({ tableNumber, onOrderUpdate }: OrderManagementProps) {
  const [order, setOrder] = useState<any>(null);
  const [customerId, setCustomerId] = useState<string>('demo-customer-001');
  const [notes, setNotes] = useState<string>('');
  const [showPaymentModal, setShowPaymentModal] = useState(false);
  
  const {
    loading: orderLoading,
    error: orderError,
    getOrder,
    createOrder,
    addItemToOrder,
    removeItemFromOrder,
    updateOrderItemQuantity,
    clearOrder,
    cancelOrder,
    updateOrderNotes,
    updateOrderCustomer,
    clearError: clearOrderError
  } = useOrders();

  const {
    loading: paymentLoading,
    error: paymentError,
    clearError: clearPaymentError
  } = usePayment();

  // Siparişi yükle
  useEffect(() => {
    loadOrder();
  }, [tableNumber]);

  const loadOrder = async () => {
    try {
      const orderData = await getOrder(tableNumber);
      if (orderData?.success) {
        setOrder(orderData.cart);
        setCustomerId(orderData.cart.customerId || 'demo-customer-001');
        setNotes(orderData.cart.notes || '');
      } else {
        // Sipariş yoksa yeni oluştur
        const newOrder = await createOrder(tableNumber, customerId);
        if (newOrder?.success) {
          setOrder(newOrder.cart);
        }
      }
    } catch (error) {
      console.error('Order load error:', error);
    }
  };

  // Ürün ekle
  const handleAddItem = async (productId: string, quantity: number = 1) => {
    try {
      const result = await addItemToOrder(tableNumber, productId, quantity, notes);
      if (result?.success) {
        await loadOrder(); // Siparişi yeniden yükle
        onOrderUpdate(); // Parent component'i güncelle
      }
    } catch (error) {
      console.error('Add item error:', error);
    }
  };

  // Ürün çıkar
  const handleRemoveItem = async (itemId: string) => {
    try {
      const result = await removeItemFromOrder(tableNumber, itemId);
      if (result?.success) {
        await loadOrder(); // Siparişi yeniden yükle
        onOrderUpdate(); // Parent component'i güncelle
      }
    } catch (error) {
      console.error('Remove item error:', error);
    }
  };

  // Miktar güncelle
  const handleQuantityUpdate = async (itemId: string, newQuantity: number) => {
    try {
      const result = await updateOrderItemQuantity(tableNumber, itemId, newQuantity);
      if (result?.success) {
        await loadOrder(); // Siparişi yeniden yükle
        onOrderUpdate(); // Parent component'i güncelle
      }
    } catch (error) {
      console.error('Quantity update error:', error);
    }
  };

  // Siparişi temizle
  const handleClearOrder = async () => {
    Alert.alert(
      'Siparişi Temizle',
      'Bu siparişi tamamen temizlemek istediğinizden emin misiniz?',
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Temizle',
          style: 'destructive',
          onPress: async () => {
            try {
              const result = await clearOrder(tableNumber);
              if (result?.success) {
                setOrder(null);
                setNotes('');
                onOrderUpdate(); // Parent component'i güncelle
              }
            } catch (error) {
              console.error('Clear order error:', error);
            }
          }
        }
      ]
    );
  };

  // Siparişi iptal et
  const handleCancelOrder = async () => {
    Alert.alert(
      'Siparişi İptal Et',
      'Bu siparişi iptal etmek istediğinizden emin misiniz?',
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'İptal Et',
          style: 'destructive',
          onPress: async () => {
            try {
              const result = await cancelOrder(tableNumber, 'Kasiyer tarafından iptal edildi');
              if (result?.success) {
                setOrder(null);
                setNotes('');
                onOrderUpdate(); // Parent component'i güncelle
              }
            } catch (error) {
              console.error('Cancel order error:', error);
            }
          }
        }
      ]
    );
  };

  // Notları güncelle
  const handleNotesUpdate = async () => {
    try {
      const result = await updateOrderNotes(tableNumber, notes);
      if (result?.success) {
        await loadOrder(); // Siparişi yeniden yükle
      }
    } catch (error) {
      console.error('Notes update error:', error);
    }
  };

  // Müşteri bilgilerini güncelle
  const handleCustomerUpdate = async () => {
    try {
      const result = await updateOrderCustomer(tableNumber, customerId);
      if (result?.success) {
        await loadOrder(); // Siparişi yeniden yükle
      }
    } catch (error) {
      console.error('Customer update error:', error);
    }
  };

  // Ödeme modal'ını aç
  const handlePayment = () => {
    if (!order || order.items.length === 0) {
      Alert.alert('Hata', 'Ödeme için ürün bulunamadı');
      return;
    }
    setShowPaymentModal(true);
  };

  // Ödeme başarılı
  const handlePaymentSuccess = async (paymentId: string) => {
    try {
      Alert.alert('Başarılı', `Ödeme tamamlandı!\nÖdeme ID: ${paymentId}`);
      
      // Siparişi temizle
      await clearOrder(tableNumber);
      setOrder(null);
      setNotes('');
      onOrderUpdate(); // Parent component'i güncelle
      
      setShowPaymentModal(false);
    } catch (error) {
      console.error('Payment success handling error:', error);
    }
  };

  if (orderLoading) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator size="large" color="#007AFF" />
        <Text style={styles.loadingText}>Sipariş yükleniyor...</Text>
      </View>
    );
  }

  if (!order) {
    return (
      <View style={styles.emptyContainer}>
        <Text style={styles.emptyTitle}>Masa {tableNumber}</Text>
        <Text style={styles.emptySubtitle}>Henüz sipariş yok</Text>
        <TouchableOpacity style={styles.newOrderButton} onPress={loadOrder}>
          <Text style={styles.newOrderButtonText}>Yeni Sipariş</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.title}>Masa {tableNumber} - Sipariş</Text>
        <View style={styles.headerActions}>
          <TouchableOpacity style={styles.actionButton} onPress={handleClearOrder}>
            <Ionicons name="trash-outline" size={20} color="#f44336" />
          </TouchableOpacity>
          <TouchableOpacity style={styles.actionButton} onPress={handleCancelOrder}>
            <Ionicons name="close-circle-outline" size={20} color="#ff9800" />
          </TouchableOpacity>
        </View>
      </View>

      {/* Müşteri Bilgileri */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Müşteri Bilgileri</Text>
        <View style={styles.inputRow}>
          <Text style={styles.label}>Müşteri ID:</Text>
          <TextInput
            style={styles.input}
            value={customerId}
            onChangeText={setCustomerId}
            placeholder="Müşteri ID"
          />
          <TouchableOpacity style={styles.updateButton} onPress={handleCustomerUpdate}>
            <Text style={styles.updateButtonText}>Güncelle</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Notlar */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Notlar</Text>
        <View style={styles.notesContainer}>
          <TextInput
            style={styles.notesInput}
            value={notes}
            onChangeText={setNotes}
            placeholder="Sipariş notları..."
            multiline
            numberOfLines={3}
          />
          <TouchableOpacity style={styles.updateButton} onPress={handleNotesUpdate}>
            <Text style={styles.updateButtonText}>Güncelle</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Sipariş Öğeleri */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Sipariş Öğeleri</Text>
        <ScrollView style={styles.itemsContainer}>
          {order.items?.map((item: any, index: number) => (
            <View key={index} style={styles.orderItem}>
              <View style={styles.itemInfo}>
                <Text style={styles.itemName}>{item.productName}</Text>
                <Text style={styles.itemPrice}>€{item.unitPrice.toFixed(2)}</Text>
              </View>
              <View style={styles.itemActions}>
                <TouchableOpacity
                  style={styles.quantityButton}
                  onPress={() => handleQuantityUpdate(item.id, item.quantity - 1)}
                >
                  <Ionicons name="remove" size={16} color="#666" />
                </TouchableOpacity>
                <Text style={styles.quantityText}>{item.quantity}</Text>
                <TouchableOpacity
                  style={styles.quantityButton}
                  onPress={() => handleQuantityUpdate(item.id, item.quantity + 1)}
                >
                  <Ionicons name="add" size={16} color="#666" />
                </TouchableOpacity>
                <TouchableOpacity
                  style={styles.removeButton}
                  onPress={() => handleRemoveItem(item.id)}
                >
                  <Ionicons name="trash-outline" size={16} color="#f44336" />
                </TouchableOpacity>
              </View>
              <Text style={styles.itemTotal}>€{item.totalPrice.toFixed(2)}</Text>
            </View>
          ))}
        </ScrollView>
      </View>

      {/* Toplam */}
      <View style={styles.totalSection}>
        <View style={styles.totalRow}>
          <Text style={styles.totalLabel}>Ara Toplam:</Text>
          <Text style={styles.totalAmount}>€{order.subtotal?.toFixed(2) || '0.00'}</Text>
        </View>
        <View style={styles.totalRow}>
          <Text style={styles.totalLabel}>KDV:</Text>
          <Text style={styles.totalAmount}>€{order.totalTax?.toFixed(2) || '0.00'}</Text>
        </View>
        <View style={styles.totalRow}>
          <Text style={styles.grandTotalLabel}>Genel Toplam:</Text>
          <Text style={styles.grandTotalAmount}>€{order.grandTotal?.toFixed(2) || '0.00'}</Text>
        </View>
      </View>

      {/* Ödeme Butonu */}
      {order.items && order.items.length > 0 && (
        <TouchableOpacity style={styles.paymentButton} onPress={handlePayment}>
          <Text style={styles.paymentButtonText}>
            €{order.grandTotal?.toFixed(2) || '0.00'} Öde
          </Text>
        </TouchableOpacity>
      )}

      {/* Hata Mesajları */}
      {(orderError || paymentError) && (
        <View style={styles.errorContainer}>
          <Text style={styles.errorText}>
            {orderError || paymentError}
          </Text>
          <TouchableOpacity
            style={styles.clearErrorButton}
            onPress={() => {
              clearOrderError();
              clearPaymentError();
            }}
          >
            <Text style={styles.clearErrorButtonText}>Temizle</Text>
          </TouchableOpacity>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 10,
    fontSize: 16,
    color: '#666',
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 20,
  },
  emptyTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 5,
  },
  emptySubtitle: {
    fontSize: 16,
    color: '#666',
    marginBottom: 20,
  },
  newOrderButton: {
    backgroundColor: '#007AFF',
    paddingHorizontal: 20,
    paddingVertical: 12,
    borderRadius: 8,
  },
  newOrderButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    backgroundColor: '#fff',
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  title: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
  },
  headerActions: {
    flexDirection: 'row',
  },
  actionButton: {
    padding: 8,
    marginLeft: 10,
  },
  section: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 15,
  },
  inputRow: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  label: {
    fontSize: 14,
    color: '#333',
    marginRight: 10,
    minWidth: 80,
  },
  input: {
    flex: 1,
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 8,
    padding: 10,
    fontSize: 14,
  },
  updateButton: {
    backgroundColor: '#007AFF',
    paddingHorizontal: 15,
    paddingVertical: 8,
    borderRadius: 6,
    marginLeft: 10,
  },
  updateButtonText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: '600',
  },
  notesContainer: {
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  notesInput: {
    flex: 1,
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 8,
    padding: 10,
    fontSize: 14,
    minHeight: 80,
    textAlignVertical: 'top',
  },
  itemsContainer: {
    maxHeight: 300,
  },
  orderItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderBottomColor: '#f0f0f0',
  },
  itemInfo: {
    flex: 1,
  },
  itemName: {
    fontSize: 14,
    fontWeight: '500',
    color: '#333',
  },
  itemPrice: {
    fontSize: 12,
    color: '#666',
    marginTop: 2,
  },
  itemActions: {
    flexDirection: 'row',
    alignItems: 'center',
    marginHorizontal: 15,
  },
  quantityButton: {
    padding: 5,
    backgroundColor: '#f0f0f0',
    borderRadius: 4,
  },
  quantityText: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
    marginHorizontal: 10,
    minWidth: 20,
    textAlign: 'center',
  },
  removeButton: {
    padding: 5,
    marginLeft: 10,
  },
  itemTotal: {
    fontSize: 14,
    fontWeight: '600',
    color: '#007AFF',
    minWidth: 60,
    textAlign: 'right',
  },
  totalSection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
  },
  totalRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 5,
  },
  totalLabel: {
    fontSize: 14,
    color: '#666',
  },
  totalAmount: {
    fontSize: 14,
    color: '#333',
    fontWeight: '500',
  },
  grandTotalLabel: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
  },
  grandTotalAmount: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#007AFF',
  },
  paymentButton: {
    backgroundColor: '#4CAF50',
    padding: 20,
    margin: 20,
    borderRadius: 8,
    alignItems: 'center',
  },
  paymentButtonText: {
    color: '#fff',
    fontSize: 18,
    fontWeight: 'bold',
  },
  errorContainer: {
    backgroundColor: '#ffebee',
    padding: 15,
    margin: 20,
    borderRadius: 8,
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  errorText: {
    color: '#c62828',
    fontSize: 14,
    flex: 1,
  },
  clearErrorButton: {
    backgroundColor: '#c62828',
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 4,
    marginLeft: 10,
  },
  clearErrorButtonText: {
    color: '#fff',
    fontSize: 12,
    fontWeight: '600',
  },
});
