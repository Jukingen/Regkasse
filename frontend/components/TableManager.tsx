import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  Modal,
  Alert,
  Vibration,
  TextInput,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';
import { useTableOrdersRecoveryOptimized } from '../hooks/useTableOrdersRecoveryOptimized';

interface TableOrder {
  id: string;
  tableNumber: string;
  items: {
    productId: string;
    productName: string;
    quantity: number;
    price: number;
    notes?: string;
  }[];
  total: number;
  status: 'pending' | 'preparing' | 'ready' | 'served';
  createdAt: Date;
  customerName?: string;
  notes?: string;
}

interface Table {
  number: string;
  status: 'empty' | 'occupied' | 'reserved' | 'cleaning';
  currentOrder?: TableOrder;
  customerName?: string;
  startTime?: Date;
  lastOrderTime?: Date;
  totalPaid?: number;
  orderHistory?: TableOrder[];
}

interface TableManagerProps {
  visible: boolean;
  onClose: () => void;
  onTableSelect: (tableNumber: string, tableOrder?: TableOrder) => void;
  onOrderComplete: (orderId: string) => void;
  selectedTable?: string | null;
  tableOrders?: { [tableNumber: string]: any[] }; // Güncel sipariş verileri
}

const TableManager: React.FC<TableManagerProps> = ({
  visible,
  onClose,
  onTableSelect,
  onOrderComplete,
  selectedTable,
  tableOrders = {},
}) => {
  const { t: _t } = useTranslation();
  const {
    isRecoveryCompleted,
    recoveryData,
    getOrderForTable,
    hasActiveOrders,
  } = useTableOrdersRecoveryOptimized();
  const [tables, setTables] = useState<Table[]>([]);
  const [showCustomerInput, setShowCustomerInput] = useState(false);
  const [editingTable, setEditingTable] = useState<string | null>(null);
  const [customerName, setCustomerName] = useState('');

  // Basit masa durumu yönetimi
  useEffect(() => {
    const demoTables: Table[] = [
      { number: '1', status: 'empty' },
      { number: '2', status: 'empty' },
      { number: '3', status: 'occupied', customerName: 'Maria Schmidt', startTime: new Date(Date.now() - 15 * 60 * 1000) },
      { number: '4', status: 'reserved', customerName: 'Reserviert' },
      { number: '5', status: 'empty' },
      { number: '6', status: 'occupied', customerName: 'Peter Weber', startTime: new Date(Date.now() - 45 * 60 * 1000) },
      { number: '7', status: 'empty' },
      { number: '8', status: 'occupied', customerName: 'Anna Fischer', startTime: new Date(Date.now() - 20 * 60 * 1000) },
      { number: '9', status: 'empty' },
      { number: '10', status: 'reserved', customerName: 'Reserviert' },
    ];

    // Masaları güncel siparişlerle güncelle - önce local tableOrders, sonra recovery data
    const tablesWithOrders = demoTables.map(table => {
      const currentTableOrders = tableOrders[table.number] || [];
      const tableNum = parseInt(table.number, 10);
      
      // Local sipariş varsa o öncelikli
      if (currentTableOrders.length > 0) {
        // Güncel siparişlerden toplam hesapla
        const total = currentTableOrders.reduce((sum, item) => {
          const price = item.product?.price ?? item.price;
          return sum + (price * item.quantity);
        }, 0);

        console.log(`Masa ${table.number} - Toplam: €${total.toFixed(2)}, Ürün sayısı: ${currentTableOrders.length}`);

        const updatedOrder: TableOrder = {
          id: `order-${table.number}`,
          tableNumber: table.number,
          items: currentTableOrders.map((item: any) => ({
            productId: item.product?.id ?? item.productId,
            productName: item.product?.name ?? item.productName,
            quantity: item.quantity,
            price: item.product?.price ?? item.price,
            notes: item.notes
          })),
          total,
          status: 'pending',
          createdAt: table.startTime || new Date(),
          customerName: table.customerName,
          notes: 'Aktualisiert',
        };
        
        return { 
          ...table, 
          currentOrder: updatedOrder,
          status: 'occupied' as const
        };
      }
      
      // Recovery data'dan sipariş varsa onu kullan (F5 sonrası)
      if (isRecoveryCompleted && recoveryData) {
        const recoveryOrder = getOrderForTable(tableNum);
        if (recoveryOrder && recoveryOrder.itemCount > 0) {
          console.log(`🔄 Recovery: Masa ${table.number} - Toplam: €${recoveryOrder.totalAmount.toFixed(2)}, Ürün sayısı: ${recoveryOrder.itemCount}`);
          
          const currentOrder: TableOrder = {
            id: recoveryOrder.cartId,
            tableNumber: table.number,
            items: recoveryOrder.items.map((item: { productId: string; productName: string; quantity: number; price: number; notes?: string }) => ({
              productId: item.productId,
              productName: item.productName,
              quantity: item.quantity,
              price: item.price,
              notes: item.notes,
            })),
            total: recoveryOrder.totalAmount,
            status: 'pending', // Status mapping yapılabilir
            createdAt: new Date(recoveryOrder.createdAt),
            customerName: recoveryOrder.customerName || table.customerName,
            notes: 'Wiederhergestellt nach F5',
          };

          return {
            ...table,
            status: 'occupied' as const,
            currentOrder,
            customerName: recoveryOrder.customerName || table.customerName,
          };
        }
      }
      
      // Boş masa
      return { 
        ...table, 
        currentOrder: undefined,
        status: 'empty' as const
      };
    });

    setTables(tablesWithOrders);
    
    // Recovery tamamlandığında kullanıcıya bildirim göster
    if (isRecoveryCompleted && hasActiveOrders) {
      console.log(`✅ Recovery completed: ${recoveryData?.totalActiveTables} active table orders restored`);
    }
  }, [tableOrders, isRecoveryCompleted, recoveryData, getOrderForTable, hasActiveOrders]);

  const getTableStatusColor = (status: string) => {
    switch (status) {
      case 'empty': return Colors.light.success;
      case 'occupied': return Colors.light.warning;
      case 'reserved': return Colors.light.info;
      case 'cleaning': return Colors.light.textSecondary;
      default: return Colors.light.textSecondary;
    }
  };

  const getTableStatusText = (status: string) => {
    switch (status) {
      case 'empty': return 'Frei';
      case 'occupied': return 'Besetzt';
      case 'reserved': return 'Reserviert';
      case 'cleaning': return 'Reinigung';
      default: return 'Unbekannt';
    }
  };

  const getOrderStatusColor = (status: string) => {
    switch (status) {
      case 'pending': return Colors.light.warning;
      case 'preparing': return Colors.light.info;
      case 'ready': return Colors.light.success;
      case 'served': return Colors.light.textSecondary;
      default: return Colors.light.textSecondary;
    }
  };

  const getOrderStatusText = (status: string) => {
    switch (status) {
      case 'pending': return 'Wartend';
      case 'preparing': return 'Zubereitung';
      case 'ready': return 'Bereit';
      case 'served': return 'Serviert';
      default: return 'Unbekannt';
    }
  };

  const handleTablePress = (table: Table) => {
    Vibration.vibrate(25);
    
    // Eğer masada güncel sipariş varsa onu kullan, yoksa tableOrders'dan al
    if (table.currentOrder) {
      onTableSelect(table.number, table.currentOrder);
    } else {
      // tableOrders'dan güncel siparişleri al
      const currentTableOrders = tableOrders[table.number] || [];
      if (currentTableOrders.length > 0) {
        // CartItem formatından TableOrder formatına dönüştür
        const total = currentTableOrders.reduce((sum, item) => {
          const price = item.product ? item.product.price : item.price;
          return sum + (price * item.quantity);
        }, 0);

        const tableOrder: TableOrder = {
          id: `order-${table.number}`,
          tableNumber: table.number,
          items: currentTableOrders.map(item => ({
            productId: item.product ? item.product.id : item.productId,
            productName: item.product ? item.product.name : item.productName,
            quantity: item.quantity,
            price: item.product ? item.product.price : item.price,
            notes: item.notes
          })),
          total,
          status: 'pending',
          createdAt: table.startTime || new Date(),
          customerName: table.customerName,
          notes: 'Aktualisiert',
        };
        
        onTableSelect(table.number, tableOrder);
      } else {
        onTableSelect(table.number, undefined);
      }
    }
  };

  const handleQuickAction = (action: string, tableNumber: string) => {
    Vibration.vibrate(25);
    
    switch (action) {
      case 'clear':
        Alert.alert(
          'Masa Temizle',
          `Tisch ${tableNumber} temizlendi olarak işaretlemek istediğinizden emin misiniz?`,
          [
            { text: 'İptal', style: 'cancel' },
            {
              text: 'Temizle',
              onPress: () => {
                setTables(prev => prev.map(table => 
                  table.number === tableNumber 
                    ? { 
                        ...table, 
                        status: 'cleaning', 
                        customerName: undefined, 
                        startTime: undefined, 
                        currentOrder: undefined,
                        lastOrderTime: table.lastOrderTime,
                        totalPaid: table.totalPaid,
                        orderHistory: table.orderHistory
                      }
                    : table
                ));
                
                // 5 saniye sonra masayı boş olarak işaretle
                setTimeout(() => {
                  setTables(prev => prev.map(table => 
                    table.number === tableNumber 
                      ? { ...table, status: 'empty' }
                      : table
                  ));
                }, 5000);
              }
            }
          ]
        );
        break;
        
      case 'reserve':
        Alert.alert(
          'Rezervasyon',
          `Tisch ${tableNumber} için rezervasyon eklemek istediğinizden emin misiniz?`,
          [
            { text: 'İptal', style: 'cancel' },
            {
              text: 'Rezerve Et',
              onPress: () => {
                setTables(prev => prev.map(table => 
                  table.number === tableNumber 
                    ? { ...table, status: 'reserved', customerName: 'Reserviert' }
                    : table
                ));
              }
            }
          ]
        );
        break;
        
      case 'customer':
        setEditingTable(tableNumber);
        setCustomerName('');
        setShowCustomerInput(true);
        break;
        
      case 'status':
        Alert.alert(
          'Sipariş Durumu',
          `Tisch ${tableNumber} sipariş durumunu seçin:`,
          [
            { text: 'İptal', style: 'cancel' },
            { text: 'Wartend', onPress: () => updateOrderStatus(tableNumber, 'pending') },
            { text: 'Zubereitung', onPress: () => updateOrderStatus(tableNumber, 'preparing') },
            { text: 'Bereit', onPress: () => updateOrderStatus(tableNumber, 'ready') },
            { text: 'Serviert', onPress: () => updateOrderStatus(tableNumber, 'served') },
          ]
        );
        break;
    }
  };

  const updateOrderStatus = (tableNumber: string, status: 'pending' | 'preparing' | 'ready' | 'served') => {
    setTables(prev => prev.map(table => 
      table.number === tableNumber && table.currentOrder
        ? { 
            ...table, 
            currentOrder: { 
              ...table.currentOrder, 
              status 
            } 
          }
        : table
    ));
  };

  const handleOrderComplete = (orderId: string) => {
    Alert.alert(
      'Sipariş Tamamlandı',
      'Bu siparişi tamamlandı olarak işaretlemek istediğinizden emin misiniz?',
      [
        { text: 'İptal', style: 'cancel' },
        {
          text: 'Tamamla',
          onPress: () => {
            onOrderComplete(orderId);
            Vibration.vibrate(25);
          }
        }
      ]
    );
  };

  const formatTime = (date: Date) => {
    return date.toLocaleTimeString('de-DE', { 
      hour: '2-digit', 
      minute: '2-digit' 
    });
  };

  const getElapsedTime = (startTime: Date) => {
    const elapsed = Date.now() - startTime.getTime();
    const minutes = Math.floor(elapsed / (1000 * 60));
    return `${minutes} Min`;
  };

  const getLastOrderTime = (lastOrderTime: Date) => {
    const elapsed = Date.now() - lastOrderTime.getTime();
    const minutes = Math.floor(elapsed / (1000 * 60));
    return `${minutes} Min`;
  };

  // Müşteri adı kaydetme işlemi
  const handleSaveCustomerName = () => {
    if (customerName && customerName.trim() && editingTable) {
      setTables(prev => prev.map(table => 
        table.number === editingTable 
          ? { ...table, customerName: customerName.trim() }
          : table
      ));
    }
    setShowCustomerInput(false);
    setEditingTable(null);
    setCustomerName('');
  };

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
          <Text style={styles.title}>Tisch Verwaltung</Text>
          <TouchableOpacity style={styles.closeButton} onPress={onClose}>
            <Ionicons name="close" size={24} color={Colors.light.text} />
          </TouchableOpacity>
        </View>

        {/* Masa Grid */}
        <ScrollView style={styles.content}>
          <View style={styles.tableGrid}>
            {tables.map((table) => (
              <View
                key={table.number}
                style={[
                  styles.tableCard,
                  { borderColor: getTableStatusColor(table.status) },
                  selectedTable === table.number && styles.selectedTableCard
                ]}
              >
                <TouchableOpacity
                  style={styles.tableContent}
                  onPress={() => handleTablePress(table)}
                >
                  <View style={styles.tableHeader}>
                    <Text style={styles.tableNumber}>Tisch {table.number}</Text>
                    <View style={[
                      styles.statusIndicator,
                      { backgroundColor: getTableStatusColor(table.status) }
                    ]} />
                  </View>
                  
                  <Text style={styles.tableStatus}>
                    {getTableStatusText(table.status)}
                  </Text>
                  
                  {table.customerName && (
                    <Text style={styles.customerName} numberOfLines={1}>
                      {table.customerName}
                    </Text>
                  )}
                  
                  {table.startTime && (
                    <Text style={styles.startTime}>
                      {formatTime(table.startTime)} ({getElapsedTime(table.startTime)})
                    </Text>
                  )}
                  
                  {table.lastOrderTime && (
                    <Text style={styles.lastOrderTime}>
                      Letzte Bestellung: {formatTime(table.lastOrderTime)} ({getLastOrderTime(table.lastOrderTime)})
                    </Text>
                  )}
                  
                  {table.totalPaid && table.totalPaid > 0 && (
                    <Text style={styles.totalPaid}>
                      Gesamt bezahlt: €{table.totalPaid.toFixed(2)}
                    </Text>
                  )}
                  
                  {table.currentOrder && (
                    <View style={styles.tableOrderInfo}>
                      <Text style={styles.tableOrderTotal}>
                        €{table.currentOrder.total.toFixed(2)}
                      </Text>
                      <View style={[
                        styles.orderStatus,
                        { backgroundColor: getOrderStatusColor(table.currentOrder.status) }
                      ]}>
                        <Text style={styles.orderStatusText}>
                          {getOrderStatusText(table.currentOrder.status)}
                        </Text>
                      </View>
                    </View>
                  )}
                </TouchableOpacity>

                {/* Hızlı Aksiyon Butonları */}
                <View style={styles.quickActions}>
                  {/* Masa Temizle */}
                  {table.status === 'occupied' && (
                    <TouchableOpacity
                      style={[styles.quickActionButton, styles.clearButton]}
                      onPress={() => handleQuickAction('clear', table.number)}
                    >
                      <Ionicons name="trash-outline" size={16} color="white" />
                    </TouchableOpacity>
                  )}

                  {/* Rezervasyon */}
                  {table.status === 'empty' && (
                    <TouchableOpacity
                      style={[styles.quickActionButton, styles.reserveButton]}
                      onPress={() => handleQuickAction('reserve', table.number)}
                    >
                      <Ionicons name="calendar-outline" size={16} color="white" />
                    </TouchableOpacity>
                  )}

                  {/* Müşteri Adı */}
                  {table.status === 'occupied' && (
                    <TouchableOpacity
                      style={[styles.quickActionButton, styles.customerButton]}
                      onPress={() => handleQuickAction('customer', table.number)}
                    >
                      <Ionicons name="person-outline" size={16} color="white" />
                    </TouchableOpacity>
                  )}

                  {/* Sipariş Durumu */}
                  {table.currentOrder && (
                    <TouchableOpacity
                      style={[styles.quickActionButton, styles.statusButton]}
                      onPress={() => handleQuickAction('status', table.number)}
                    >
                      <Ionicons name="time-outline" size={16} color="white" />
                    </TouchableOpacity>
                  )}
                </View>
              </View>
            ))}
          </View>
        </ScrollView>

        {/* Müşteri Adı Giriş Modal */}
        <Modal
          visible={showCustomerInput}
          transparent
          animationType="fade"
          onRequestClose={() => setShowCustomerInput(false)}
        >
          <View style={styles.customerInputOverlay}>
            <View style={styles.customerInputContainer}>
              <Text style={styles.customerInputTitle}>
                Tisch {editingTable} - Müşteri Adı
              </Text>
              
              <TextInput
                style={styles.customerInputField}
                placeholder="Müşteri adını girin..."
                value={customerName}
                onChangeText={setCustomerName}
                autoFocus
              />
              
              <View style={styles.customerInputActions}>
                <TouchableOpacity
                  style={[styles.customerInputButton, styles.cancelButton]}
                  onPress={() => {
                    setShowCustomerInput(false);
                    setEditingTable(null);
                    setCustomerName('');
                  }}
                >
                  <Text style={styles.cancelButtonText}>İptal</Text>
                </TouchableOpacity>
                
                <TouchableOpacity
                  style={[styles.customerInputButton, styles.saveButton]}
                  onPress={handleSaveCustomerName}
                >
                  <Text style={styles.saveButtonText}>Kaydet</Text>
                </TouchableOpacity>
              </View>
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
    padding: Spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
    backgroundColor: Colors.light.surface,
  },
  title: {
    ...Typography.h2,
    color: Colors.light.text,
  },
  closeButton: {
    padding: Spacing.xs,
  },
  content: {
    flex: 1,
    padding: Spacing.md,
  },
  tableGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
    gap: Spacing.md,
  },
  tableCard: {
    width: '48%',
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    borderWidth: 2,
    minHeight: 140,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 2,
    },
    shadowOpacity: 0.1,
    shadowRadius: 3.84,
    elevation: 3,
  },
  tableHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.sm,
  },
  tableNumber: {
    ...Typography.h3,
    color: Colors.light.text,
    fontWeight: '600',
  },
  statusIndicator: {
    width: 12,
    height: 12,
    borderRadius: 6,
  },
  tableStatus: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    marginBottom: Spacing.xs,
  },
  customerName: {
    ...Typography.bodySmall,
    color: Colors.light.text,
    fontWeight: '500',
    marginBottom: Spacing.xs,
  },
  startTime: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    marginBottom: Spacing.sm,
  },
  tableOrderInfo: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  tableOrderTotal: {
    ...Typography.bodySmall,
    color: Colors.light.primary,
    fontWeight: '600',
  },
  orderStatus: {
    paddingHorizontal: Spacing.xs,
    paddingVertical: 2,
    borderRadius: BorderRadius.sm,
  },
  orderStatusText: {
    ...Typography.caption,
    color: 'white',
    fontSize: 10,
    fontWeight: '600',
  },
  orderDetailsContainer: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  orderDetailsHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
    backgroundColor: Colors.light.surface,
  },
  orderDetailsTitle: {
    ...Typography.h2,
    color: Colors.light.text,
  },
  orderDetailsContent: {
    flex: 1,
    padding: Spacing.lg,
  },
  orderInfo: {
    backgroundColor: Colors.light.surface,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.lg,
  },
  orderInfoText: {
    ...Typography.body,
    color: Colors.light.text,
    marginBottom: Spacing.xs,
  },
  orderInfoLabel: {
    fontWeight: '600',
    color: Colors.light.textSecondary,
  },
  orderItems: {
    backgroundColor: Colors.light.surface,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.lg,
  },
  orderItemsTitle: {
    ...Typography.h3,
    color: Colors.light.text,
    marginBottom: Spacing.md,
  },
  orderItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: Spacing.xs,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  orderItemInfo: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
  },
  orderItemName: {
    ...Typography.body,
    color: Colors.light.text,
    flex: 1,
  },
  orderItemQuantity: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
    marginLeft: Spacing.sm,
  },
  orderItemPrice: {
    ...Typography.body,
    color: Colors.light.primary,
    fontWeight: '600',
  },
  orderDetailsTotal: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    backgroundColor: Colors.light.primary + '20',
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.lg,
  },
  orderTotalLabel: {
    ...Typography.h3,
    color: Colors.light.text,
  },
  orderTotalAmount: {
    ...Typography.h2,
    color: Colors.light.primary,
    fontWeight: 'bold',
  },
  completeOrderButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.success,
    padding: Spacing.lg,
    borderRadius: BorderRadius.md,
    gap: Spacing.sm,
  },
  completeOrderButtonText: {
    ...Typography.button,
    color: 'white',
    fontWeight: '600',
  },
  selectedTableCard: {
    borderWidth: 3,
    backgroundColor: Colors.light.primary + '10',
    shadowOpacity: 0.3,
    elevation: 8,
  },
  tableContent: {
    flex: 1,
  },
  quickActions: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    paddingTop: Spacing.sm,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
    marginTop: Spacing.sm,
  },
  quickActionButton: {
    width: 32,
    height: 32,
    borderRadius: 16,
    justifyContent: 'center',
    alignItems: 'center',
    marginHorizontal: 2,
  },
  clearButton: {
    backgroundColor: Colors.light.error,
  },
  reserveButton: {
    backgroundColor: Colors.light.info,
  },
  customerButton: {
    backgroundColor: Colors.light.primary,
  },
  statusButton: {
    backgroundColor: Colors.light.warning,
  },
  customerInputOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  customerInputContainer: {
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.lg,
    padding: Spacing.lg,
    width: '80%',
    maxWidth: 400,
  },
  customerInputTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: Colors.light.text,
    marginBottom: Spacing.md,
    textAlign: 'center',
  },
  customerInputField: {
    borderWidth: 1,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.md,
    padding: Spacing.md,
    fontSize: 16,
    color: Colors.light.text,
    backgroundColor: Colors.light.background,
    marginBottom: Spacing.lg,
  },
  customerInputActions: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: Spacing.md,
  },
  customerInputButton: {
    flex: 1,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    alignItems: 'center',
  },
  cancelButton: {
    backgroundColor: Colors.light.error,
  },
  saveButton: {
    backgroundColor: Colors.light.primary,
  },
  cancelButtonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: '600',
  },
  saveButtonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: '600',
  },
  lastOrderTime: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    fontSize: 10,
    marginTop: Spacing.xs,
  },
  totalPaid: {
    ...Typography.caption,
    color: Colors.light.success,
    fontSize: 10,
    fontWeight: '600',
    marginTop: Spacing.xs,
  },
});

export default TableManager; 