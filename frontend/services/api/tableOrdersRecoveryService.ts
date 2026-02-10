// Bu service, masa siparişleri recovery API'si ile iletişim kurar
// RKSV uyumlu güvenlik ve error handling sağlar

import { apiClient } from './config';

export interface TableOrderRecoveryItem {
  productId: string;
  productName: string;
  quantity: number;
  price: number;
  total: number;
  notes?: string;
}

export interface TableOrderRecovery {
  tableNumber?: number;
  cartId: string;
  customerName?: string;
  itemCount: number;
  totalAmount: number;
  status: string;
  createdAt: string;
  lastUpdated: string;
  items: TableOrderRecoveryItem[];
}

export interface TableOrdersRecoveryResponse {
  success: boolean;
  message: string;
  userId: string;
  tableOrders: TableOrderRecovery[];
  totalActiveTables: number;
  retrievedAt: string;
}

export class TableOrdersRecoveryService {
  private static instance: TableOrdersRecoveryService;

  public static getInstance(): TableOrdersRecoveryService {
    if (!TableOrdersRecoveryService.instance) {
      TableOrdersRecoveryService.instance = new TableOrdersRecoveryService();
    }
    return TableOrdersRecoveryService.instance;
  }

  /**
   * Kullanıcının tüm aktif masa siparişlerini getirir
   * F5 sonrası recovery için kullanılır
   * RKSV uyumlu - yalnızca kullanıcının kendi siparişleri döner
   */
  public async getAllActiveTableOrders(): Promise<TableOrdersRecoveryResponse> {
    try {
      console.log('🔄 Requesting table orders recovery from backend...');
      
      const response = await apiClient.get('/cart/table-orders-recovery');
      
      // Backend'den gelen response'u doğrula
      if (!response || typeof response !== 'object') {
        throw new Error('Invalid response format from backend');
      }

      const recoveryData = response as TableOrdersRecoveryResponse;

      if (!recoveryData.success) {
        throw new Error(recoveryData.message || 'Backend returned failure status');
      }

      // Veri yapısını doğrula
      if (!Array.isArray(recoveryData.tableOrders)) {
        throw new Error('Invalid table orders data structure');
      }

      console.log(`✅ Table orders recovery successful: ${recoveryData.totalActiveTables} active orders`);
      
      return recoveryData;
    } catch (error: any) {
      console.error('❌ Table orders recovery service error:', error);
      
      // API error'ları için detaylı bilgi
      if (error?.response) {
        const status = error.response.status;
        const message = error.response.data?.message || 'Unknown API error';
        
        if (status === 401) {
          throw new Error('Authentication required for table orders recovery');
        } else if (status === 403) {
          throw new Error('Access denied for table orders recovery');
        } else if (status === 500) {
          throw new Error('Server error during table orders recovery');
        } else {
          throw new Error(`API error (${status}): ${message}`);
        }
      }
      
      // Network veya diğer hatalar
      throw new Error(`Table orders recovery failed: ${error.message || 'Unknown error'}`);
    }
  }

  /**
   * Belirli bir masa için sipariş detaylarını filtreleyerek döndürür
   */
  public getOrderForTable(
    recoveryData: TableOrdersRecoveryResponse, 
    tableNumber: number
  ): TableOrderRecovery | null {
    if (!recoveryData?.tableOrders || !Array.isArray(recoveryData.tableOrders)) {
      return null;
    }

    return recoveryData.tableOrders.find(
      order => order.tableNumber === tableNumber
    ) || null;
  }

  /**
   * Recovery data'dan aktif masa numaralarını çıkarır
   */
  public getActiveTableNumbers(recoveryData: TableOrdersRecoveryResponse): number[] {
    if (!recoveryData?.tableOrders || !Array.isArray(recoveryData.tableOrders)) {
      return [];
    }

    return recoveryData.tableOrders
      .map(order => order.tableNumber)
      .filter((tableNumber): tableNumber is number => 
        typeof tableNumber === 'number' && tableNumber > 0
      )
      .sort((a, b) => a - b);
  }

  /**
   * Recovery data'nın geçerliliğini kontrol eder
   */
  public validateRecoveryData(data: any): data is TableOrdersRecoveryResponse {
    if (!data || typeof data !== 'object') return false;
    if (typeof data.success !== 'boolean') return false;
    if (typeof data.message !== 'string') return false;
    if (typeof data.userId !== 'string') return false;
    if (!Array.isArray(data.tableOrders)) return false;
    if (typeof data.totalActiveTables !== 'number') return false;
    if (typeof data.retrievedAt !== 'string') return false;

    // Her table order'ın geçerliliğini kontrol et
    return data.tableOrders.every((order: any) => 
      typeof order.cartId === 'string' &&
      typeof order.itemCount === 'number' &&
      typeof order.totalAmount === 'number' &&
      typeof order.status === 'string' &&
      Array.isArray(order.items)
    );
  }

  /**
   * Recovery data'nın yaşını kontrol eder (5 dakikadan eski data'yı reddeder)
   */
  public isRecoveryDataFresh(retrievedAt: string): boolean {
    try {
      const retrievedTime = new Date(retrievedAt).getTime();
      const now = Date.now();
      const fiveMinutesInMs = 5 * 60 * 1000;
      
      return (now - retrievedTime) < fiveMinutesInMs;
    } catch {
      return false;
    }
  }
}

// Singleton instance export
export const tableOrdersRecoveryService = TableOrdersRecoveryService.getInstance();
