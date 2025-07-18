import AsyncStorage from '@react-native-async-storage/async-storage';
import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Vibration } from 'react-native';

import printerService from '../services/PrinterService';
import { CartService, Cart, CartItem as BackendCartItem, CreateCartRequest, AddCartItemRequest } from '../services/api/cartService';
import { Customer } from '../services/api/customerService';
import { getProducts, Product } from '../services/api/productService';
import { CartItem, Order } from '../types/cart';

// Türkçe Açıklama: Bu hook, kasa işlemlerini yönetir. Sepet (cart) ile ilgili tüm hesaplamalar backend'den alınır. CartItem ve Product tipleri backend ile uyumlu.
export const useCashRegister = (user: any) => {
  const { t } = useTranslation();
  const cartService = CartService.getInstance();
  const [products, setProducts] = useState<Product[]>([]);
  const [favoriteProducts, setFavoriteProducts] = useState<Product[]>([]);
  // cart state'i Cart tipinde olacak ve backend'den gelen değerler kullanılacak
  const [cart, setCart] = useState<Cart | null>(null);
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [loading, setLoading] = useState(false);
  const [showOrderManager, setShowOrderManager] = useState(false);
  const [orders, setOrders] = useState<Order[]>([]);
  const [paymentAmount, setPaymentAmount] = useState<string>('');
  const [selectedPaymentMethod, setSelectedPaymentMethod] = useState<'cash' | 'card' | 'voucher'>('cash');
  const [showChangeResult, setShowChangeResult] = useState(false);
  const [changeAmount, setChangeAmount] = useState(0);
  const [isProcessingPayment, setIsProcessingPayment] = useState(false);
  const [paymentSuccess, setPaymentSuccess] = useState(false);
  const [showQuickAccess, setShowQuickAccess] = useState(true);
  const [isQuickAccessCollapsed, setIsQuickAccessCollapsed] = useState(false);
  const [pendingOrdersCount, setPendingOrdersCount] = useState(0);
  const [lowStockCount, setLowStockCount] = useState(0);
  const [dailySales, setDailySales] = useState(0);
  const [userFavorites, setUserFavorites] = useState<Product[]>([]);
  const [showFavoritesManager, setShowFavoritesManager] = useState(false);
  const [showTableManager, setShowTableManager] = useState(false);
  const [selectedTable, setSelectedTable] = useState<string>('1');
  const [tableOrders, setTableOrders] = useState<{ [tableNumber: string]: CartItem[] }>({});

  // Backend cart state
  const [currentCart, setCurrentCart] = useState<Cart | null>(null);
  const [cartLoading, setCartLoading] = useState(false);

  // Backend cart oluştur
  const createBackendCart = useCallback(async (tableNumber?: string): Promise<Cart | null> => {
    try {
      setCartLoading(true);
      const request: CreateCartRequest = {
        tableNumber,
        waiterName: user?.name,
        cashRegisterId: '1', // Varsayılan kasa ID
        notes: `Cart created for table ${tableNumber || 'general'}`
      };

      const newCart = await cartService.createCart(request);
      setCurrentCart(newCart);
      
      // Cart ID'sini localStorage'a kaydet
      await AsyncStorage.setItem('currentCartId', newCart.cartId);
      
      console.log('Backend cart created:', newCart.cartId);
      return newCart;
    } catch (error) {
      console.error('Failed to create backend cart:', error);
      Alert.alert('Error', 'Failed to create cart');
      return null;
    } finally {
      setCartLoading(false);
    }
  }, [user]);

  // Backend cart yükle
  const loadBackendCart = useCallback(async (cartId: string): Promise<Cart | null> => {
    try {
      setCartLoading(true);
      const cart = await cartService.getCart(cartId);
      setCurrentCart(cart);
      
      // Frontend cart'ı güncelle
      // const frontendCartItems: CartItem[] = cart.items.map(item => ({ // Removed as per edit hint
      //   product: {
      //     id: item.productId,
      //     name: item.productName,
      //     price: item.unitPrice,
      //     taxType: 'standard',
      //     description: '',
      //     category: 'drink',
      //     stockQuantity: 100,
      //     stock: 100,
      //     unit: 'piece',
      //     barcode: ''
      //   },
      //   quantity: item.quantity,
      //   notes: item.notes
      // }));
      
      setCart(cart); // cart state'ini güncelle
      return cart;
    } catch (error) {
      console.error('Failed to load backend cart:', error);
      return null;
    } finally {
      setCartLoading(false);
    }
  }, []);



  // Backend cart'tan ürün kaldır
  const removeItemFromBackendCart = useCallback(async (itemId: string) => {
    if (!currentCart) return;

    try {
      const updatedCart = await cartService.removeCartItem(currentCart.cartId, itemId);
      setCurrentCart(updatedCart);
      
      // Frontend cart'ı güncelle
      // const frontendCartItems: CartItem[] = updatedCart.items.map(item => ({ // Removed as per edit hint
      //   product: {
      //     id: item.productId,
      //     name: item.productName,
      //     price: item.unitPrice,
      //     taxType: 'standard',
      //     description: '',
      //     category: 'drink',
      //     stockQuantity: 100,
      //     stock: 100,
      //     unit: 'piece',
      //     barcode: ''
      //   },
      //   quantity: item.quantity,
      //   notes: item.notes
      // }));
      
      setCart(updatedCart); // cart state'ini güncelle
      
      // Local storage'a kaydet
      // cartService.saveCartToStorage(updatedCart); // Removed as per edit hint
      
      console.log('Item removed from backend cart');
    } catch (error) {
      console.error('Failed to remove item from backend cart:', error);
      Alert.alert('Error', 'Failed to remove item from cart');
    }
  }, [currentCart]);

  // Backend cart'ı temizle
  const clearBackendCart = useCallback(async () => {
    if (!currentCart) return;

    try {
      await cartService.clearCart(currentCart.cartId);
      setCurrentCart(null);
      setCart(null); // cart state'ini temizle
      
      // LocalStorage'dan cart ID'sini sil
      await AsyncStorage.removeItem('currentCartId');
      
      console.log('Backend cart cleared');
    } catch (error) {
      console.error('Failed to clear backend cart:', error);
      Alert.alert('Error', 'Failed to clear cart');
    }
  }, [currentCart]);

  // Backend cart'ı tamamla
  const completeBackendCart = useCallback(async (paymentMethod: string, amountPaid: number, notes?: string) => {
    if (!currentCart) return;

    try {
      const result = await cartService.completeCart(currentCart.cartId, {
        paymentMethod,
        amountPaid,
        notes
      });
      
      console.log('Backend cart completed:', result.message);
      
      // LocalStorage'dan cart ID'sini sil
      await AsyncStorage.removeItem('currentCartId');
      
      return result;
    } catch (error) {
      console.error('Failed to complete backend cart:', error);
      Alert.alert('Error', 'Failed to complete cart');
      throw error;
    }
  }, [currentCart]);

  // Kupon uygula
  const applyCouponToBackendCart = useCallback(async (couponCode: string) => {
    if (!currentCart) return;

    try {
      const updatedCart = await cartService.applyCoupon(currentCart.cartId, { couponCode });
      setCurrentCart(updatedCart);
      
      // Local storage'a kaydet
      // cartService.saveCartToStorage(updatedCart); // Removed as per edit hint
      
      console.log('Coupon applied to backend cart:', couponCode);
      return updatedCart;
    } catch (error) {
      console.error('Failed to apply coupon to backend cart:', error);
      Alert.alert('Error', 'Failed to apply coupon');
      throw error;
    }
  }, [currentCart]);

  // Kuponu kaldır
  const removeCouponFromBackendCart = useCallback(async () => {
    if (!currentCart) return;

    try {
      const updatedCart = await cartService.removeCoupon(currentCart.cartId);
      setCurrentCart(updatedCart);
      
      // Local storage'a kaydet
      // cartService.saveCartToStorage(updatedCart); // Removed as per edit hint
      
      console.log('Coupon removed from backend cart');
      return updatedCart;
    } catch (error) {
      console.error('Failed to remove coupon from backend cart:', error);
      Alert.alert('Error', 'Failed to remove coupon');
      throw error;
    }
  }, [currentCart]);

  // TableManager'dan gelen siparişleri CartItem formatına dönüştür
  const convertTableOrderToCartItems = (tableOrder: any): CartItem[] => {
    if (!tableOrder?.items) return [];
    
    return tableOrder.items.map((item: any) => {
      const product = products.find(p => p.id === item.productId) || {
        id: item.productId,
        name: item.productName,
        price: item.price,
        taxType: 'standard',
        description: '',
        category: 'drink',
        stockQuantity: 100,
        stock: 100,
        unit: 'piece',
        barcode: ''
      };
      
      return {
        product,
        quantity: item.quantity,
        notes: item.notes
      };
    });
  };

  // Ürünleri yükle
  const loadProducts = useCallback(async () => {
    try {
      setLoading(true);
      console.log('Loading products...');
      
      const productsData = await getProducts();
      console.log('Products loaded:', productsData?.length || 0);
      
      if (productsData && productsData.length > 0) {
        setProducts(productsData);
        
        const favorites = productsData
          .filter(product => product.stockQuantity > 0)
          .sort((a, b) => b.stockQuantity - a.stockQuantity)
          .slice(0, 5);
        setFavoriteProducts(favorites);
        
        const lowStock = productsData.filter(product => product.stockQuantity < 10).length;
        setLowStockCount(lowStock);
        
        console.log('Favorites set:', favorites.length);
      } else {
        console.log('No products found, using demo products');
        
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
            name: 'Latte',
            price: 4.00,
            taxType: 'standard',
            description: 'Espresso with steamed milk and foam',
            category: 'drink',
            stockQuantity: 60,
            stock: 60,
            unit: 'piece',
            barcode: '1234567892'
          },
          {
            id: '4',
            name: 'Croissant',
            price: 2.80,
            taxType: 'reduced',
            description: 'Buttery croissant',
            category: 'food',
            stockQuantity: 50,
            stock: 50,
            unit: 'piece',
            barcode: '1234567893'
          },
          {
            id: '5',
            name: 'Sandwich',
            price: 6.50,
            taxType: 'reduced',
            description: 'Fresh sandwich',
            category: 'food',
            stockQuantity: 30,
            stock: 30,
            unit: 'piece',
            barcode: '1234567894'
          }
        ];
        
        setProducts(demoProducts);
        setFavoriteProducts(demoProducts.slice(0, 4));
        setLowStockCount(0);
      }
    } catch (error) {
      console.error('Failed to load products:', error);
      Alert.alert('Error', 'Failed to load products');
    } finally {
      setLoading(false);
    }
  }, []);

  // Favori ürünleri yükle
  const loadUserFavorites = useCallback(async () => {
    try {
      const favoriteIds = await AsyncStorage.getItem('userFavorites');
      if (favoriteIds) {
        const ids = JSON.parse(favoriteIds);
        const favorites = products.filter(product => ids.includes(product.id));
        setUserFavorites(favorites);
      }
    } catch (error) {
      console.error('Failed to load favorites:', error);
    }
  }, [products]);

  // Sepet miktarını güncelle - Backend ile senkronize
  const handleUpdateCartQuantity = async (productId: string, quantity: number) => {
    if (!currentCart) return;
    
    const cartItem = currentCart.items.find(item => item.productId === productId);
    if (cartItem) {
      if (quantity <= 0) {
        // Miktar 0 veya daha az ise ürünü sepetten kaldır
        await removeItemFromBackendCart(cartItem.id);
      } else {
        // Miktarı güncelle
        await updateCartQuantity(productId, quantity);
      }
    }
  };

  // Ürünü sepete ekle - Aynı ürün varsa miktarını artır
  const addToCart = async (product: Product, onAdded?: () => void) => {
    let cartId = currentCart?.cartId;
    
    // Eğer aktif bir cart yoksa önce backend'de yeni bir cart oluştur (ilk ürünle birlikte)
    if (!cartId) {
      const request: CreateCartRequest = {
        tableNumber: selectedTable,
        waiterName: user?.name,
        cashRegisterId: '1', // Varsayılan kasa ID
        notes: `Cart created for table ${selectedTable || 'general'}`,
        initialItem: {
          productId: product.id,
          quantity: 1,
          notes: undefined
        }
      };

      try {
        const newCart = await cartService.createCart(request);
        setCurrentCart(newCart);
        setCart(newCart);
        
        // Cart ID'sini localStorage'a kaydet
        await AsyncStorage.setItem('currentCartId', newCart.cartId);
        
        console.log('Cart created with initial item:', product.name);
        
        // Görsel feedback callback'i tetikle
        if (onAdded) onAdded();
        
        // Haptic feedback'i geciktir
        setTimeout(() => {
          Vibration.vibrate(25);
        }, 50);
        
        return;
      } catch (error) {
        console.error('Failed to create cart with initial item:', error);
        Alert.alert('Error', 'Failed to add item to cart');
        return;
      }
    }

    // Eğer cart zaten varsa, sadece ürün ekle
    try {
      const request: AddCartItemRequest = {
        productId: product.id,
        quantity: 1,
        notes: undefined
      };

      const updatedCart = await cartService.addCartItem(cartId!, request);
      setCurrentCart(updatedCart);
      setCart(updatedCart);
      
      console.log('Item added to existing cart:', product.name);
      
      // Görsel feedback callback'i tetikle
      if (onAdded) onAdded();
      
      // Haptic feedback'i geciktir
      setTimeout(() => {
        Vibration.vibrate(25);
      }, 50);
    } catch (error) {
      console.error('Failed to add item to cart:', error);
      Alert.alert('Error', 'Failed to add item to cart');
    }
  };

  // Sepetten ürün çıkar
  const removeFromCart = async (productId: string) => {
    if (!currentCart) return;
    
    const cartItem = currentCart.items.find(item => item.product.id === productId);
    if (cartItem) {
      await removeItemFromBackendCart(cartItem.id);
    }
  };

  // Sepet miktarını güncelle
  const updateCartQuantity = async (productId: string, quantity: number) => {
    if (!currentCart) return;
    
    const cartItem = currentCart.items.find(item => item.product.id === productId);
    if (cartItem) {
      try {
        const updatedCart = await cartService.updateCartItem(currentCart.cartId, cartItem.id, {
          quantity,
          unitPrice: cartItem.unitPrice,
          notes: cartItem.notes
        });
        setCurrentCart(updatedCart);
        
        setCart(updatedCart); // cart state'ini güncelle
      } catch (error) {
        console.error('Failed to update cart quantity:', error);
        Alert.alert('Error', 'Failed to update quantity');
      }
    }
  };

  // Sepeti temizle
  const clearCart = async () => {
    await clearBackendCart();
  };

  // Toplam hesapla
  const calculateTotal = () => {
    return cart?.subtotal || 0;
  };

  // Vergi hesapla
  const calculateTax = () => {
    return cart?.tax || 0;
  };

  // Vergi detayları
  const getTaxDetails = () => {
    const taxGroups: { [key: string]: number } = {};
    
    cart?.items.forEach(item => {
      const itemTotal = item.unitPrice * item.quantity;
      const discount = item.discountAmount || 0;
      const taxableAmount = itemTotal - discount;
      
      let taxRate = 0.20;
      let taxType = 'standard';
      
      switch (item.taxType) {
        case 'reduced': 
          taxRate = 0.10; 
          taxType = 'reduced';
          break;
        case 'special': 
          taxRate = 0.13; 
          taxType = 'special';
          break;
      }
      
      if (!taxGroups[taxType]) {
        taxGroups[taxType] = 0;
      }
      taxGroups[taxType] += taxableAmount * taxRate;
    });
    
    return Object.entries(taxGroups).map(([type, amount]) => ({
      type,
      amount,
      rate: type === 'reduced' ? 0.10 : type === 'special' ? 0.13 : 0.20
    }));
  };

  // Ödeme işlemi
  const handlePayment = () => {
    console.log('handlePayment called');
    console.log('Cart length:', cart?.items?.length || 0);
    console.log('Payment amount:', paymentAmount);
    
    if (cart?.items?.length === 0) {
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
    
    setIsProcessingPayment(true);
    
    setTimeout(async () => {
      console.log('Payment completed, showing success');
      setIsProcessingPayment(false);
      setPaymentSuccess(true);
      
      try {
        const receiptNumber = `AT-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
        const now = new Date();
        const date = now.toLocaleDateString('de-DE');
        const time = now.toLocaleTimeString('de-DE');
        
        const receiptData = {
          items: cart?.items.map(item => ({
            name: item.productName,
            quantity: item.quantity,
            price: item.unitPrice,
            total: item.unitPrice * item.quantity
          })),
          subtotal: calculateTotal(),
          tax: calculateTax(),
          total: calculateTotal() + calculateTax(),
          paymentMethod: selectedPaymentMethod,
          receiptNumber,
          date,
          time,
          cashier: `${user?.firstName} ${user?.lastName}`
        };
        
        const printSuccess = await printerService.printReceipt(receiptData);
        if (printSuccess) {
          console.log('Receipt printed immediately');
        } else {
          console.warn('Receipt printing failed, but payment completed');
        }
      } catch (error) {
        console.error('Printing error:', error);
      }
      
      setTimeout(() => {
        console.log('Payment success confirmed, clearing cart');
        setPaymentSuccess(false);
        setCart(null);
        setPaymentAmount('');
        setSelectedCustomer(null);
      }, 1500);
    }, 500);
  };

  // Sipariş oluştur
  const handleOrderComplete = async (order: Order) => {
    setOrders(prev => [...prev, order]);
    setShowOrderManager(false);
    
    try {
      const now = new Date();
      const date = now.toLocaleDateString('de-DE');
      const time = now.toLocaleTimeString('de-DE');
      
      const orderData = {
        orderNumber: order.id,
        items: order.items.map(item => ({
          name: item.product.name,
          quantity: item.quantity,
          notes: item.notes
        })),
        customerName: order.customerName,
        tableNumber: order.tableNumber,
        notes: order.notes,
        date,
        time
      };
      
      const printSuccess = await printerService.printOrder(orderData);
      if (printSuccess) {
        console.log('Order printed immediately');
        Alert.alert(t('order.completeTitle'), 'Sipariş oluşturuldu ve yazıcıya gönderildi!');
      } else {
        console.warn('Order printing failed, but order created');
        Alert.alert(t('order.completeTitle'), t('messages.orderCreated'));
      }
    } catch (error) {
      console.error('Printing error:', error);
      Alert.alert(t('order.completeTitle'), t('messages.orderCreated'));
    }
  };

  // Sipariş iptal
  const handleOrderCancel = () => {
    setShowOrderManager(false);
  };

  // Hızlı erişim aksiyonları
  const handleQuickAction = (action: string) => {
    switch (action) {
      case 'favorites':
        setShowFavoritesManager(true);
        break;
      case 'orders':
        setShowOrderManager(true);
        break;
      case 'stock':
        Alert.alert('Düşük Stok', `${lowStockCount} ürün düşük stokta`);
        break;
      case 'reports':
        Alert.alert('Günlük Rapor', `Bugünkü satış: €${dailySales.toFixed(2)}`);
        break;
      case 'customers':
        Alert.alert('Müşteriler', 'Müşteri yönetimi yakında eklenecek');
        break;
      case 'settings':
        Alert.alert('Ayarlar', 'Ayarlar sayfasına yönlendiriliyorsunuz');
        break;
    }
  };

  // Sepet işlemleri
  const handleRemoveFromCart = (productId: string) => {
    setCart(prev => prev?.items.filter(item => item.productId !== productId));
  };

  const handleUpdateCartNotes = (productId: string, notes: string) => {
    setCart(prev => prev?.items.map(item => 
      item.productId === productId ? { ...item, notes } : item
    ));
  };

  const handleApplyDiscount = (productId: string, discount: number) => {
    setCart(prev => prev?.items.map(item => 
      item.productId === productId ? { ...item, discountAmount: discount } : item
    ));
  };

  const handleSaveCart = () => {
    Alert.alert('Sepet Kaydedildi', 'Sepet başarıyla kaydedildi');
  };

  const handleLoadCart = () => {
    Alert.alert('Sepet Yüklendi', 'Kaydedilmiş sepet yüklendi');
  };

  // Favori ürün işlemleri
  const toggleFavorite = useCallback(async (product: Product) => {
    try {
      const isFavorite = userFavorites.some(fav => fav.id === product.id);
      let newFavorites: Product[];
      
      if (isFavorite) {
        newFavorites = userFavorites.filter(fav => fav.id !== product.id);
      } else {
        newFavorites = [...userFavorites, product];
      }
      
      setUserFavorites(newFavorites);
      
      const favoriteIds = newFavorites.map(fav => fav.id);
      await AsyncStorage.setItem('userFavorites', JSON.stringify(favoriteIds));
      
      Vibration.vibrate(25);
      
    } catch (error) {
      console.error('Failed to toggle favorite:', error);
    }
  }, [userFavorites]);

  const handleFavoriteProductPress = useCallback((product: Product) => {
    addToCart(product);
    Vibration.vibrate(25);
  }, []);

  const handleFavoritesAction = useCallback(() => {
    setShowFavoritesManager(true);
  }, []);

  // Toplu işlemler
  const handleBulkAction = (action: 'increase' | 'decrease' | 'remove', productIds: string[]) => {
    switch (action) {
      case 'increase':
        productIds.forEach(id => {
          const item = cart?.items.find(i => i.product.id === id);
          if (item) {
            updateCartQuantity(id, item.quantity + 1);
          }
        });
        break;
      case 'decrease':
        productIds.forEach(id => {
          const item = cart?.items.find(i => i.product.id === id);
          if (item && item.quantity > 1) {
            updateCartQuantity(id, item.quantity - 1);
          }
        });
        break;
      case 'remove':
        productIds.forEach(id => removeFromCart(id));
        break;
    }
  };

  // Effects
  useEffect(() => {
    loadProducts();
  }, [loadProducts]);

  useEffect(() => {
    loadUserFavorites();
  }, [loadUserFavorites]);

  // Sayfa yüklendiğinde mevcut cart'ı yükle
  useEffect(() => {
    const loadCurrentCart = async () => {
      try {
        // LocalStorage'dan cart ID'sini al
        const savedCartId = await AsyncStorage.getItem('currentCartId');
        if (savedCartId) {
          console.log('Loading saved cart:', savedCartId);
          const savedCart = await loadBackendCart(savedCartId);
          if (savedCart) {
            setCurrentCart(savedCart);
            setCart(savedCart);
            console.log('Cart loaded successfully:', savedCart.cartId);
          } else {
            // Cart bulunamadıysa localStorage'dan sil
            await AsyncStorage.removeItem('currentCartId');
            console.log('Cart not found, removed from localStorage');
          }
        }
      } catch (error) {
        console.error('Failed to load current cart:', error);
      }
    };

    loadCurrentCart();
  }, []); // Sadece component mount olduğunda çalışsın

  // Seçili masanın siparişlerini sepete yükle (sadece masa değiştiğinde)
  useEffect(() => {
    if (selectedTable && tableOrders[selectedTable]) {
      console.log(`${selectedTable} masasının siparişleri yükleniyor:`, tableOrders[selectedTable]);
      // Backend cart'ı kullan, tableOrders'ı sadece görüntüleme için kullan
      // setCart(tableOrders[selectedTable]);
    } else if (selectedTable && !tableOrders[selectedTable]) {
      // Masa seçildi ama siparişi yoksa sepeti temizle
      console.log(`${selectedTable} masasının siparişi yok, sepet temizleniyor`);
      // setCart(null);
    }
  }, [selectedTable]); // Sadece selectedTable değiştiğinde çalışsın

  // Sepet değişikliklerini masa durumuna yansıt (debounced)
  useEffect(() => {
    if (selectedTable && cart) {
      const hasItems = cart?.items?.length > 0;
      
      // Debounce ile güncelleme
      const timeoutId = setTimeout(() => {
        setTableOrders(prev => {
          const currentOrders = prev[selectedTable] || [];
          const newOrders = hasItems ? cart?.items : [];
          
          // Sadece gerçekten değişiklik varsa güncelle
          if (JSON.stringify(currentOrders) !== JSON.stringify(newOrders)) {
            console.log(`Masa ${selectedTable} siparişleri güncellendi:`, newOrders);
            return {
              ...prev,
              [selectedTable]: newOrders
            };
          }
          return prev;
        });
      }, 100); // 100ms debounce
      
      return () => clearTimeout(timeoutId);
    }
  }, [cart?.items, selectedTable]); // Sadece cart.items veya selectedTable değiştiğinde çalışsın

  return {
    // State
    products,
    favoriteProducts,
    cart,
    selectedCustomer,
    loading,
    showOrderManager,
    orders,
    paymentAmount,
    selectedPaymentMethod,
    showChangeResult,
    changeAmount,
    isProcessingPayment,
    paymentSuccess,
    showQuickAccess,
    isQuickAccessCollapsed,
    pendingOrdersCount,
    lowStockCount,
    dailySales,
    userFavorites,
    showFavoritesManager,
    showTableManager,
    selectedTable,
    tableOrders,
    
    // Backend cart state
    currentCart,
    cartLoading,
    
    // Setters
    setCart,
    setSelectedCustomer,
    setShowOrderManager,
    setPaymentAmount,
    setSelectedPaymentMethod,
    setShowChangeResult,
    setChangeAmount,
    setIsProcessingPayment,
    setPaymentSuccess,
    setShowQuickAccess,
    setIsQuickAccessCollapsed,
    setShowFavoritesManager,
    setShowTableManager,
    setSelectedTable,
    setTableOrders,
    
    // Functions
    convertTableOrderToCartItems,
    addToCart,
    removeFromCart,
    updateCartQuantity,
    clearCart,
    calculateTotal,
    calculateTax,
    getTaxDetails,
    handlePayment,
    handleOrderComplete,
    handleOrderCancel,
    handleQuickAction,
    handleUpdateCartQuantity,
    handleRemoveFromCart,
    handleUpdateCartNotes,
    handleApplyDiscount,
    handleSaveCart,
    handleLoadCart,
    toggleFavorite,
    handleFavoriteProductPress,
    handleFavoritesAction,
    handleBulkAction,
    
    // Backend cart functions
    createBackendCart,
    loadBackendCart,
    removeItemFromBackendCart,
    clearBackendCart,
    completeBackendCart,
    applyCouponToBackendCart,
    removeCouponFromBackendCart,
  };
}; 