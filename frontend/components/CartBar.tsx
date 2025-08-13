// Bu komponent, sadece backend'den gelen sepet ve hesaplama verilerini gösterir. Local hesaplama yapılmaz.
import { Ionicons } from '@expo/vector-icons';
import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, ActivityIndicator, ScrollView } from 'react-native';
import { useTranslation } from 'react-i18next';

// useCashRegister hook'undan gelen Cart tipi ile uyumlu interface
interface CartItem {
  id: string;
  productName: string;
  product: {
    id: string;
    name: string;
    description: string;
    price: number;
    stockQuantity: number;
    unit: string;
    category: string;
    taxType: 'Standard' | 'Reduced' | 'Special';
  };
  quantity: number;
  unitPrice: number;
  taxRate: number;
  discountAmount: number;
  taxAmount: number;
  totalAmount: number;
  notes?: string;
  isModified: boolean;
}

interface Cart {
  cartId: string;
  tableNumber?: string;
  waiterName?: string;
  customer?: {
    id: string;
    name: string;
    email: string;
    phone: string;
    category: 'Regular' | 'VIP' | 'Wholesale' | 'Corporate';
  };
  subtotal: number;
  taxAmount: number;
  discountAmount: number;
  totalAmount: number;
  appliedCoupon?: {
    id: string;
    code: string;
    name: string;
    discountType: 'Percentage' | 'FixedAmount';
    discountValue: number;
  };
  notes?: string;
  status: 'Active' | 'Completed' | 'Cancelled' | 'Expired';
  expiresAt?: string;
  items: CartItem[];
}

interface CartBarProps {
  cart: Cart | null;
  loading: boolean;
  onRemove: (itemId: string) => void;
  onUpdateQty: (itemId: string, qty: number) => void;
  onClear: () => void;
}

// Backend format'ına uygun default cart
const defaultCart: Cart = { 
  cartId: '', 
  items: [], 
  subtotal: 0, 
  discountAmount: 0, 
  taxAmount: 0, 
  totalAmount: 0,
  status: 'Active'
};

// Ürün kutusu: memoize edilmiş kart
interface CartItemCardProps {
  item: CartItem;
  isSelected: boolean;
  onUpdateQty: (itemId: string, qty: number) => void;
  onRemove: (itemId: string) => void;
  setSelectedItemId: React.Dispatch<React.SetStateAction<string | null>>;
}

const getItemName = (item: any) => item.productName || (item.product && item.product.name) || item.name || '';
const getUnitPrice = (item: any) => item.unitPrice ?? item.price ?? 0;

const CartItemCard = React.memo(({ item, isSelected, onUpdateQty, onRemove, setSelectedItemId }: CartItemCardProps) => (
  <TouchableOpacity
    key={item.id}
    style={[styles.itemBox, isSelected && styles.itemBoxSelected]}
    onPress={() => setSelectedItemId(item.id)}
    activeOpacity={0.85}
  >
    <View style={styles.itemHeader}>
      <Text style={styles.itemName}>{getItemName(item)}</Text>
      {isSelected && <Ionicons name="checkmark-circle" size={18} color={COLORS.accent} style={{ marginLeft: 2 }} />}
    </View>
    <Text style={styles.itemPrice}>{Number(getUnitPrice(item)).toFixed(2)} €</Text>
    <View style={styles.qtyRow}>
      <TouchableOpacity
        style={[styles.qtyBtn, isSelected && styles.qtyBtnActive]}
        onPress={() => onUpdateQty(item.id, item.quantity - 1)}
        accessibilityLabel="Miktarı azalt"
      >
        <Ionicons name="remove-circle-outline" size={22} color={COLORS.danger} />
      </TouchableOpacity>
      <Text style={styles.qtyText}>{item.quantity}</Text>
      <TouchableOpacity
        style={[styles.qtyBtn, isSelected && styles.qtyBtnActive]}
        onPress={() => onUpdateQty(item.id, item.quantity + 1)}
        accessibilityLabel="Miktarı arttır"
      >
        <Ionicons name="add-circle-outline" size={22} color={COLORS.success} />
      </TouchableOpacity>
      <TouchableOpacity
        style={[styles.removeBtn, isSelected && styles.removeBtnActive]}
        onPress={() => onRemove(item.id)}
        accessibilityLabel="Ürünü sil"
      >
        <Ionicons name="trash-outline" size={20} color={COLORS.danger} />
      </TouchableOpacity>
    </View>
  </TouchableOpacity>
));

