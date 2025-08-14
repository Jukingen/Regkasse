import AsyncStorage from '@react-native-async-storage/async-storage';
import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { Alert, Vibration } from 'react-native';
import * as FileSystem from 'expo-file-system';
import * as Sharing from 'expo-sharing';

import printerService from '../services/PrinterService';
import { CartService, Cart, CartItem as BackendCartItem, CreateCartRequest, AddCartItemRequest } from '../services/api/cartService';
import { Customer } from '../services/api/customerService';
import { getProducts, Product } from '../services/api/productService';
import { CartItem, Order } from '../types/cart';
import { apiClient } from '../services/api/config';
// --- OFFLINE KODLAR TAMAMEN KALDIRILDI ---
// import { saveSaleOffline } from '../services/offline/offlineSalesService' satırı ve saveSaleOffline fonksiyonu ile ilgili tüm kodlar kaldırıldı.

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
  // const [currentCart, setCurrentCart] = useState<Cart | null>(null);
  // const [cartLoading, setCartLoading] = useState(false);

  // Backend cart oluştur
  const createBackendCart = useCallback(async (tableNumber?: string): Promise<Cart | null> => {
    if (!user) {
      console.log('User not logged in, cannot create cart');
      return null;
    }
    
    try {
      // setCartLoading(true);
      const request: CreateCartRequest = {
        tableNumber,
        waiterName: user?.name,
        cashRegisterId: '1', // Varsayılan kasa ID
        notes: `Cart created for table ${tableNumber || 'general'}`
      };

      const newCart = await cartService.createCart(request);
      setCart(newCart);
      
      // Cart ID'sini localStorage'a kaydet
      await AsyncStorage.setItem('currentCartId', newCart.cartId);
      
      console.log('Backend cart created:', newCart.cartId);
      return newCart;
    } catch (error) {
      console.error('Failed to create backend cart:', error);
      Alert.alert('Error', 'Failed to create cart');
      return null;
    } finally {
      // setCartLoading(false);
    }
  }, [user]);

  // Backend cart yükle
  const loadBackendCart = useCallback(async (cartId: string): Promise<Cart | null> => {
    if (!user) {
      console.log('User not logged in, cannot load cart');
      return null;
    }
    
    try {
      // setCartLoading(true);
      const cart = await cartService.getCart(cartId);
      setCart(cart);
      
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
      
      // setCart(cart); // cart state'ini güncelle
      return cart;
    } catch (error) {
      console.error('Failed to load backend cart:', error);
      return null;
    } finally {
      // setCartLoading(false);
    }
  }, []);



  // Backend cart'tan ürün kaldır
  const removeItemFromBackendCart = useCallback(async (itemId: string) => {
    if (!user || !cart) return;

    try {
      const updatedCart = await cartService.removeCartItem(cart.cartId, itemId);
      setCart(updatedCart);
      
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
      
      // setCart(updatedCart); // cart state'ini güncelle
      
      // Local storage'a kaydet
      // cartService.saveCartToStorage(updatedCart); // Removed as per edit hint
      
      console.log('Item removed from backend cart');
    } catch (error) {
      console.error('Failed to remove item from backend cart:', error);
      Alert.alert('Error', 'Failed to remove item from cart');
    }
  }, [cart]);

  // Backend cart'ı temizle
  const clearBackendCart = useCallback(async () => {
    if (!user || !cart) return;

    try {
      await cartService.clearCart(cart.cartId);
      setCart(null);
      // setCart(null); // cart state'ini temizle
      
      // LocalStorage'dan cart ID'sini sil
      await clearCartIdFromStorage();
      
      console.log('Backend cart cleared and localStorage cleaned');
    } catch (error) {
      console.error('Failed to clear backend cart:', error);
      Alert.alert('Error', 'Failed to clear cart');
    }
  }, [cart]);

  // Backend cart'ı tamamla
  const completeBackendCart = useCallback(async (paymentMethod: string, amountPaid: number, notes?: string) => {
    if (!user || !cart) return;

    try {
      const result = await cartService.completeCart(cart.cartId, {
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
  }, [cart]);

  // Kupon uygula
  const applyCouponToBackendCart = useCallback(async (couponCode: string) => {
    if (!cart) return;

    try {
      const updatedCart = await cartService.applyCoupon(cart.cartId, { couponCode });
      setCart(updatedCart);
      
      // Local storage'a kaydet
      // cartService.saveCartToStorage(updatedCart); // Removed as per edit hint
      
      console.log('Coupon applied to backend cart:', couponCode);
      return updatedCart;
    } catch (error) {
      console.error('Failed to apply coupon to backend cart:', error);
      Alert.alert('Error', 'Failed to apply coupon');
      throw error;
    }
  }, [cart]);

  // Kuponu kaldır
  const removeCouponFromBackendCart = useCallback(async () => {
    if (!cart) return;

    try {
      const updatedCart = await cartService.removeCoupon(cart.cartId);
      setCart(updatedCart);
      
      // Local storage'a kaydet
      // cartService.saveCartToStorage(updatedCart); // Removed as per edit hint
      
      console.log('Coupon removed from backend cart');
      return updatedCart;
    } catch (error) {
      console.error('Failed to remove coupon from backend cart:', error);
      Alert.alert('Error', 'Failed to remove coupon');
      throw error;
    }
  }, [cart]);

  // TableManager'dan gelen siparişleri CartItem formatına dönüştür
  const convertTableOrderToCartItems = (tableOrder: any): CartItem[] => {
    if (!tableOrder?.items) return [];
    
    return tableOrder.items.map((item: any) => {
      const product = products.find(p => p.id === item.productId) || {
        id: item.productId,
        name: item.productName,
        price: item.price,
        taxType: 'Standard',
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
            taxType: 'Standard',
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
            taxType: 'Standard',
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
            taxType: 'Standard',
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
            taxType: 'Reduced',
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
            taxType: 'Reduced',
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
    if (!cart) return;
    
    const cartItem = cart.items.find(item => item.product.id === productId);
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
    try {
      // Önce localStorage'dan mevcut cartId'yi kontrol et
      let cartId = await getCurrentCartId();
      
      if (!cartId) {
        // 1. Yeni cart oluştur
        console.log('Creating new cart for product:', product.name);
        const newCart = await cartService.createCart({
          tableNumber: selectedTable,
          waiterName: user?.name,
          cashRegisterId: '1',
          notes: `Cart created for table ${selectedTable || 'general'}`
        });
        
        // 2. CartId'yi localStorage'a kaydet ve state'i güncelle
        await saveCartIdToStorage(newCart.cartId);
        setCart(newCart); // FE state'i hemen güncelle
        cartId = newCart.cartId; // local değişkeni güncelle
        
        console.log('New cart created and saved to localStorage:', newCart.cartId);
        
        // 3. İlk ürünü ekle
        console.log('Adding first item to cart:', product.name);
        await cartService.addCartItem(cartId, {
          productId: product.id,
          quantity: 1,
          notes: undefined
        });
        
        // 4. Cart'ı tekrar fetch et ve state'i güncelle
        console.log('Fetching updated cart from backend');
        const freshCart = await cartService.getCart(cartId);
        setCart(freshCart);
        
        console.log('Cart created, item added, and fetched successfully:', {
          cartId: freshCart.cartId,
          itemCount: freshCart.items.length,
          productName: product.name
        });
        
        if (onAdded) onAdded();
        setTimeout(() => { Vibration.vibrate(25); }, 50);
        return;
      }
      
      // Eğer cart zaten varsa, sadece ürün ekle
      console.log('Adding item to existing cart:', product.name, 'CartId:', cartId);
      const request: AddCartItemRequest = {
        productId: product.id,
        quantity: 1,
        notes: undefined
      };
      
      await cartService.addCartItem(cartId, request);
      
      // Cart'ı backend'den tekrar fetch et
      console.log('Fetching updated cart from backend');
      const freshCart = await cartService.getCart(cartId);
      setCart(freshCart);
      
      console.log('Item added and cart fetched successfully:', {
        cartId: freshCart.cartId,
        itemCount: freshCart.items.length,
        productName: product.name
      });
      
      if (onAdded) onAdded();
      setTimeout(() => { Vibration.vibrate(25); }, 50);
      
    } catch (error: any) {
      // Eğer hata 404 ise (cartId expired), yeni cart oluşturup tekrar dene
      if (error?.message?.includes('404') || error?.message?.includes('not found')) {
        console.warn('Cart expired, creating new cart...');
        await clearCartIdFromStorage();
        setCart(null);
        
        // Yeni cart oluştur ve ürünü tekrar ekle
        const newCart = await cartService.createCart({
          tableNumber: selectedTable,
          waiterName: user?.name,
          cashRegisterId: '1',
          notes: `Cart created for table ${selectedTable || 'general'}`
        });
        
        await saveCartIdToStorage(newCart.cartId);
        setCart(newCart);
        const cartId = newCart.cartId;
        
        await cartService.addCartItem(cartId, {
          productId: product.id,
          quantity: 1,
          notes: undefined
        });
        
        const freshCart = await cartService.getCart(cartId);
        setCart(freshCart);
        
        console.log('New cart created and item added successfully:', {
          cartId: freshCart.cartId,
          itemCount: freshCart.items.length,
          productName: product.name
        });
        
        if (onAdded) onAdded();
        setTimeout(() => { Vibration.vibrate(25); }, 50);
      } else {
        console.error('Failed to add item to cart:', error);
        Alert.alert('Error', 'Failed to add item to cart');
      }
    }
  };

  // Sepetten ürün çıkar
  const removeFromCart = async (productId: string) => {
    if (!cart) return;
    
    const cartItem = cart.items.find(item => item.product.id === productId);
    if (cartItem) {
      await removeItemFromBackendCart(cartItem.id);
    }
  };

  // Sepet miktarını güncelle
  const updateCartQuantity = async (productId: string, quantity: number) => {
    if (!cart) return;
    
    const cartItem = cart.items.find(item => item.product.id === productId);
    if (cartItem) {
      try {
        const updatedCart = await cartService.updateCartItem(cart.cartId, cartItem.id, {
          quantity,
          unitPrice: cartItem.unitPrice,
          notes: cartItem.notes
        });
        setCart(updatedCart);
        
        // setCart(updatedCart); // cart state'ini güncelle
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

  // Sepeti tamamen sıfırla (FE state)
  const resetCart = async () => {
    setCart(null);
    await clearCartIdFromStorage();
    console.log('Cart reset and localStorage cleaned');
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
      let taxType = 'Standard';
      
      switch (item.taxType) {
        case 'Reduced': 
          taxRate = 0.10; 
          taxType = 'Reduced';
          break;
        case 'Special': 
          taxRate = 0.13; 
          taxType = 'Special';
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
      rate: type === 'Reduced' ? 0.10 : type === 'Special' ? 0.13 : 0.20
    }));
  };

  // Ödeme işlemi
  const handlePayment = async () => {
    if (!cart || cart.items.length === 0) {
      Alert.alert(t('errors.emptyCart'), t('messages.emptyCart'));
      return;
    }

    const totalWithTax = calculateTotal() + calculateTax();
    if (!paymentAmount || parseFloat(paymentAmount) < totalWithTax) {
      Alert.alert(t('errors.invalidAmount'), t('validation.invalidAmount'));
      return;
    }
    
    setIsProcessingPayment(true);
    try {
      // InvoiceCreateRequest için JSON hazırla
      const invoiceRequest = {
        customerName: selectedCustomer?.name || '',
        customerEmail: selectedCustomer?.email || '',
        customerPhone: selectedCustomer?.phone || '',
        customerAddress: selectedCustomer?.address || '',
        customerTaxNumber: selectedCustomer?.taxNumber || '',
        dueDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000),
        subtotal: cart.subtotal,
        taxAmount: cart.taxAmount,
        totalAmount: cart.totalAmount,
          paymentMethod: selectedPaymentMethod,
        paymentReference: null,
        companyName: '', // Gerekirse ayarlardan alınabilir
        companyAddress: '',
        companyPhone: '',
        companyEmail: '',
        companyTaxNumber: '',
        termsAndConditions: '',
        notes: cart.notes,
        invoiceItems: cart.items.map(i => ({
          productName: i.productName,
          quantity: i.quantity,
          unitPrice: i.unitPrice,
          taxRate: i.taxRate,
          taxAmount: i.taxAmount,
          totalAmount: i.totalAmount,
          description: i.product?.description || ''
        })),
        customerId: selectedCustomer?.id || '',
        createdById: user?.id || 'system'
      };

      // POST /api/invoices
      const response = await apiClient.post('/invoice', invoiceRequest);
      if (!response || !response.invoice) {
        throw new Error('Invoice could not be created');
      }

      // Sepeti ve localStorage'ı temizle
        setCart(null);
      // setCart(null);
      await AsyncStorage.removeItem('currentCartId');
      setPaymentSuccess(true);
        setPaymentAmount('');
        setSelectedCustomer(null);
      Alert.alert(t('payment.success', 'Ödeme başarılı'), t('payment.completed', 'Satış ve fatura başarıyla tamamlandı.'));
    } catch (error) {
      Alert.alert(t('payment.error', 'Hata'), t('payment.failed', 'Satış/fatura tamamlanamadı.'));
    } finally {
      setIsProcessingPayment(false);
    }
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

  // Zincirleme satış tamamlama fonksiyonu
  const handleCompleteSale = async () => {
    try {
      if (!cart || cart.items.length === 0) {
        Alert.alert('Hata', 'Sepet boş!');
        return;
      }
      if (!selectedCustomer) {
        Alert.alert('Hata', 'Müşteri seçilmedi!');
        return;
      }

      const saleRequest = {
        customerId: selectedCustomer.id,
        items: cart.items.map(item => ({
          productId: item.product.id,
          quantity: item.quantity,
          taxType: item.product.taxType,
        })),
        payment: {
          method: selectedPaymentMethod,
          tse_required: true,
        }
      };

      // 1. Satış/Invoice API'ye gönder
      const response = await apiClient.post('/invoices', saleRequest);
      if (!response || !response.invoice) {
        throw new Error('Fatura oluşturulamadı');
      }
      const invoice = response.invoice;

      // 2. PDF, CSV, JSON dosyalarını hazırla ve indirilebilir yap
      // PDF
              const pdfBlob = await apiClient.get(`/invoices/${invoice.id}/pdf`, { responseType: 'blob' });
      const pdfUri = FileSystem.documentDirectory + `invoice_${invoice.id}.pdf`;
      await FileSystem.writeAsStringAsync(pdfUri, pdfBlob, { encoding: FileSystem.EncodingType.Base64 });
      // CSV
              const csvBlob = await apiClient.get(`/invoices/${invoice.id}/csv`, { responseType: 'blob' });
      const csvUri = FileSystem.documentDirectory + `invoice_${invoice.id}.csv`;
      await FileSystem.writeAsStringAsync(csvUri, csvBlob, { encoding: FileSystem.EncodingType.Base64 });
      // JSON
      const jsonUri = FileSystem.documentDirectory + `invoice_${invoice.id}.json`;
      await FileSystem.writeAsStringAsync(jsonUri, JSON.stringify(invoice), { encoding: FileSystem.EncodingType.UTF8 });

      // 3. PDF'i anında yazıcıya gönder (Bluetooth/USB)
      await printerService.printReceipt({
        ...invoice,
        font: 'OCRA-B',
        pdfUri,
      });

      // 4. Sepeti ve ödeme ekranını sıfırla
      setCart(null);
      // setCart(null);
      setSelectedCustomer(null);
      setPaymentAmount('');
      await AsyncStorage.removeItem('currentCartId');

      // 5. Kullanıcıya başarılı bildirim göster
      Alert.alert('Satış tamamlandı', 'Fatura başarıyla oluşturuldu ve yazıcıya gönderildi!');

      // 6. Dosyaları paylaşılabilir yap (isteğe bağlı)
      await Sharing.shareAsync(pdfUri, { mimeType: 'application/pdf' });
      await Sharing.shareAsync(csvUri, { mimeType: 'text/csv' });
      await Sharing.shareAsync(jsonUri, { mimeType: 'application/json' });

    } catch (error: any) {
      Alert.alert('Hata', error.message || 'Satış tamamlanamadı!');
    }
  };

  // Effects - Sadece kullanıcı login olduktan sonra ürünleri yükle
  useEffect(() => {
    if (user) {
      loadProducts();
    }
  }, [user, loadProducts]);

  useEffect(() => {
    if (user) {
      loadUserFavorites();
    }
  }, [user, loadUserFavorites]);

  // Sayfa yüklendiğinde mevcut cart'ı backend'den mutlaka fetch et - Sadece kullanıcı login olduktan sonra
  useEffect(() => {
    if (user) {
      const loadCurrentCart = async () => {
        try {
          const savedCartId = await AsyncStorage.getItem('currentCartId');
          if (savedCartId) {
            console.log('Loading saved cart from localStorage:', savedCartId);
            // Her zaman backend'den fetch et
            const savedCart = await cartService.getCart(savedCartId);
            setCart(savedCart);
            console.log('Cart loaded successfully from localStorage:', savedCart.cartId);
          } else {
            console.log('No saved cart found in localStorage');
            setCart(null);
          }
        } catch (error) {
          console.error('Failed to load saved cart:', error);
          // Hatalı cartId'yi localStorage'dan temizle
          await AsyncStorage.removeItem('currentCartId');
          setCart(null);
        }
      };

      loadCurrentCart();
    }
  }, [user]);

  // CartId'yi localStorage'dan güvenli şekilde al
  const getCurrentCartId = useCallback(async (): Promise<string | null> => {
    try {
      const savedCartId = await AsyncStorage.getItem('currentCartId');
      if (savedCartId) {
        console.log('Current cartId from localStorage:', savedCartId);
        return savedCartId;
      }
      console.log('No cartId found in localStorage');
      return null;
    } catch (error) {
      console.error('Failed to get cartId from localStorage:', error);
      return null;
    }
  }, []);

  // CartId'yi localStorage'a güvenli şekilde kaydet
  const saveCartIdToStorage = useCallback(async (cartId: string) => {
    try {
      await AsyncStorage.setItem('currentCartId', cartId);
      console.log('CartId saved to localStorage:', cartId);
    } catch (error) {
      console.error('Failed to save cartId to localStorage:', error);
    }
  }, []);

  // CartId'yi localStorage'dan temizle
  const clearCartIdFromStorage = useCallback(async () => {
    try {
      await AsyncStorage.removeItem('currentCartId');
      console.log('CartId cleared from localStorage');
    } catch (error) {
      console.error('Failed to clear cartId from localStorage:', error);
    }
  }, []);

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
    // currentCart,
    // cartLoading,
    
    // Setters
    setCart,
    resetCart,
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
    handleCompleteSale,
    
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