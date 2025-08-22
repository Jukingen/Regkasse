// Türkçe Açıklama: Masa seçimi ve masa durumu gösterimi için ayrı component
// Karmaşık cash-register.tsx dosyasından masa seçimi logic'ini ayırır

import React from 'react';
import { View, Text, TouchableOpacity, ScrollView, StyleSheet } from 'react-native';
import { Vibration } from 'react-native';

interface TableSelectorProps {
  selectedTable: number;
  onTableSelect: (tableNumber: number) => void;
  tableCarts: Map<number, any>;
  recoveryData: any;
  tableSelectionLoading: number | null;
  onClearAllTables: () => void;
}

export const TableSelector: React.FC<TableSelectorProps> = ({
  selectedTable,
  onTableSelect,
  tableCarts,
  recoveryData,
  tableSelectionLoading,
  onClearAllTables,
}) => {
  const tableNumbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

  const handleTablePress = (tableNumber: number) => {
    // Loading state kontrolü
    if (tableSelectionLoading !== null) {
      return;
    }

    // Aynı masaya tıklanırsa işlem yapma
    if (selectedTable === tableNumber) {
      return;
    }

    // Haptic feedback
    Vibration.vibrate(30);
    
    // Masa seçimi
    onTableSelect(tableNumber);
  };

  const getTableItemCount = (tableNumber: number): number => {
    const tableCart = tableCarts.get(tableNumber);
    const hasItems = tableCart && tableCart.items && tableCart.items.length > 0;
    
    // Recovery data'dan masa için ürün sayısını al
    const recoveryOrder = recoveryData?.tableOrders?.find(
      (order: any) => order.tableNumber === tableNumber
    );
    const recoveryItemCount = recoveryOrder?.itemCount || 0;
    
    // Hem local cart hem de recovery data'dan ürün sayısını kontrol et
    return hasItems ? (tableCart?.totalItems || tableCart?.items?.length || 0) : recoveryItemCount;
  };

  return (
    <View style={styles.tableSection}>
      <Text style={styles.sectionTitle}>Select Table</Text>
      <ScrollView 
        horizontal 
        showsHorizontalScrollIndicator={false} 
        style={styles.tableScroll}
        contentContainerStyle={styles.tableScrollContent}
        bounces={false}
        decelerationRate="fast"
        scrollEventThrottle={16}
        keyboardShouldPersistTaps="handled"
        nestedScrollEnabled={false}
      >
        {tableNumbers.map((tableNumber) => {
          const itemCount = getTableItemCount(tableNumber);
          const shouldShowItemCount = itemCount > 0;
          const isLoading = tableSelectionLoading === tableNumber;
          
          return (
            <TouchableOpacity
              key={tableNumber}
              style={[
                styles.tableTab,
                selectedTable === tableNumber && styles.selectedTableTab,
                shouldShowItemCount && styles.tableTabWithItems,
                isLoading && styles.tableTabLoading
              ]}
              onPress={() => handleTablePress(tableNumber)}
              activeOpacity={0.7}
              hitSlop={{ top: 20, bottom: 20, left: 20, right: 20 }}
              delayPressIn={0}
              delayPressOut={0}
              disabled={isLoading}
              accessible
              accessibilityLabel={`Table ${tableNumber}`}
              accessibilityHint={`Select table ${tableNumber}`}
              accessibilityRole="button"
              accessibilityState={{ 
                selected: selectedTable === tableNumber,
                disabled: isLoading
              }}
            >
              <Text style={[
                styles.tableTabText,
                selectedTable === tableNumber && styles.selectedTableTabText,
                shouldShowItemCount && styles.tableTabTextWithItems,
                isLoading && styles.tableTabTextLoading
              ]}>
                {isLoading ? '...' : tableNumber}
              </Text>
              {shouldShowItemCount && (
                <View style={styles.tableItemIndicator}>
                  <Text style={styles.tableItemIndicatorText}>{itemCount}</Text>
                </View>
              )}
            </TouchableOpacity>
          );
        })}
        
        {/* Clear All Tables Button */}
        <TouchableOpacity
          style={styles.clearAllTablesButton}
          onPress={onClearAllTables}
          activeOpacity={0.7}
          accessible
          accessibilityLabel="Clear All Tables"
          accessibilityHint="Clear all items from all tables - DANGEROUS!"
          accessibilityRole="button"
        >
          <Text style={styles.clearAllTablesIcon}>🧹</Text>
          <Text style={styles.clearAllTablesText}>Clear{'\n'}ALL</Text>
        </TouchableOpacity>
      </ScrollView>
    </View>
  );
};

const styles = StyleSheet.create({
  tableSection: {
    backgroundColor: '#fff',
    padding: 20,
    marginBottom: 10,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 15,
    color: '#333',
  },
  tableScroll: {
    flexDirection: 'row',
  },
  tableScrollContent: {
    alignItems: 'center',
  },
  tableTab: {
    paddingHorizontal: 20,
    paddingVertical: 12,
    marginRight: 10,
    borderRadius: 8,
    backgroundColor: '#f0f0f0',
    borderWidth: 2,
    borderColor: 'transparent',
    minWidth: 60,
    minHeight: 50,
    alignItems: 'center',
    justifyContent: 'center',
    elevation: 3,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.15,
    shadowRadius: 4,
    zIndex: 10,
    transform: [{ scale: 1 }],
    overflow: 'visible',
  },
  selectedTableTab: {
    backgroundColor: '#2196F3',
    borderColor: '#1976D2',
    elevation: 6,
    shadowOpacity: 0.25,
    zIndex: 15,
    transform: [{ scale: 1.05 }],
    overflow: 'visible',
  },
  tableTabWithItems: {
    borderColor: '#4CAF50',
    borderWidth: 2,
    elevation: 4,
    zIndex: 12,
    transform: [{ scale: 1.02 }],
    overflow: 'visible',
  },
  tableTabLoading: {
    backgroundColor: '#e0e0e0',
    borderColor: '#ccc',
    borderWidth: 2,
    opacity: 0.8,
    transform: [{ scale: 0.98 }],
    elevation: 2,
    zIndex: 8,
    overflow: 'visible',
  },
  tableTabText: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#666',
  },
  selectedTableTabText: {
    color: '#fff',
  },
  tableTabTextWithItems: {
    color: '#4CAF50',
  },
  tableTabTextLoading: {
    color: '#999',
  },
  tableItemIndicator: {
    position: 'absolute',
    top: -5,
    right: -5,
    backgroundColor: '#FF9800',
    borderRadius: 10,
    paddingHorizontal: 5,
    paddingVertical: 2,
    minWidth: 20,
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 20,
    elevation: 8,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.4,
    shadowRadius: 3,
    overflow: 'visible',
  },
  tableItemIndicatorText: {
    color: '#fff',
    fontSize: 10,
    fontWeight: 'bold',
  },
  clearAllTablesButton: {
    paddingHorizontal: 15,
    paddingVertical: 12,
    marginLeft: 20,
    borderRadius: 8,
    backgroundColor: '#ffebee',
    borderWidth: 2,
    borderColor: '#f44336',
    alignItems: 'center',
    justifyContent: 'center',
    minWidth: 80,
    minHeight: 50,
    elevation: 4,
    shadowColor: '#f44336',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.3,
    shadowRadius: 4,
    zIndex: 10,
  },
  clearAllTablesIcon: {
    fontSize: 20,
    marginBottom: 2,
  },
  clearAllTablesText: {
    fontSize: 11,
    fontWeight: 'bold',
    color: '#f44336',
    textAlign: 'center',
    lineHeight: 12,
  },
});