const CartBar: React.FC<CartBarProps> = ({ cart, loading, onRemove, onUpdateQty, onClear }) => {
  const { t } = useTranslation();
  const safeCart = useMemo(() => cart ?? defaultCart, [cart]);
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null);

  // DEBUG: Cart state değişimini izle
  useEffect(() => {
    console.log('CartBar - cart state:', cart);
  }, [cart]);

  // Sepet değiştiğinde ilk ürünü otomatik seçili yap
  useEffect(() => {
    if (safeCart && safeCart.items.length > 0) {
      setSelectedItemId(prev => prev && safeCart.items.some(i => i.id === prev) ? prev : safeCart.items[0].id);
    } else {
      setSelectedItemId(null);
    }
  }, [safeCart]);

  const memoOnUpdateQty = useCallback(onUpdateQty, [onUpdateQty]);
  const memoOnRemove = useCallback(onRemove, [onRemove]);

  // Sepet boş kontrolü güçlendirildi
  const hasItems = cart && Array.isArray(cart.items) && cart.items.length > 0;

  if (loading) {
    return null;
  }
  if (!hasItems) {
    return <Text style={styles.emptyText}>Sepet boş</Text>;
  }

  return (
    <View style={styles.container}>
      <ScrollView
        horizontal
        style={styles.cartList}
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={{ flexDirection: 'row', alignItems: 'center' }}
      >
        {safeCart.items.map(item => (
          <CartItemCard
            key={item.id}
            item={item}
            isSelected={selectedItemId === item.id}
            onUpdateQty={memoOnUpdateQty}
            onRemove={memoOnRemove}
            setSelectedItemId={setSelectedItemId}
          />
        ))}
      </ScrollView>
      <View style={styles.summaryRow}>
        <Text style={styles.totalText}>{t('cart.subtotal', 'Ara Toplam')}: {Number(safeCart.subtotal ?? 0).toFixed(2)} €</Text>
        <Text style={styles.totalText}>{t('cart.discount', 'İndirim')}: {Number(safeCart.discountAmount ?? 0).toFixed(2)} €</Text>
        <Text style={styles.totalText}>{t('cart.vat', 'KDV')}: {Number(safeCart.taxAmount ?? 0).toFixed(2)} €</Text>
        <Text style={styles.totalText}>{t('cart.grandTotal', 'Genel Toplam')}: {Number(safeCart.totalAmount ?? 0).toFixed(2)} €</Text>
        <TouchableOpacity style={styles.clearBtn} onPress={onClear} accessibilityLabel="Sepeti Temizle">
          <Ionicons name="trash" size={18} color="#fff" style={{ marginRight: 4 }} />
          <Text style={styles.clearBtnText}>{t('cart.clear', 'Sepeti Temizle')}</Text>
        </TouchableOpacity>
      </View>
    </View>
  );
};

const COLORS = {
  background: '#F7F8FA',
  card: '#FFFFFF',
  accent: '#1976D2',
  accentSoft: '#E3F2FD',
  danger: '#E53935',
  dangerSoft: '#FFEBEE',
  success: '#43A047',
  successSoft: '#E8F5E9',
  text: '#222',
  textSoft: '#666',
  border: '#E0E0E0',
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: COLORS.background,
    padding: 12,
    borderRadius: 16,
    marginVertical: 8,
    width: '100%',
    alignItems: 'flex-start',
  },
  cartList: {
    minHeight: 60,
    maxHeight: 120,
    width: '100%',
  },
  emptyText: {
    color: COLORS.textSoft,
    fontSize: 16,
    marginLeft: 8,
  },
  itemBox: {
    backgroundColor: COLORS.card,
    borderRadius: 14,
    padding: 10,
    marginRight: 12,
    minWidth: 120,
    alignItems: 'center',
    borderWidth: 2,
    borderColor: 'transparent',
    shadowColor: '#000',
    shadowOpacity: 0.04,
    shadowRadius: 8,
    shadowOffset: { width: 0, height: 2 },
    elevation: 1,
  },
  itemBoxSelected: {
    borderColor: COLORS.accent,
    backgroundColor: COLORS.accentSoft,
  },
  itemHeader: { flexDirection: 'row', alignItems: 'center', marginBottom: 2 },
  itemName: { fontWeight: 'bold', fontSize: 15, color: COLORS.text },
  itemPrice: { color: COLORS.accent, fontSize: 13, marginBottom: 2 },
  qtyRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: 4,
  },
  qtyBtn: {
    backgroundColor: COLORS.background,
    borderRadius: 8,
    paddingHorizontal: 8,
    paddingVertical: 2,
    marginHorizontal: 2,
  },
  qtyBtnActive: {
    backgroundColor: COLORS.accentSoft,
  },
  qtyText: {
    fontSize: 15,
    marginHorizontal: 4,
    color: COLORS.text,
    fontWeight: 'bold',
  },
  removeBtn: {
    backgroundColor: COLORS.dangerSoft,
    borderRadius: 8,
    paddingHorizontal: 8,
    paddingVertical: 2,
    marginLeft: 6,
    flexDirection: 'row',
    alignItems: 'center',
  },
  removeBtnActive: {
    // backgroundColor: COLORS.danger, // kaldırıldı, seçili üründe kırmızı kutu olmasın
  },
  removeBtnText: {
    color: '#fff',
    fontWeight: 'bold',
    fontSize: 14,
  },
  summaryRow: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    width: '100%',
    marginTop: 8,
    gap: 8,
  },
  totalText: {
    fontSize: 15,
    fontWeight: 'bold',
    color: COLORS.accent,
    marginRight: 8,
  },
  clearBtn: {
    backgroundColor: COLORS.danger,
    borderRadius: 8,
    paddingHorizontal: 12,
    paddingVertical: 6,
    marginLeft: 8,
    flexDirection: 'row',
    alignItems: 'center',
  },
  clearBtnText: {
    color: '#fff',
    fontWeight: 'bold',
    fontSize: 15,
  },
  selectedBox: {
    backgroundColor: COLORS.accentSoft,
    borderRadius: 16,
    padding: 12,
    marginBottom: 8,
    width: '100%',
    alignItems: 'flex-start',
    borderWidth: 2,
    borderColor: COLORS.accent,
  },
  selectedLabel: { fontSize: 12, color: COLORS.accent, fontWeight: 'bold', marginBottom: 2 },
  selectedName: { fontSize: 18, fontWeight: 'bold', color: COLORS.text, marginBottom: 2 },
  selectedDetailsRow: { flexDirection: 'row', flexWrap: 'wrap', marginBottom: 2 },
  selectedDetail: { fontSize: 13, color: COLORS.textSoft, marginRight: 12 },
  selectedBold: { fontWeight: 'bold', color: COLORS.text },
});

export default CartBar; 