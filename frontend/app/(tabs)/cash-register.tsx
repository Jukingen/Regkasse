import React, { useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  Modal,
  ScrollView,
  Animated,
  Vibration,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';
import { Colors, Spacing, BorderRadius } from '../../constants/Colors';
import OrderManager from '../../components/OrderManager';
import QuickAccessPanel from '../../components/QuickAccessPanel';
import EnhancedCart from '../../components/EnhancedCart';
import TableManager from '../../components/TableManager';
import PaymentSection from '../../components/PaymentSection';
import ProductList from '../../components/ProductList';
import FavoritesSection from '../../components/FavoritesSection';
import HeaderSection from '../../components/HeaderSection';
import { useCashRegister } from '../../hooks/useCashRegister';

export default function CashRegisterScreen() {
  const { t } = useTranslation();
  const { user } = useAuth();
  
  // Custom hook kullan
  const cashRegister = useCashRegister(user);
  
  // Animasyonlar
  const changeAnimation = useRef(new Animated.Value(0)).current;
  const paymentAnimation = useRef(new Animated.Value(0)).current;
  const successAnimation = useRef(new Animated.Value(0)).current;

  return (
    <View style={styles.container}>
      {/* Header */}
      <HeaderSection
        user={user}
        selectedTable={cashRegister.selectedTable}
        pendingOrdersCount={cashRegister.pendingOrdersCount}
        onShowOrderManager={() => cashRegister.setShowOrderManager(true)}
        onShowFavoritesManager={cashRegister.handleFavoritesAction}
        onShowTableManager={() => cashRegister.setShowTableManager(true)}
        onQuickAction={cashRegister.handleQuickAction}
      />

      <View style={styles.content}>
        {/* Hızlı Erişim Paneli - Açılır/Kapanır */}
        {!cashRegister.isQuickAccessCollapsed && (
          <QuickAccessPanel
            onQuickAction={cashRegister.handleQuickAction}
            pendingOrders={cashRegister.pendingOrdersCount}
            lowStockItems={cashRegister.lowStockCount}
            dailySales={cashRegister.dailySales}
            isCollapsed={false}
            onToggleCollapse={() => cashRegister.setIsQuickAccessCollapsed(true)}
            favoriteProducts={cashRegister.userFavorites}
            onFavoriteProductPress={cashRegister.handleFavoriteProductPress}
          />
        )}
        
        {/* Hızlı Erişim Açma Butonu */}
        {cashRegister.isQuickAccessCollapsed && (
          <TouchableOpacity
            style={styles.quickAccessToggleButton}
            onPress={() => cashRegister.setIsQuickAccessCollapsed(false)}
          >
            <Ionicons name="chevron-down" size={24} color={Colors.light.primary} />
          </TouchableOpacity>
        )}

        {/* Sol Panel - Ürünler */}
        <View style={styles.leftPanel}>
          {/* Favori Ürünler - Hızlı Erişim */}
          <FavoritesSection
            favoriteProducts={cashRegister.favoriteProducts}
            onAddToCart={cashRegister.addToCart}
          />

          {/* Ürün Listesi */}
          <ProductList
            products={cashRegister.products}
            userFavorites={cashRegister.userFavorites}
            onAddToCart={cashRegister.addToCart}
            onToggleFavorite={cashRegister.toggleFavorite}
          />
        </View>

        {/* Sağ Panel - Gelişmiş Sepet */}
        <View style={styles.rightPanel}>
          {/* Sepet Bölümü */}
          <View style={styles.cartSection}>
            <EnhancedCart
              items={cashRegister.cart}
              onUpdateQuantity={cashRegister.handleUpdateCartQuantity}
              onRemoveItem={cashRegister.handleRemoveFromCart}
              onUpdateNotes={cashRegister.handleUpdateCartNotes}
              onApplyDiscount={cashRegister.handleApplyDiscount}
              onClearCart={cashRegister.clearCart}
              onSaveCart={cashRegister.handleSaveCart}
              onLoadCart={cashRegister.handleLoadCart}
              onBulkAction={cashRegister.handleBulkAction}
              key={`cart-${cashRegister.selectedTable}`} // Masa değiştiğinde yeniden render
            />
          </View>

          {/* Ödeme Bölümü */}
          <PaymentSection
            cart={cashRegister.cart}
            paymentAmount={cashRegister.paymentAmount}
            setPaymentAmount={cashRegister.setPaymentAmount}
            selectedPaymentMethod={cashRegister.selectedPaymentMethod}
            setSelectedPaymentMethod={cashRegister.setSelectedPaymentMethod}
            calculateTotal={cashRegister.calculateTotal}
            calculateTax={cashRegister.calculateTax}
            onPayment={cashRegister.handlePayment}
            isProcessingPayment={cashRegister.isProcessingPayment}
            changeAmount={cashRegister.changeAmount}
            changeAnimation={changeAnimation}
            showChangeResult={cashRegister.showChangeResult}
            setShowChangeResult={cashRegister.setShowChangeResult}
            setChangeAmount={cashRegister.setChangeAmount}
          />

          {/* Ödeme Başarılı */}
          {cashRegister.paymentSuccess && (
            <Animated.View 
              style={[
                styles.paymentSuccessContainer,
                {
                  transform: [{
                    scale: successAnimation.interpolate({
                      inputRange: [0, 1],
                      outputRange: [0.5, 1],
                    })
                  }],
                  opacity: successAnimation,
                }
              ]}
            >
              <Ionicons name="checkmark-circle" size={48} color={Colors.light.success} />
              <Text style={styles.paymentSuccessTitle}>Zahlung erfolgreich!</Text>
              <Text style={styles.paymentSuccessSubtitle}>Vielen Dank für Ihren Einkauf</Text>
            </Animated.View>
          )}
        </View>
      </View>

      {/* Modals */}
      <OrderManager
        visible={cashRegister.showOrderManager}
        onClose={() => cashRegister.setShowOrderManager(false)}
        onOrderComplete={cashRegister.handleOrderComplete}
        onOrderCancel={cashRegister.handleOrderCancel}
        products={cashRegister.products}
        currentUserId={user?.id}
        currentUserRole={user?.role}
      />

      {/* Masa Yönetimi Modal */}
      <TableManager
        visible={cashRegister.showTableManager}
        onClose={() => cashRegister.setShowTableManager(false)}
        selectedTable={cashRegister.selectedTable}
        tableOrders={cashRegister.tableOrders}
        onTableSelect={(tableNumber, tableOrder) => {
          console.log('Masa seçildi:', tableNumber, 'Sipariş:', tableOrder);
          cashRegister.setSelectedTable(tableNumber);
          cashRegister.setShowTableManager(false);
          
          if (tableOrder) {
            const cartItems = cashRegister.convertTableOrderToCartItems(tableOrder);
            console.log('Dönüştürülen siparişler:', cartItems);
            cashRegister.setCart(cartItems);
          } else {
            const savedOrder = cashRegister.tableOrders[tableNumber] || [];
            console.log('Kaydedilmiş siparişler:', savedOrder);
            cashRegister.setCart(savedOrder);
          }
          
          Vibration.vibrate(25);
        }}
        onOrderComplete={(orderId) => {
          console.log('Sipariş tamamlandı:', orderId);
        }}
      />

      {/* Favori Ürünler Yöneticisi Modal */}
      <Modal
        visible={cashRegister.showFavoritesManager}
        transparent
        animationType="slide"
      >
        <View style={styles.modalOverlay}>
          <View style={styles.favoritesManagerContainer}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>
                <Ionicons name="heart" size={24} color={Colors.light.primary} />
                {' '}Favori Ürünler
              </Text>
              <TouchableOpacity
                style={styles.closeButton}
                onPress={() => cashRegister.setShowFavoritesManager(false)}
              >
                <Ionicons name="close" size={24} color={Colors.light.textSecondary} />
              </TouchableOpacity>
            </View>

            <ScrollView style={styles.favoritesManagerContent}>
              {cashRegister.userFavorites.length === 0 ? (
                <View style={styles.emptyFavorites}>
                  <Ionicons name="heart-outline" size={64} color={Colors.light.textSecondary} />
                  <Text style={styles.emptyFavoritesText}>Henüz favori ürününüz yok</Text>
                  <Text style={styles.emptyFavoritesSubtext}>
                    Ürünlerin yanındaki kalp ikonuna tıklayarak favorilere ekleyebilirsiniz
                  </Text>
                </View>
              ) : (
                <View style={styles.favoritesList}>
                  {cashRegister.userFavorites.map(product => (
                    <View key={product.id} style={styles.favoriteItem}>
                      <View style={styles.favoriteItemInfo}>
                        <Text style={styles.favoriteItemName}>{product.name}</Text>
                        <Text style={styles.favoriteItemCategory}>{product.category}</Text>
                      </View>
                      <View style={styles.favoriteItemActions}>
                        <TouchableOpacity
                          style={styles.addToCartButton}
                          onPress={() => {
                            cashRegister.addToCart(product);
                            cashRegister.setShowFavoritesManager(false);
                          }}
                        >
                          <Ionicons name="add" size={20} color="white" />
                        </TouchableOpacity>
                        <TouchableOpacity
                          style={styles.removeFavoriteButton}
                          onPress={() => cashRegister.toggleFavorite(product)}
                        >
                          <Ionicons name="trash-outline" size={20} color="white" />
                        </TouchableOpacity>
                      </View>
                    </View>
                  ))}
                </View>
              )}
            </ScrollView>
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  content: {
    flex: 1,
    flexDirection: 'row',
  },
  leftPanel: {
    flex: 1,
    backgroundColor: Colors.light.surface,
  },
  rightPanel: {
    flex: 1,
    backgroundColor: Colors.light.background,
  },
  quickAccessToggleButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.surface,
    padding: Spacing.md,
    marginBottom: Spacing.md,
    borderRadius: BorderRadius.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
    gap: Spacing.sm,
  },
  paymentSuccessContainer: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: 'rgba(0, 0, 0, 0.8)',
    alignItems: 'center',
    justifyContent: 'center',
    zIndex: 1000,
  },
  paymentSuccessTitle: {
    fontSize: 24,
    color: Colors.light.success,
    fontWeight: 'bold',
    marginTop: Spacing.lg,
    textAlign: 'center',
  },
  paymentSuccessSubtitle: {
    fontSize: 16,
    color: 'white',
    marginTop: Spacing.sm,
    textAlign: 'center',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  favoritesManagerContainer: {
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
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.lg,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  modalTitle: {
    fontSize: 20,
    color: Colors.light.text,
    flexDirection: 'row',
    alignItems: 'center',
  },
  closeButton: {
    padding: Spacing.xs,
  },
  favoritesManagerContent: {
    flex: 1,
    padding: Spacing.lg,
  },
  emptyFavorites: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: Spacing.xl,
  },
  emptyFavoritesText: {
    fontSize: 20,
    color: Colors.light.text,
    marginTop: Spacing.md,
    textAlign: 'center',
  },
  emptyFavoritesSubtext: {
    fontSize: 16,
    color: Colors.light.textSecondary,
    marginTop: Spacing.sm,
    textAlign: 'center',
  },
  favoritesList: {
    gap: Spacing.md,
  },
  favoriteItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    borderWidth: 1,
    borderColor: Colors.light.border,
  },
  favoriteItemInfo: {
    flex: 1,
  },
  favoriteItemName: {
    fontSize: 16,
    color: Colors.light.text,
    fontWeight: '600',
  },
  favoriteItemCategory: {
    fontSize: 14,
    color: Colors.light.textSecondary,
    marginTop: Spacing.xs,
  },
  favoriteItemActions: {
    flexDirection: 'row',
    gap: Spacing.sm,
  },
  addToCartButton: {
    padding: Spacing.sm,
    backgroundColor: Colors.light.primary,
    borderRadius: BorderRadius.sm,
  },
  removeFavoriteButton: {
    padding: Spacing.sm,
    backgroundColor: Colors.light.error,
    borderRadius: BorderRadius.sm,
  },
  cartSection: {
    flex: 1,
    backgroundColor: Colors.light.surface,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.sm,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 1,
    },
    shadowOpacity: 0.05,
    shadowRadius: 2,
    elevation: 2,
  },
}); 