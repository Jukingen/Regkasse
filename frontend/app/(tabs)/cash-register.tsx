import React, { useState, useEffect, useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  Alert,
  TextInput,
  Modal,
  FlatList,
  Platform,
  Animated,
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '../../contexts/AuthContext';
import { productService, Product } from '../../services/api/productService';
import { customerService, Customer } from '../../services/api/customerService';
import { Colors, Spacing, BorderRadius, Typography } from '../../constants/Colors';
import OrderManager from '../../components/OrderManager';

interface CartItem {
  product: Product;
  quantity: number;
  notes?: string;
}

interface Order {
  id: string;
  items: CartItem[];
  customer?: Customer;
  customerName?: string;
  tableNumber?: string;
  notes?: string;
  status: 'pending' | 'preparing' | 'ready' | 'served' | 'cancelled';
  createdAt: Date;
}

export default function CashRegisterScreen() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [products, setProducts] = useState<Product[]>([]);
  const [favoriteProducts, setFavoriteProducts] = useState<Product[]>([]);
  const [cart, setCart] = useState<CartItem[]>([]);
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [filteredProducts, setFilteredProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(false);
  const [showOrderManager, setShowOrderManager] = useState(false);
  const [orders, setOrders] = useState<Order[]>([]);
  const [paymentAmount, setPaymentAmount] = useState<string>('');
  const [selectedPaymentMethod, setSelectedPaymentMethod] = useState<'cash' | 'card' | 'voucher'>('cash');
  const [showChangeResult, setShowChangeResult] = useState(false);
  const [changeAmount, setChangeAmount] = useState(0);
  const [isProcessingPayment, setIsProcessingPayment] = useState(false);
  const [paymentSuccess, setPaymentSuccess] = useState(false);
  
  // Animasyonlar
  const changeAnimation = new Animated.Value(0);
  const paymentAnimation = new Animated.Value(0);
  const successAnimation = new Animated.Value(0);

  // Ürünleri yükle
  const loadProducts = useCallback(async () => {
    try {
      setLoading(true);
      console.log('Loading products...'); // Debug log
      
      const productsData = await productService.getAllProducts();
      console.log('Products loaded:', productsData?.length || 0); // Debug log
      
      if (productsData && productsData.length > 0) {
        setProducts(productsData);
        
        // Favori ürünleri belirle (stokta olan, sık kullanılan)
        const favorites = productsData
          .filter(product => product.stock > 0)
          .sort((a, b) => b.stock - a.stock)
          .slice(0, 5);
        setFavoriteProducts(favorites);
        
        console.log('Favorites set:', favorites.length); // Debug log
      } else {
        console.log('No products found, using demo products'); // Debug log
        
        // Demo ürünler oluştur
        const demoProducts: Product[] = [
          {
            id: '1',
            name: 'Espresso',
            price: 2.50,
            taxType: 'standard',
            description: 'Single shot espresso',
            category: 'drink',
            stockQuantity: 100,
            stock: 100,
            unit: 'piece',
            barcode: '1234567890'
          },
          {
            id: '2',
            name: 'Cappuccino',
            price: 3.50,
            taxType: 'standard',
            description: 'Espresso with steamed milk',
            category: 'drink',
            stockQuantity: 80,
            stock: 80,
            unit: 'piece',
            barcode: '1234567891'
          },
          {
            id: '3',
            name: 'Wiener Schnitzel',
            price: 18.90,
            taxType: 'standard',
            description: 'Traditional Viennese schnitzel',
            category: 'food',
            stockQuantity: 50,
            stock: 50,
            unit: 'piece',
            barcode: '1234567892'
          },
          {
            id: '4',
            name: 'Apfelstrudel',
            price: 6.50,
            taxType: 'reduced',
            description: 'Traditional apple strudel',
            category: 'dessert',
            stockQuantity: 30,
            stock: 30,
            unit: 'piece',
            barcode: '1234567893'
          },
          {
            id: '5',
            name: 'Mozartkugel',
            price: 1.50,
            taxType: 'reduced',
            description: 'Traditional chocolate',
            category: 'dessert',
            stockQuantity: 200,
            stock: 200,
            unit: 'piece',
            barcode: '1234567894'
          },
          {
            id: '6',
            name: 'Kaffee mit Milch',
            price: 4.20,
            taxType: 'special',
            description: 'Coffee with milk (special tax rate)',
            category: 'drink',
            stockQuantity: 75,
            stock: 75,
            unit: 'piece',
            barcode: '1234567895'
          }
        ];
        
        setProducts(demoProducts);
        setFavoriteProducts(demoProducts.slice(0, 3));
        
        Alert.alert(
          t('messages.demoMode'),
          t('messages.demoProductsMessage'),
          [{ text: t('common.ok') }]
        );
      }
    } catch (error) {
      console.error('Products load failed:', error);
      
      // Demo ürünler oluştur
      const demoProducts: Product[] = [
        {
          id: '1',
          name: 'Espresso',
          price: 2.50,
          taxType: 'standard',
          description: 'Single shot espresso',
          category: 'drink',
          stockQuantity: 100,
          stock: 100,
          unit: 'piece',
          barcode: '1234567890'
        },
        {
          id: '2',
          name: 'Cappuccino',
          price: 3.50,
          taxType: 'standard',
          description: 'Espresso with steamed milk',
          category: 'drink',
          stockQuantity: 80,
          stock: 80,
          unit: 'piece',
          barcode: '1234567891'
        },
        {
          id: '3',
          name: 'Wiener Schnitzel',
          price: 18.90,
          taxType: 'standard',
          description: 'Traditional Viennese schnitzel',
          category: 'food',
          stockQuantity: 50,
          stock: 50,
          unit: 'piece',
          barcode: '1234567892'
        },
        {
          id: '4',
          name: 'Apfelstrudel',
          price: 6.50,
          taxType: 'reduced',
          description: 'Traditional apple strudel',
          category: 'dessert',
          stockQuantity: 30,
          stock: 30,
          unit: 'piece',
          barcode: '1234567893'
        },
        {
          id: '5',
          name: 'Mozartkugel',
          price: 1.50,
          taxType: 'reduced',
          description: 'Traditional chocolate',
          category: 'dessert',
          stockQuantity: 200,
          stock: 200,
          unit: 'piece',
          barcode: '1234567894'
        },
        {
          id: '6',
          name: 'Kaffee mit Milch',
          price: 4.20,
          taxType: 'special',
          description: 'Coffee with milk (special tax rate)',
          category: 'drink',
          stockQuantity: 75,
          stock: 75,
          unit: 'piece',
          barcode: '1234567895'
        }
      ];
      
      setProducts(demoProducts);
      setFavoriteProducts(demoProducts.slice(0, 3));
      
              Alert.alert(
          t('errors.connectionError'),
          t('messages.demoProductsFallback'),
          [{ text: t('common.ok') }]
        );
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    loadProducts();
  }, [loadProducts]);

  // Arama fonksiyonu
  useEffect(() => {
    if (searchQuery.trim() === '') {
      setFilteredProducts([]);
    } else {
      const filtered = products.filter(product =>
        product.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
        product.barcode?.includes(searchQuery)
      );
      setFilteredProducts(filtered);
    }
  }, [searchQuery, products]);

  // Sepete ürün ekle
  const addToCart = (product: Product) => {
    console.log('Adding product to cart:', product.name, 'Stock:', product.stock); // Debug log
    
    const existingItem = cart.find(item => item.product.id === product.id);
    
    if (existingItem) {
      // Stok kontrolü - mevcut miktar + 1 stoktan fazla olmamalı
      if (existingItem.quantity + 1 > product.stock) {
        Alert.alert(t('errors.stockInsufficient'), t('errors.stockNotEnough', { available: product.stock }));
        return;
      }
      
      setCart(prev => prev.map(item =>
        item.product.id === product.id
          ? { ...item, quantity: item.quantity + 1 }
          : item
      ));
      console.log('Product quantity increased in cart'); // Debug log
    } else {
      // Yeni ürün ekleniyor - stok kontrolü
      if (product.stock <= 0) {
        Alert.alert(t('errors.stockInsufficient'), t('errors.stockNotEnough', { available: product.stock }));
        return;
      }
      
      setCart(prev => [...prev, { product, quantity: 1 }]);
      console.log('New product added to cart'); // Debug log
    }
  };

  // Sepetten ürün çıkar
  const removeFromCart = (productId: string) => {
    setCart(prev => prev.filter(item => item.product.id !== productId));
  };

  // Sepet miktarını güncelle
  const updateCartQuantity = (productId: string, quantity: number) => {
    console.log('Updating cart quantity for product:', productId, 'New quantity:', quantity); // Debug log
    
    if (quantity <= 0) {
      removeFromCart(productId);
      return;
    }
    
    const product = products.find(p => p.id === productId);
    if (product && quantity > product.stock) {
      Alert.alert(t('errors.stockInsufficient'), t('errors.stockNotEnough', { available: product.stock }));
      return;
    }
    
    setCart(prev => prev.map(item =>
      item.product.id === productId
        ? { ...item, quantity }
        : item
    ));
    console.log('Cart quantity updated successfully'); // Debug log
  };

  // Sepeti temizle
  const clearCart = () => {
    console.log('Clear cart button pressed, current cart length:', cart.length); // Debug log
    setCart([]);
    setPaymentAmount('');
    console.log('Cart cleared directly'); // Debug log
  };

  // Toplam hesapla
  const calculateTotal = () => {
    return cart.reduce((total, item) => {
      return total + (item.product.price * item.quantity);
    }, 0);
  };

  // Vergi hesapla - her vergi tipi için ayrı
  const calculateTax = () => {
    const taxRates: { [key: string]: number } = { 
      standard: 0.20, 
      reduced: 0.10, 
      special: 0.13 
    };
    
    const taxByType: { [key: string]: number } = {
      standard: 0,
      reduced: 0,
      special: 0
    };
    
    console.log('Calculating tax for cart items:', cart.length); // Debug log
    
    cart.forEach(item => {
      // taxType kontrolü ekle
      const taxType = item.product.taxType || 'standard';
      const rate = taxRates[taxType] || 0;
      const taxAmount = item.product.price * item.quantity * rate;
      
      console.log(`Item: ${item.product.name}, TaxType: ${taxType}, Rate: ${rate}, TaxAmount: ${taxAmount}`); // Debug log
      
      // taxByType'da bu taxType var mı kontrol et
      if (taxByType[taxType] !== undefined) {
        taxByType[taxType] += taxAmount;
      } else {
        // Eğer tanımlı değilse standard'a ekle
        taxByType.standard += taxAmount;
      }
    });
    
    const totalTax = Object.values(taxByType).reduce((total, tax) => total + tax, 0);
    console.log('Total tax calculated:', totalTax, 'Tax breakdown:', taxByType); // Debug log
    
    return totalTax;
  };

  // Vergi detaylarını getir
  const getTaxDetails = () => {
    const taxRates: { [key: string]: number } = { 
      standard: 0.20, 
      reduced: 0.10, 
      special: 0.13 
    };
    
    const taxByType: { [key: string]: { amount: number; rate: number } } = {
      standard: { amount: 0, rate: 0.20 },
      reduced: { amount: 0, rate: 0.10 },
      special: { amount: 0, rate: 0.13 }
    };
    
    console.log('Getting tax details for cart items:', cart.length); // Debug log
    
    cart.forEach(item => {
      // taxType kontrolü ekle
      const taxType = item.product.taxType || 'standard';
      const rate = taxRates[taxType] || 0;
      const taxAmount = item.product.price * item.quantity * rate;
      
      console.log(`Tax detail - Item: ${item.product.name}, TaxType: ${taxType}, Rate: ${rate}, TaxAmount: ${taxAmount}`); // Debug log
      
      // taxByType'da bu taxType var mı kontrol et
      if (taxByType[taxType]) {
        taxByType[taxType].amount += taxAmount;
      } else {
        // Eğer tanımlı değilse standard'a ekle
        taxByType.standard.amount += taxAmount;
      }
    });
    
    const result = Object.entries(taxByType)
      .filter(([_, data]) => data.amount > 0)
      .map(([type, data]) => ({
        type,
        rate: data.rate,
        amount: data.amount
      }));
    
    console.log('Tax details result:', result); // Debug log
    return result;
  };

  // Ödeme işlemi
  const handlePayment = () => {
    console.log('handlePayment called');
    console.log('Cart length:', cart.length);
    console.log('Payment amount:', paymentAmount);
    
    if (cart.length === 0) {
      console.log('Cart is empty, showing alert');
      Alert.alert(t('errors.emptyCart'), t('messages.emptyCart'));
      return;
    }

    const totalWithTax = calculateTotal() + calculateTax();
    console.log('Total with tax:', totalWithTax);
    
    if (!paymentAmount || parseFloat(paymentAmount) < totalWithTax) {
      console.log('Invalid amount, showing alert');
      Alert.alert(t('errors.invalidAmount'), t('validation.invalidAmount'));
      return;
    }

    console.log('Processing payment...');
    
    // Ödeme işlemi başlat
    setIsProcessingPayment(true);
    
    // Ödeme animasyonu başlat
    paymentAnimation.setValue(0);
    Animated.timing(paymentAnimation, {
      toValue: 1,
      duration: 2000,
      useNativeDriver: true,
    }).start();

    // Ödeme işlemi simülasyonu (2 saniye)
    setTimeout(() => {
      console.log('Payment completed, showing success');
      setIsProcessingPayment(false);
      setPaymentSuccess(true);
      
      // Başarı animasyonu başlat
      successAnimation.setValue(0);
      Animated.spring(successAnimation, {
        toValue: 1,
        useNativeDriver: true,
        tension: 100,
        friction: 8,
      }).start();
      
      // 3 saniye sonra temizle
      setTimeout(() => {
        console.log('Payment success confirmed, clearing cart');
        setPaymentSuccess(false);
        setCart([]);
        setPaymentAmount('');
        setSelectedCustomer(null);
      }, 3000);
    }, 2000);
  };



  // Sipariş oluştur
  const handleOrderComplete = (order: Order) => {
    setOrders(prev => [...prev, order]);
    setShowOrderManager(false);
    Alert.alert(t('order.completeTitle'), t('messages.orderCreated'));
  };

  // Sipariş iptal
  const handleOrderCancel = () => {
    setShowOrderManager(false);
  };

  return (
    <View style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <View style={styles.headerInfo}>
          <Text style={styles.headerTitle}>{t('cashRegister.title')}</Text>
          <Text style={styles.headerSubtitle}>
            {user?.firstName} {user?.lastName}
          </Text>
        </View>
        <View style={styles.headerActions}>
          <TouchableOpacity
            style={styles.orderButton}
            onPress={() => setShowOrderManager(true)}
          >
            <Ionicons name="list-outline" size={20} color="white" />
            <Text style={styles.orderButtonText}>{t('cashRegister.orders')}</Text>
          </TouchableOpacity>
        </View>
      </View>

      <View style={styles.content}>
        {/* Sol Panel - Ürünler */}
        <View style={styles.leftPanel}>
          {/* Arama */}
          <View style={styles.searchContainer}>
            <Ionicons name="search" size={20} color={Colors.light.textSecondary} />
            <TextInput
              style={styles.searchInput}
              placeholder={t('common.search')}
              value={searchQuery}
              onChangeText={setSearchQuery}
            />
            {searchQuery.length > 0 && (
              <TouchableOpacity onPress={() => setSearchQuery('')}>
                <Ionicons name="close-circle" size={20} color={Colors.light.textSecondary} />
              </TouchableOpacity>
            )}
          </View>

          {/* Favori Ürünler */}
          {!searchQuery && (
            <View style={styles.favoritesSection}>
              <Text style={styles.sectionTitle}>{t('cashRegister.quickAdd')}</Text>
              <ScrollView horizontal showsHorizontalScrollIndicator={false}>
                {favoriteProducts.map(product => (
                  <TouchableOpacity
                    key={product.id}
                    style={styles.favoriteProduct}
                    onPress={() => addToCart(product)}
                  >
                    <Text style={styles.favoriteProductName}>{product.name}</Text>
                    <Text style={styles.favoriteProductPrice}>€{product.price.toFixed(2)}</Text>
                  </TouchableOpacity>
                ))}
              </ScrollView>
            </View>
          )}

          {/* Ürün Listesi */}
          <View style={styles.productsSection}>
            <Text style={styles.sectionTitle}>
              {searchQuery ? t('cashRegister.searchResults') : t('cashRegister.allProducts')}
            </Text>
            <FlatList
              data={searchQuery ? filteredProducts : products}
              keyExtractor={(item) => item.id}
              renderItem={({ item }) => (
                <TouchableOpacity
                  style={styles.productItem}
                  onPress={() => addToCart(item)}
                >
                  <View style={styles.productInfo}>
                    <Text style={styles.productName}>{item.name}</Text>
                  </View>
                  <View style={styles.productPrice}>
                    <Text style={styles.priceText}>€{item.price.toFixed(2)}</Text>
                  </View>
                </TouchableOpacity>
              )}
              showsVerticalScrollIndicator={false}
            />
          </View>
        </View>

        {/* Sağ Panel - Sepet */}
        <View style={styles.rightPanel}>
          <View style={styles.cartHeader}>
            <Text style={styles.cartTitle}>{t('cashRegister.cart')}</Text>
            {cart.length > 0 && (
              <TouchableOpacity onPress={clearCart}>
                <Ionicons name="trash-outline" size={20} color={Colors.light.error} />
              </TouchableOpacity>
            )}
          </View>

          {cart.length === 0 ? (
            <View style={styles.emptyCart}>
              <Ionicons name="cart-outline" size={48} color={Colors.light.textSecondary} />
              <Text style={styles.emptyCartText}>{t('cashRegister.cartEmpty')}</Text>
            </View>
          ) : (
            <>
              <ScrollView style={styles.cartItems} showsVerticalScrollIndicator={false}>
                {cart.map(item => (
                  <View key={item.product.id} style={styles.cartItem}>
                    <View style={styles.cartItemInfo}>
                      <Text style={styles.cartItemName}>{item.product.name}</Text>
                      <Text style={styles.cartItemPrice}>
                        €{(item.product.price * item.quantity).toFixed(2)}
                      </Text>
                    </View>
                    <View style={styles.cartItemActions}>
                      <TouchableOpacity
                        style={styles.quantityButton}
                        onPress={() => updateCartQuantity(item.product.id, item.quantity - 1)}
                      >
                        <Ionicons name="remove" size={16} color={Colors.light.text} />
                      </TouchableOpacity>
                      <Text style={styles.quantityText}>{item.quantity}</Text>
                      <TouchableOpacity
                        style={styles.quantityButton}
                        onPress={() => updateCartQuantity(item.product.id, item.quantity + 1)}
                      >
                        <Ionicons name="add" size={16} color={Colors.light.text} />
                      </TouchableOpacity>
                    </View>
                  </View>
                ))}
              </ScrollView>

              {/* Sepet Özeti */}
              <View style={styles.cartSummary}>
                {(() => {
                  const subtotal = calculateTotal();
                  const tax = calculateTax();
                  const total = subtotal + tax;
                  
                  console.log('Cart summary - Subtotal:', subtotal, 'Tax:', tax, 'Total:', total); // Debug log
                  
                  return (
                    <>
                      <View style={styles.summaryRow}>
                        <Text style={styles.summaryLabel}>{t('cashRegister.subtotal')} (ohne MwSt.):</Text>
                        <Text style={styles.summaryValue}>€{subtotal.toFixed(2)}</Text>
                      </View>
                      
                      {/* Vergi Detayları */}
                      {getTaxDetails().map((taxDetail, index) => (
                        <View key={index} style={styles.summaryRow}>
                          <Text style={styles.summaryLabel}>
                            {t('cashRegister.tax')} ({Math.round(taxDetail.rate * 100)}%):
                          </Text>
                          <Text style={styles.summaryValue}>€{taxDetail.amount.toFixed(2)}</Text>
                        </View>
                      ))}
                      
                      <View style={styles.summaryRow}>
                        <Text style={styles.summaryLabel}>{t('cashRegister.total')} (inkl. MwSt.):</Text>
                        <Text style={styles.summaryTotal}>€{total.toFixed(2)}</Text>
                      </View>
                    </>
                  );
                })()}
              </View>

              {/* Ödeme Bölümü */}
              <View style={styles.paymentSection}>
                <Text style={styles.paymentTitle}>{t('cashRegister.payment')}</Text>
                
                {/* Ödeme Yöntemi */}
                <View style={styles.paymentMethodContainer}>
                  <Text style={styles.paymentLabel}>{t('cashRegister.paymentMethod')}:</Text>
                  <View style={styles.paymentMethodButtons}>
                    {(['cash', 'card', 'voucher'] as const).map(method => (
                      <TouchableOpacity
                        key={method}
                        style={[
                          styles.paymentMethodButton,
                          selectedPaymentMethod === method && styles.paymentMethodButtonActive
                        ]}
                        onPress={() => setSelectedPaymentMethod(method)}
                      >
                        <Text style={[
                          styles.paymentMethodText,
                          selectedPaymentMethod === method && styles.paymentMethodTextActive
                        ]}>
                          {t(`cashRegister.${method}`)}
                        </Text>
                      </TouchableOpacity>
                    ))}
                  </View>
                </View>

                {/* Ödeme Tutarı */}
                <View style={styles.paymentAmountContainer}>
                  <Text style={styles.paymentLabel}>{t('cashRegister.amountReceived')}:</Text>
                  
                  {/* Basit ve etkili sayı girişi */}
                  <TextInput
                    style={styles.paymentAmountInput}
                    placeholder="0.00"
                    value={paymentAmount}
                    onChangeText={(text) => {
                      console.log('Input changed to:', text);
                      // Sadece sayı ve tek nokta karakterine izin ver
                      const cleanedText = text.replace(/[^0-9.]/g, '');
                      const parts = cleanedText.split('.');
                      if (parts.length <= 2) {
                        setPaymentAmount(cleanedText);
                      }
                    }}
                    keyboardType="decimal-pad"
                  />
                  
                  {/* Basit test butonları */}
                  <View style={styles.quickAmountButtons}>
                    <TouchableOpacity
                      style={styles.quickAmountButton}
                      onPress={() => {
                        console.log('Setting amount to 10');
                        setPaymentAmount('10');
                      }}
                    >
                      <Text style={styles.quickAmountButtonText}>10€</Text>
                    </TouchableOpacity>
                    
                    <TouchableOpacity
                      style={styles.quickAmountButton}
                      onPress={() => {
                        console.log('Setting amount to 20');
                        setPaymentAmount('20');
                      }}
                    >
                      <Text style={styles.quickAmountButtonText}>20€</Text>
                    </TouchableOpacity>
                    
                    <TouchableOpacity
                      style={styles.quickAmountButton}
                      onPress={() => {
                        console.log('Clearing amount');
                        setPaymentAmount('');
                      }}
                    >
                      <Text style={styles.quickAmountButtonText}>Temizle</Text>
                    </TouchableOpacity>
                  </View>
                  
                  {/* Debug bilgisi */}
                  <Text style={styles.debugText}>
                    Değer: "{paymentAmount}" (Tip: {typeof paymentAmount})
                  </Text>
                </View>

                {/* Para Üstü Hesaplama */}
                {paymentAmount && parseFloat(paymentAmount) > 0 && (
                  <TouchableOpacity
                    style={styles.changeButton}
                    onPress={() => {
                      console.log('Calculate change button pressed');
                      const totalWithTax = calculateTotal() + calculateTax();
                      const change = parseFloat(paymentAmount) - totalWithTax;
                      console.log('Total with tax:', totalWithTax, 'Payment amount:', paymentAmount, 'Change:', change);
                      
                      if (change >= 0) {
                        setChangeAmount(change);
                        setShowChangeResult(true);
                        
                        // Animasyon başlat
                        changeAnimation.setValue(0);
                        Animated.spring(changeAnimation, {
                          toValue: 1,
                          useNativeDriver: true,
                          tension: 100,
                          friction: 8,
                        }).start();
                        
                        // 3 saniye sonra gizle
                        setTimeout(() => {
                          setShowChangeResult(false);
                        }, 3000);
                      } else {
                        Alert.alert(
                          'Ungültiger Betrag',
                          'Der erhaltene Betrag ist zu niedrig.',
                          [{ text: t('common.ok') }]
                        );
                      }
                    }}
                  >
                    <Ionicons name="calculator-outline" size={20} color="white" />
                    <Text style={styles.changeButtonText}>{t('cashRegister.calculateChange')}</Text>
                  </TouchableOpacity>
                )}

                {/* Para Üstü Sonucu */}
                {showChangeResult && (
                  <Animated.View 
                    style={[
                      styles.changeResultContainer,
                      {
                        transform: [{
                          scale: changeAnimation.interpolate({
                            inputRange: [0, 1],
                            outputRange: [0.5, 1],
                          })
                        }],
                        opacity: changeAnimation,
                      }
                    ]}
                  >
                    <Ionicons name="checkmark-circle" size={32} color={Colors.light.success} />
                    <Text style={styles.changeResultTitle}>{t('cashRegister.change')}</Text>
                    <Text style={styles.changeResultAmount}>€{changeAmount.toFixed(2)}</Text>
                  </Animated.View>
                )}

                {/* Ödeme Butonu */}
                <TouchableOpacity
                  style={[
                    styles.payButton,
                    isProcessingPayment && styles.payButtonProcessing
                  ]}
                  onPress={() => {
                    console.log('Process payment button pressed');
                    handlePayment();
                  }}
                  disabled={isProcessingPayment}
                >
                  {isProcessingPayment ? (
                    <>
                      <Ionicons name="hourglass-outline" size={24} color="white" />
                      <Text style={styles.payButtonText}>Verarbeitung...</Text>
                    </>
                  ) : (
                    <>
                      <Ionicons name="card-outline" size={24} color="white" />
                      <Text style={styles.payButtonText}>{t('cashRegister.processPayment')}</Text>
                    </>
                  )}
                </TouchableOpacity>

                {/* Ödeme Başarılı */}
                {paymentSuccess && (
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
            </>
          )}
        </View>
      </View>

      {/* Modals */}
      <OrderManager
        visible={showOrderManager}
        onClose={() => setShowOrderManager(false)}
        onOrderComplete={handleOrderComplete}
        onOrderCancel={handleOrderCancel}
        products={products}
        currentUserId={user?.id}
        currentUserRole={user?.role}
      />
    </View>
  );
}

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
    backgroundColor: Colors.light.primary,
  },
  headerInfo: {
    flex: 1,
  },
  headerTitle: {
    ...Typography.h2,
    color: 'white',
  },
  headerSubtitle: {
    ...Typography.bodySmall,
    color: 'white',
    opacity: 0.8,
  },
  headerActions: {
    flexDirection: 'row',
    gap: Spacing.sm,
  },
  orderButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.2)',
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    borderRadius: BorderRadius.sm,
    gap: Spacing.xs,
  },
  orderButtonText: {
    ...Typography.bodySmall,
    color: 'white',
    fontWeight: '600',
  },
  content: {
    flex: 1,
    flexDirection: 'row',
  },
  leftPanel: {
    flex: 1,
    borderRightWidth: 1,
    borderRightColor: Colors.light.border,
  },
  rightPanel: {
    width: 350,
    backgroundColor: Colors.light.surface,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
    gap: Spacing.sm,
  },
  searchInput: {
    flex: 1,
    ...Typography.body,
    color: Colors.light.text,
  },
  favoritesSection: {
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  sectionTitle: {
    ...Typography.h3,
    color: Colors.light.text,
    marginBottom: Spacing.sm,
  },
  favoriteProduct: {
    backgroundColor: Colors.light.primary + '20',
    padding: Spacing.sm,
    borderRadius: BorderRadius.sm,
    marginRight: Spacing.sm,
    minWidth: 100,
    alignItems: 'center',
  },
  favoriteProductName: {
    ...Typography.bodySmall,
    color: Colors.light.primary,
    fontWeight: '600',
    textAlign: 'center',
  },
  favoriteProductPrice: {
    ...Typography.bodySmall,
    color: Colors.light.textSecondary,
    marginTop: Spacing.xs,
  },
  productsSection: {
    flex: 1,
    padding: Spacing.md,
  },
  productItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.sm,
    marginBottom: Spacing.sm,
  },
  productInfo: {
    flex: 1,
  },
  productName: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '600',
  },

  productPrice: {
    alignItems: 'flex-end',
  },
  priceText: {
    ...Typography.body,
    color: Colors.light.primary,
    fontWeight: 'bold',
  },

  cartHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  cartTitle: {
    ...Typography.h3,
    color: Colors.light.text,
  },
  emptyCart: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  emptyCartText: {
    ...Typography.body,
    color: Colors.light.textSecondary,
    marginTop: Spacing.md,
  },
  cartItems: {
    flex: 1,
    padding: Spacing.md,
  },
  cartItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.sm,
    borderBottomWidth: 1,
    borderBottomColor: Colors.light.border,
  },
  cartItemInfo: {
    flex: 1,
  },
  cartItemName: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '500',
  },
  cartItemPrice: {
    ...Typography.bodySmall,
    color: Colors.light.primary,
    fontWeight: '600',
  },
  cartItemActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
  },
  quantityButton: {
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: Colors.light.primary + '20',
    justifyContent: 'center',
    alignItems: 'center',
  },
  quantityText: {
    ...Typography.body,
    color: Colors.light.text,
    fontWeight: '600',
    minWidth: 30,
    textAlign: 'center',
  },
  cartSummary: {
    padding: Spacing.md,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  summaryRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: Spacing.xs,
  },
  summaryLabel: {
    ...Typography.body,
    color: Colors.light.text,
  },
  summaryValue: {
    ...Typography.body,
    color: Colors.light.text,
  },
  summaryTotal: {
    ...Typography.h3,
    color: Colors.light.primary,
    fontWeight: 'bold',
  },
  paymentSection: {
    padding: Spacing.md,
    borderTopWidth: 1,
    borderTopColor: Colors.light.border,
  },
  paymentTitle: {
    ...Typography.h3,
    color: Colors.light.text,
    marginBottom: Spacing.md,
  },
  paymentMethodContainer: {
    marginBottom: Spacing.md,
  },
  paymentLabel: {
    ...Typography.body,
    color: Colors.light.text,
    marginBottom: Spacing.sm,
  },
  paymentMethodButtons: {
    flexDirection: 'row',
    gap: Spacing.sm,
  },
  paymentMethodButton: {
    flex: 1,
    padding: Spacing.sm,
    borderRadius: BorderRadius.sm,
    backgroundColor: Colors.light.background,
    alignItems: 'center',
  },
  paymentMethodButtonActive: {
    backgroundColor: Colors.light.primary,
  },
  paymentMethodText: {
    ...Typography.bodySmall,
    color: Colors.light.text,
    fontWeight: '500',
  },
  paymentMethodTextActive: {
    color: 'white',
  },
  paymentAmountContainer: {
    marginBottom: Spacing.md,
  },
  paymentAmountInput: {
    borderWidth: 2,
    borderColor: Colors.light.border,
    borderRadius: BorderRadius.sm,
    padding: Spacing.md,
    ...Typography.body,
    color: Colors.light.text,
    backgroundColor: Colors.light.background,
    fontSize: 18,
    fontWeight: '600',
    textAlign: 'center',
    minHeight: 50,
    // Web platformu için ek stiller
    outline: 'none' as any,
    cursor: 'text' as any,
    userSelect: 'text' as any,
  },
  changeButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.info,
    padding: Spacing.sm,
    borderRadius: BorderRadius.sm,
    marginBottom: Spacing.md,
    gap: Spacing.sm,
  },
  changeButtonText: {
    ...Typography.bodySmall,
    color: 'white',
    fontWeight: '600',
  },
  payButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: Colors.light.success,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    gap: Spacing.sm,
  },
  payButtonText: {
    ...Typography.button,
    color: 'white',
    fontWeight: '600',
  },
  testButtonsContainer: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    marginTop: Spacing.sm,
  },
  testButton: {
    padding: Spacing.sm,
    backgroundColor: Colors.light.info,
    borderRadius: BorderRadius.sm,
    alignItems: 'center',
  },
  testButtonText: {
    ...Typography.bodySmall,
    color: 'white',
    fontWeight: '600',
  },
  quickAmountButtons: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    marginTop: Spacing.sm,
    gap: Spacing.sm,
  },
  quickAmountButton: {
    flex: 1,
    padding: Spacing.sm,
    backgroundColor: Colors.light.info,
    borderRadius: BorderRadius.sm,
    alignItems: 'center',
  },
  quickAmountButtonText: {
    ...Typography.bodySmall,
    color: 'white',
    fontWeight: '600',
  },
  debugText: {
    marginTop: Spacing.sm,
    color: Colors.light.textSecondary,
    fontSize: 12,
    textAlign: 'center',
  },
  changeResultContainer: {
    backgroundColor: Colors.light.success + '20',
    borderWidth: 2,
    borderColor: Colors.light.success,
    borderRadius: BorderRadius.md,
    padding: Spacing.lg,
    marginTop: Spacing.md,
    alignItems: 'center',
    justifyContent: 'center',
  },
  changeResultTitle: {
    ...Typography.h3,
    color: Colors.light.success,
    fontWeight: '600',
    marginTop: Spacing.sm,
  },
  changeResultAmount: {
    ...Typography.h2,
    color: Colors.light.success,
    fontWeight: 'bold',
    marginTop: Spacing.xs,
  },
  payButtonProcessing: {
    backgroundColor: Colors.light.warning,
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
    ...Typography.h2,
    color: Colors.light.success,
    fontWeight: 'bold',
    marginTop: Spacing.lg,
    textAlign: 'center',
  },
  paymentSuccessSubtitle: {
    ...Typography.body,
    color: 'white',
    marginTop: Spacing.sm,
    textAlign: 'center',
  },
}); 