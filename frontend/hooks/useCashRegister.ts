import { useState, useEffect, useCallback } from 'react';
import { Alert, Vibration } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { useTranslation } from 'react-i18next';
import { productService, Product } from '../services/api/productService';
import { customerService, Customer } from '../services/api/customerService';
import { CartItem, Order } from '../types/cart';
import printerService from '../services/PrinterService';

export const useCashRegister = (user: any) => {
  const { t } = useTranslation();
  const [products, setProducts] = useState<Product[]>([]);
  const [favoriteProducts, setFavoriteProducts] = useState<Product[]>([]);
  const [cart, setCart] = useState<CartItem[]>([]);
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

  // TableManager'dan gelen siparişleri CartItem formatına dönüştür
  const convertTableOrderToCartItems = (tableOrder: any): CartItem[] => {
    if (!tableOrder || !tableOrder.items) return [];
    
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
      
      const productsData = await productService.getAllProducts();
      console.log('Products loaded:', productsData?.length || 0);
      
      if (productsData && productsData.length > 0) {
        setProducts(productsData);
        
        const favorites = productsData
          .filter(product => product.stock > 0)
          .sort((a, b) => b.stock - a.stock)
          .slice(0, 5);
        setFavoriteProducts(favorites);
        
        const lowStock = productsData.filter(product => product.stock < 10).length;
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

  // Sepete ürün ekle
  const addToCart = (product: Product) => {
    setCart(prev => {
      const existingItem = prev.find(item => item.product.id === product.id);
      
      if (existingItem) {
        return prev.map(item =>
          item.product.id === product.id
            ? { ...item, quantity: item.quantity + 1 }
            : item
        );
      } else {
        return [...prev, { product, quantity: 1 }];
      }
    });
    
    // Haptic feedback'i geciktir
    setTimeout(() => {
      Vibration.vibrate(25);
    }, 50);
  };

  // Sepetten ürün çıkar
  const removeFromCart = (productId: string) => {
    setCart(prev => prev.filter(item => item.product.id !== productId));
  };

  // Sepet miktarını güncelle
  const updateCartQuantity = (productId: string, quantity: number) => {
    if (quantity <= 0) {
      removeFromCart(productId);
    } else {
      setCart(prev => prev.map(item =>
        item.product.id === productId ? { ...item, quantity } : item
      ));
    }
  };

  // Sepeti temizle
  const clearCart = () => {
    setCart([]);
    setPaymentAmount('');
    setSelectedCustomer(null);
  };

  // Toplam hesapla
  const calculateTotal = () => {
    return cart.reduce((sum, item) => {
      const itemTotal = item.product.price * item.quantity;
      const discount = item.discount || 0;
      return sum + (itemTotal - discount);
    }, 0);
  };

  // Vergi hesapla
  const calculateTax = () => {
    return cart.reduce((sum, item) => {
      const itemTotal = item.product.price * item.quantity;
      const discount = item.discount || 0;
      const taxableAmount = itemTotal - discount;
      
      let taxRate = 0.20;
      switch (item.product.taxType) {
        case 'reduced': taxRate = 0.10; break;
        case 'special': taxRate = 0.13; break;
      }
      
      return sum + (taxableAmount * taxRate);
    }, 0);
  };

  // Vergi detayları
  const getTaxDetails = () => {
    const taxGroups: { [key: string]: number } = {};
    
    cart.forEach(item => {
      const itemTotal = item.product.price * item.quantity;
      const discount = item.discount || 0;
      const taxableAmount = itemTotal - discount;
      
      let taxRate = 0.20;
      let taxType = 'standard';
      
      switch (item.product.taxType) {
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
          items: cart.map(item => ({
            name: item.product.name,
            quantity: item.quantity,
            price: item.product.price,
            total: item.product.price * item.quantity
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
        setCart([]);
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
  const handleUpdateCartQuantity = (productId: string, quantity: number) => {
    if (quantity <= 0) {
      setCart(prev => prev.filter(item => item.product.id !== productId));
    } else {
      setCart(prev => prev.map(item => 
        item.product.id === productId ? { ...item, quantity } : item
      ));
    }
  };

  const handleRemoveFromCart = (productId: string) => {
    setCart(prev => prev.filter(item => item.product.id !== productId));
  };

  const handleUpdateCartNotes = (productId: string, notes: string) => {
    setCart(prev => prev.map(item => 
      item.product.id === productId ? { ...item, notes } : item
    ));
  };

  const handleApplyDiscount = (productId: string, discount: number) => {
    setCart(prev => prev.map(item => 
      item.product.id === productId ? { ...item, discount } : item
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
          const item = cart.find(i => i.product.id === id);
          if (item) {
            updateCartQuantity(id, item.quantity + 1);
          }
        });
        break;
      case 'decrease':
        productIds.forEach(id => {
          const item = cart.find(i => i.product.id === id);
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

  // Seçili masanın siparişlerini sepete yükle (sadece masa değiştiğinde)
  useEffect(() => {
    if (selectedTable && tableOrders[selectedTable]) {
      console.log(`${selectedTable} masasının siparişleri yükleniyor:`, tableOrders[selectedTable]);
      setCart(tableOrders[selectedTable]);
    } else if (selectedTable && !tableOrders[selectedTable]) {
      // Masa seçildi ama siparişi yoksa sepeti temizle
      console.log(`${selectedTable} masasının siparişi yok, sepet temizleniyor`);
      setCart([]);
    }
  }, [selectedTable, tableOrders]); // selectedTable veya tableOrders değiştiğinde çalışsın

  // Sepet değişikliklerini masa durumuna yansıt (debounced)
  useEffect(() => {
    if (selectedTable) {
      const hasItems = cart.length > 0;
      
      // Debounce ile güncelleme
      const timeoutId = setTimeout(() => {
        setTableOrders(prev => {
          const currentOrders = prev[selectedTable] || [];
          const newOrders = hasItems ? cart : [];
          
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
  }, [cart, selectedTable]); // Sepet veya seçili masa değiştiğinde çalışsın

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
  };
}; 