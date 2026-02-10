import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  TextInput,
  Alert,
  ScrollView,
  FlatList,
  Modal,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { Product } from '../services/api/productService';

interface OrderItem {
  id: string;
  product: Product;
  quantity: number;
  notes: string;
  status: 'pending' | 'preparing' | 'ready' | 'served' | 'cancelled';
  createdAt: Date;
  updatedAt: Date;
}

interface Order {
  id: string;
  orderNumber: string;
  items: OrderItem[];
  customerName?: string;
  tableNumber?: string;
  status: 'pending' | 'preparing' | 'ready' | 'served' | 'cancelled';
  totalAmount: number;
  notes: string;
  userId?: string; // Siparişi oluşturan kullanıcı
  createdAt: Date;
  updatedAt: Date;
}

interface OrderManagerProps {
  visible: boolean;
  onClose: () => void;
  onOrderComplete: (order: Order) => void;
  onOrderCancel: (orderId: string) => void;
  products: Product[];
  currentUserId?: string;
  currentUserRole?: string;
}

const OrderManager: React.FC<OrderManagerProps> = ({
  visible,
  onClose,
  onOrderComplete,
  onOrderCancel,
  products,
  currentUserId,
  currentUserRole,
}) => {
  const { t } = useTranslation(['orders', 'common', 'products']);
  const [orders, setOrders] = useState<Order[]>([]);
  const [selectedOrder, setSelectedOrder] = useState<Order | null>(null);
  const [showNewOrderModal, setShowNewOrderModal] = useState(false);
  const [newOrderItems, setNewOrderItems] = useState<{ product: Product; quantity: number; notes: string }[]>([]);
  const [customerName, setCustomerName] = useState('');
  const [tableNumber, setTableNumber] = useState('');
  const [orderNotes, setOrderNotes] = useState('');

  // Sipariş durumu renkleri
  const getStatusColor = (status: string) => {
    switch (status) {
      case 'pending': return Colors.light.warning;
      case 'preparing': return Colors.light.info;
      case 'ready': return Colors.light.success;
      case 'served': return Colors.light.primary;
      case 'cancelled': return Colors.light.error;
      default: return Colors.light.textSecondary;
    }
  };

  // Sipariş durumu ikonları
  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'pending': return 'time-outline';
      case 'preparing': return 'restaurant-outline';
      case 'ready': return 'checkmark-circle-outline';
      case 'served': return 'checkmark-done-circle-outline';
      case 'cancelled': return 'close-circle-outline';
      default: return 'help-circle-outline';
    }
  };

  // Yeni sipariş oluştur
  const createNewOrder = () => {
    if (newOrderItems.length === 0) {
      Alert.alert(t('orders:error'), t('orders:no_items'));
      return;
    }

    const newOrder: Order = {
      id: Date.now().toString(),
      orderNumber: `ORD-${Date.now()}`,
      items: newOrderItems.map((item, index) => ({
        id: `${Date.now()}-${index}`,
        product: item.product,
        quantity: item.quantity,
        notes: item.notes,
        status: 'pending',
        createdAt: new Date(),
        updatedAt: new Date(),
      })),
      customerName: customerName || undefined,
      tableNumber: tableNumber || undefined,
      status: 'pending',
      totalAmount: newOrderItems.reduce((sum, item) => sum + (item.product.price * item.quantity), 0),
      notes: orderNotes,
      userId: currentUserId, // Siparişi oluşturan kullanıcıyı kaydet
      createdAt: new Date(),
      updatedAt: new Date(),
    };

    setOrders(prev => [newOrder, ...prev]);
    setShowNewOrderModal(false);
    resetNewOrderForm();
  };

  // Sipariş durumunu güncelle
  const updateOrderStatus = (orderId: string, status: Order['status']) => {
    setOrders(prev => prev.map(order => {
      if (order.id === orderId) {
        return {
          ...order,
          status,
          updatedAt: new Date(),
          items: order.items.map(item => ({
            ...item,
            status: status === 'cancelled' ? 'cancelled' : item.status,
            updatedAt: new Date(),
          })),
        };
      }
      return order;
    }));
  };

  // Sipariş öğesi durumunu güncelle
  const updateOrderItemStatus = (orderId: string, itemId: string, status: OrderItem['status']) => {
    setOrders(prev => prev.map(order => {
      if (order.id === orderId) {
        return {
          ...order,
          items: order.items.map(item => {
            if (item.id === itemId) {
              return { ...item, status, updatedAt: new Date() };
            }
            return item;
          }),
          updatedAt: new Date(),
        };
      }
      return order;
    }));
  };

  // Siparişi tamamla
  const completeOrder = (order: Order) => {
    Alert.alert(
      t('orders:complete_title'),
      t('orders:complete_message'),
      [
        { text: t('common:cancel'), style: 'cancel' },
        {
          text: t('orders:complete'),
          onPress: () => {
            updateOrderStatus(order.id, 'served');
            onOrderComplete(order);
            setSelectedOrder(null);
          },
        },
      ]
    );
  };

  // Siparişi iptal et
  const cancelOrder = (orderId: string) => {
    Alert.alert(
      t('orders:cancel_title'),
      t('orders:cancel_message'),
      [
        { text: t('common:cancel'), style: 'cancel' },
        {
          text: t('orders:cancel_confirm'),
          style: 'destructive',
          onPress: () => {
            updateOrderStatus(orderId, 'cancelled');
            onOrderCancel(orderId);
          },
        },
      ]
    );
  };

  // Yeni sipariş formunu sıfırla
  const resetNewOrderForm = () => {
    setNewOrderItems([]);
    setCustomerName('');
    setTableNumber('');
    setOrderNotes('');
  };

  // Sepete ürün ekle
  const addItemToNewOrder = (product: Product) => {
    const existingItem = newOrderItems.find(item => item.product.id === product.id);
    if (existingItem) {
      setNewOrderItems(prev => prev.map(item =>
        item.product.id === product.id
          ? { ...item, quantity: item.quantity + 1 }
          : item
      ));
    } else {
      setNewOrderItems(prev => [...prev, { product, quantity: 1, notes: '' }]);
    }
  };

  // Sepetten ürün çıkar
  const removeItemFromNewOrder = (productId: string) => {
    setNewOrderItems(prev => prev.filter(item => item.product.id !== productId));
  };

  // Ürün miktarını güncelle
  const updateItemQuantity = (productId: string, quantity: number) => {
    if (quantity <= 0) {
      removeItemFromNewOrder(productId);
    } else {
      setNewOrderItems(prev => prev.map(item =>
        item.product.id === productId
          ? { ...item, quantity }
          : item
      ));
    }
  };

  // Ürün notunu güncelle
  const updateItemNotes = (productId: string, notes: string) => {
    setNewOrderItems(prev => prev.map(item =>
      item.product.id === productId
        ? { ...item, notes }
        : item
    ));
  };

  // Sipariş listesi render
  const renderOrderItem = ({ item: order }: { item: Order }) => (
    <TouchableOpacity
      style={styles.orderCard}
      onPress={() => setSelectedOrder(order)}
    >
      <View style={styles.orderHeader}>
        <Text style={styles.orderNumber}>{order.orderNumber}</Text>
        <View style={[styles.statusBadge, { backgroundColor: getStatusColor(order.status) }]}>
          <Ionicons name={getStatusIcon(order.status) as any} size={16} color="white" />
          <Text style={styles.statusText}>{t(`orders:status.${order.status}`)}</Text>
        </View>
      </View>

      <View style={styles.orderInfo}>
        <Text style={styles.orderTime}>
          {order.createdAt.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })}
        </Text>
        {order.customerName && (
          <Text style={styles.customerName}>{order.customerName}</Text>
        )}
        {order.tableNumber && (
          <Text style={styles.tableNumber}>{t('orders:table')} {order.tableNumber}</Text>
        )}
      </View>

      <View style={styles.orderItems}>
        {order.items.slice(0, 2).map((item) => (
          <Text key={item.id} style={styles.orderItemText}>
            {item.quantity}x {item.product.name}
          </Text>
        ))}
        {order.items.length > 2 && (
          <Text style={styles.moreItemsText}>
            +{order.items.length - 2} {t('orders:more_items')}
          </Text>
        )}
      </View>

      <View style={styles.orderFooter}>
        <Text style={styles.orderTotal}>{order.totalAmount.toFixed(2)}€</Text>
        <View style={styles.orderActions}>
          {order.status === 'ready' && (
            <TouchableOpacity
              style={[styles.actionButton, styles.completeButton]}
              onPress={() => completeOrder(order)}
            >
              <Ionicons name="checkmark" size={16} color="white" />
            </TouchableOpacity>
          )}
          {order.status !== 'cancelled' && order.status !== 'served' && (
            <TouchableOpacity
              style={[styles.actionButton, styles.cancelButton]}
              onPress={() => cancelOrder(order.id)}
            >
              <Ionicons name="close" size={16} color="white" />
            </TouchableOpacity>
          )}
        </View>
      </View>
    </TouchableOpacity>
  );

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="pageSheet"
      onRequestClose={onClose}
    >
      <View style={styles.container}>
        {/* Header */}
        <View style={styles.header}>
          <Text style={styles.title}>{t('orders:manager')}</Text>
          <View style={styles.headerActions}>
            <TouchableOpacity
              style={styles.newOrderButton}
              onPress={() => setShowNewOrderModal(true)}
            >
              <Ionicons name="add" size={20} color="white" />
              <Text style={styles.newOrderButtonText}>{t('orders:new')}</Text>
            </TouchableOpacity>
            <TouchableOpacity onPress={onClose} style={styles.closeButton}>
              <Ionicons name="close" size={24} color={Colors.light.text} />
            </TouchableOpacity>
          </View>
        </View>

        {/* Sipariş Listesi */}
        <FlatList
          data={orders.filter(order => {
            // Admin tüm siparişleri görebilir
            if (currentUserRole === 'admin') {
              return order.status !== 'served';
            }
            // Normal kullanıcı sadece kendi siparişlerini görebilir
            return order.userId === currentUserId && order.status !== 'served';
          })}
          renderItem={renderOrderItem}
          keyExtractor={(item) => item.id}
          style={styles.orderList}
          showsVerticalScrollIndicator={false}
          ListEmptyComponent={
            <View style={styles.emptyState}>
              <Ionicons name="restaurant-outline" size={48} color={Colors.light.textSecondary} />
              <Text style={styles.emptyStateText}>
                {currentUserRole === 'admin' ? t('orders:no_orders') : t('orders:no_my_orders')}
              </Text>
            </View>
          }
        />

        {/* Yeni Sipariş Modal */}
        <Modal
          visible={showNewOrderModal}
          animationType="slide"
          presentationStyle="pageSheet"
        >
          <View style={styles.newOrderContainer}>
            <View style={styles.newOrderHeader}>
              <Text style={styles.newOrderTitle}>{t('orders:create_new')}</Text>
              <TouchableOpacity onPress={() => setShowNewOrderModal(false)}>
                <Ionicons name="close" size={24} color={Colors.light.text} />
              </TouchableOpacity>
            </View>

            <ScrollView style={styles.newOrderContent}>
              {/* Müşteri Bilgileri */}
              <View style={styles.customerSection}>
                <Text style={styles.sectionTitle}>{t('orders:customer_info')}</Text>
                <TextInput
                  style={styles.input}
                  placeholder={t('orders:customer_name_placeholder')}
                  value={customerName}
                  onChangeText={setCustomerName}
                />
                <TextInput
                  style={styles.input}
                  placeholder={t('orders:table_number_placeholder')}
                  value={tableNumber}
                  onChangeText={setTableNumber}
                  keyboardType="numeric"
                />
              </View>

              {/* Ürün Seçimi */}
              <View style={styles.productsSection}>
                <Text style={styles.sectionTitle}>{t('orders:select_products')}</Text>
                <View style={styles.productsGrid}>
                  {products.map((product) => (
                    <TouchableOpacity
                      key={product.id}
                      style={styles.productButton}
                      onPress={() => addItemToNewOrder(product)}
                    >
                      <Text style={styles.productButtonText}>{product.name}</Text>
                      <Text style={styles.productButtonPrice}>{product.price.toFixed(2)}€</Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </View>

              {/* Seçilen Ürünler */}
              {newOrderItems.length > 0 && (
                <View style={styles.selectedItemsSection}>
                  <Text style={styles.sectionTitle}>{t('orders:selected_items')}</Text>
                  {newOrderItems.map((item, index) => (
                    <View key={`${item.product.id}-${index}`} style={styles.selectedItem}>
                      <View style={styles.selectedItemInfo}>
                        <Text style={styles.selectedItemName}>{item.product.name}</Text>
                        <Text style={styles.selectedItemPrice}>
                          {(item.product.price * item.quantity).toFixed(2)}€
                        </Text>
                      </View>
                      <View style={styles.selectedItemActions}>
                        <TouchableOpacity
                          style={styles.quantityButton}
                          onPress={() => updateItemQuantity(item.product.id, item.quantity - 1)}
                        >
                          <Ionicons name="remove" size={16} color={Colors.light.text} />
                        </TouchableOpacity>
                        <Text style={styles.quantityText}>{item.quantity}</Text>
                        <TouchableOpacity
                          style={styles.quantityButton}
                          onPress={() => updateItemQuantity(item.product.id, item.quantity + 1)}
                        >
                          <Ionicons name="add" size={16} color={Colors.light.text} />
                        </TouchableOpacity>
                      </View>
                      <TextInput
                        style={styles.notesInput}
                        placeholder={t('orders:item_notes_placeholder')}
                        value={item.notes}
                        onChangeText={(notes) => updateItemNotes(item.product.id, notes)}
                      />
                    </View>
                  ))}
                </View>
              )}

              {/* Sipariş Notları */}
              <View style={styles.notesSection}>
                <Text style={styles.sectionTitle}>{t('orders:order_notes')}</Text>
                <TextInput
                  style={[styles.input, styles.notesTextArea]}
                  placeholder={t('orders:order_notes_placeholder')}
                  value={orderNotes}
                  onChangeText={setOrderNotes}
                  multiline
                  numberOfLines={3}
                />
              </View>
            </ScrollView>

            {/* Onay Butonu */}
            <View style={styles.newOrderFooter}>
              <TouchableOpacity
                style={[styles.createOrderButton, newOrderItems.length === 0 && styles.createOrderButtonDisabled]}
                onPress={createNewOrder}
                disabled={newOrderItems.length === 0}
              >
                <Ionicons name="checkmark" size={20} color="white" />
                <Text style={styles.createOrderButtonText}>{t('orders:create')}</Text>
              </TouchableOpacity>
            </View>
          </View>
        </Modal>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  title: {
    ...Typography.h2,
    color: Colors.light.text,
  },
  headerActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
  },
  newOrderButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.light.primary,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    borderRadius: BorderRadius.md,
    gap: Spacing.xs,
  },
  newOrderButtonText: {
    ...Typography.button,
    color: 'white',
  },
  closeButton: {
    padding: Spacing.xs,
  },
  orderList: {
    flex: 1,
    padding: Spacing.md,
  },
  orderCard: {
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.lg,
    padding: Spacing.md,
    marginBottom: Spacing.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  orderHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.sm,
  },
  orderNumber: {
    ...Typography.h3,
    color: Colors.light.text,
  },
  statusBadge: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: Spacing.sm,
    paddingVertical: Spacing.xs,
    borderRadius: BorderRadius.sm,
    gap: Spacing.xs,
  },
  statusText: {
    ...Typography.caption,
    color: 'white',
    fontWeight: 'bold',
  },
  orderInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.md,
    marginBottom: Spacing.sm,
  },
  orderTime: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
  },
  customerName: {
    ...Typography.bodySmall,
    color: Colors.light.text,
    fontWeight: '600',
  },
  tableNumber: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
  },
  orderItems: {
    marginBottom: Spacing.sm,
  },
  orderItemText: {
    ...Typography.bodySmall,
    color: Colors.light.text,
  },
  moreItemsText: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    fontStyle: 'italic',
  },
  orderFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  orderTotal: {
    ...Typography.h3,
    color: Colors.light.text,
    fontWeight: 'bold',
  },
  orderActions: {
    flexDirection: 'row',
    gap: Spacing.xs,
  },
  actionButton: {
    width: 32,
    height: 32,
    borderRadius: BorderRadius.sm,
    alignItems: 'center',
    justifyContent: 'center',
  },
  completeButton: {
    backgroundColor: Colors.light.success,
  },
  cancelButton: {
    backgroundColor: Colors.light.error,
  },
  emptyState: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: Spacing.xxl,
  },
  emptyStateText: {
    ...Typography.body,
    color: Colors.light.textSecondary,
    marginTop: Spacing.md,
  },
  newOrderContainer: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  newOrderHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  newOrderTitle: {
    ...Typography.h2,
    color: Colors.light.text,
  },
  newOrderContent: {
    flex: 1,
    padding: Spacing.md,
  },
  newOrderFooter: {
    padding: Spacing.md,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  customerSection: {
    marginBottom: Spacing.lg,
  },
  productsSection: {
    marginBottom: Spacing.lg,
  },
  selectedItemsSection: {
    marginBottom: Spacing.lg,
  },
  notesSection: {
    marginBottom: Spacing.lg,
  },
  sectionTitle: {
    ...Typography.h3,
    color: Colors.light.text,
    marginBottom: Spacing.sm,
  },
  input: {
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    marginBottom: Spacing.sm,
    fontSize: 16,
    color: Colors.light.text,
  },
  notesTextArea: {
    height: 80,
    textAlignVertical: 'top',
  },
  productsGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: Spacing.sm,
  },
  productButton: {
    flex: 1,
    minWidth: '45%',
    padding: Spacing.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.md,
    alignItems: 'center',
  },
  productButtonText: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '600',
    textAlign: 'center',
  },
  productButtonPrice: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
    marginTop: Spacing.xs,
  },
  selectedItem: {
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    marginBottom: Spacing.sm,
  },
  selectedItemInfo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.sm,
  },
  selectedItemName: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '600',
  },
  selectedItemPrice: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: 'bold',
  },
  selectedItemActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
    marginBottom: Spacing.sm,
  },
  quantityButton: {
    width: 32,
    height: 32,
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.sm,
    alignItems: 'center',
    justifyContent: 'center',
  },
  quantityText: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: 'bold',
    minWidth: 30,
    textAlign: 'center',
  },
  notesInput: {
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.sm,
    padding: Spacing.sm,
    fontSize: 14,
    color: Colors.light.text,
  },
  createOrderButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.primary,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    gap: Spacing.sm,
  },
  createOrderButtonDisabled: {
    backgroundColor: '#CCCCCC',
  },
  createOrderButtonText: {
    ...Typography.button,
    color: 'white',
    fontWeight: 'bold',
  },
});

export default OrderManager; 