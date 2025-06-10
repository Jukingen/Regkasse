// PouchDB import'unu geçici olarak kaldırıyorum
// import PouchDB from 'pouchdb-react-native';
import { Product } from '../api/productService';
import { PaymentRequest, PaymentResponse } from '../api/paymentService';
import { Receipt } from '../api/paymentService';

// Geçici PouchDB interface'i
interface PouchDBDatabase {
  put(doc: any): Promise<any>;
  get(id: string): Promise<any>;
  allDocs(options: any): Promise<any>;
  remove(id: string, rev: string): Promise<any>;
  destroy(): Promise<void>;
}

export interface OfflinePayment {
  id: string;
  payment: PaymentRequest;
  timestamp: string;
  status: 'pending' | 'synced' | 'failed';
  error?: string;
}

export interface OfflineReceipt {
  id: string;
  receipt: Receipt;
  timestamp: string;
  status: 'pending' | 'printed' | 'failed';
  error?: string;
}

class OfflineManager {
  private productsDB: PouchDBDatabase;
  private paymentsDB: PouchDBDatabase;
  private receiptsDB: PouchDBDatabase;
  private syncDB: PouchDBDatabase;

  constructor() {
    // Geçici olarak boş implementasyon
    this.productsDB = {} as PouchDBDatabase;
    this.paymentsDB = {} as PouchDBDatabase;
    this.receiptsDB = {} as PouchDBDatabase;
    this.syncDB = {} as PouchDBDatabase;
  }

  // Ürünleri çevrimdışı olarak kaydet
  async saveProductsOffline(products: Product[]): Promise<void> {
    try {
      // Mevcut ürünleri temizle
      const existingDocs = await this.productsDB.allDocs({ include_docs: true });
      const deletePromises = existingDocs.rows.map((row: any) => 
        this.productsDB.remove(row.id, row.value.rev)
      );
      await Promise.all(deletePromises);

      // Yeni ürünleri kaydet
      const savePromises = products.map(product => 
        this.productsDB.put({
          _id: product.id,
          ...product,
          timestamp: new Date().toISOString()
        })
      );
      await Promise.all(savePromises);

      console.log('Products saved offline:', products.length);
    } catch (error) {
      console.error('Failed to save products offline:', error);
      throw error;
    }
  }

  // Çevrimdışı ürünleri getir
  async getOfflineProducts(): Promise<Product[]> {
    try {
      const result = await this.productsDB.allDocs({ include_docs: true });
      return result.rows.map((row: any) => row.doc as Product);
    } catch (error) {
      console.error('Failed to get offline products:', error);
      return [];
    }
  }

  // Çevrimdışı ödeme kaydet
  async saveOfflinePayment(payment: PaymentRequest): Promise<string> {
    try {
      const offlinePayment: OfflinePayment = {
        id: `offline_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
        payment,
        timestamp: new Date().toISOString(),
        status: 'pending'
      };

      await this.paymentsDB.put({
        _id: offlinePayment.id,
        ...offlinePayment
      });

      console.log('Offline payment saved:', offlinePayment.id);
      return offlinePayment.id;
    } catch (error) {
      console.error('Failed to save offline payment:', error);
      throw error;
    }
  }

  // Çevrimdışı ödemeleri getir
  async getOfflinePayments(): Promise<OfflinePayment[]> {
    try {
      const result = await this.paymentsDB.allDocs({ include_docs: true });
      return result.rows.map((row: any) => row.doc as OfflinePayment);
    } catch (error) {
      console.error('Failed to get offline payments:', error);
      return [];
    }
  }

  // Çevrimdışı fiş kaydet
  async saveOfflineReceipt(receipt: Receipt): Promise<string> {
    try {
      const offlineReceipt: OfflineReceipt = {
        id: `offline_receipt_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`,
        receipt,
        timestamp: new Date().toISOString(),
        status: 'pending'
      };

      await this.receiptsDB.put({
        _id: offlineReceipt.id,
        ...offlineReceipt
      });

      console.log('Offline receipt saved:', offlineReceipt.id);
      return offlineReceipt.id;
    } catch (error) {
      console.error('Failed to save offline receipt:', error);
      throw error;
    }
  }

  // Çevrimdışı fişleri getir
  async getOfflineReceipts(): Promise<OfflineReceipt[]> {
    try {
      const result = await this.receiptsDB.allDocs({ include_docs: true });
      return result.rows.map((row: any) => row.doc as OfflineReceipt);
    } catch (error) {
      console.error('Failed to get offline receipts:', error);
      return [];
    }
  }

  // Senkronizasyon durumunu kaydet
  async saveSyncStatus(status: {
    lastSync: string;
    pendingPayments: number;
    pendingReceipts: number;
    isOnline: boolean;
  }): Promise<void> {
    try {
      await this.syncDB.put({
        _id: 'sync_status',
        ...status,
        timestamp: new Date().toISOString()
      });
    } catch (error) {
      console.error('Failed to save sync status:', error);
    }
  }

  // Senkronizasyon durumunu getir
  async getSyncStatus(): Promise<{
    lastSync: string;
    pendingPayments: number;
    pendingReceipts: number;
    isOnline: boolean;
  } | null> {
    try {
      const doc = await this.syncDB.get('sync_status');
      return doc as any;
    } catch (error) {
      console.error('Failed to get sync status:', error);
      return null;
    }
  }

  // Çevrimdışı ödemeyi senkronize et
  async syncOfflinePayment(offlinePayment: OfflinePayment): Promise<boolean> {
    try {
      // API'ye gönder
      const response = await fetch('/api/payments/process', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(offlinePayment.payment)
      });

      if (response.ok) {
        // Başarılı senkronizasyon
        await this.paymentsDB.put({
          ...offlinePayment,
          status: 'synced',
          syncedAt: new Date().toISOString()
        });
        return true;
      } else {
        // Başarısız senkronizasyon
        await this.paymentsDB.put({
          ...offlinePayment,
          status: 'failed',
          error: 'API request failed'
        });
        return false;
      }
    } catch (error) {
      console.error('Failed to sync offline payment:', error);
      // Hata durumunda
      await this.paymentsDB.put({
        ...offlinePayment,
        status: 'failed',
        error: (error as Error).message
      });
      return false;
    }
  }

  // Çevrimdışı fişi senkronize et
  async syncOfflineReceipt(offlineReceipt: OfflineReceipt): Promise<boolean> {
    try {
      // API'ye gönder
      const response = await fetch('/api/receipts/print', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(offlineReceipt.receipt)
      });

      if (response.ok) {
        // Başarılı senkronizasyon
        await this.receiptsDB.put({
          ...offlineReceipt,
          status: 'printed',
          syncedAt: new Date().toISOString()
        });
        return true;
      } else {
        // Başarısız senkronizasyon
        await this.receiptsDB.put({
          ...offlineReceipt,
          status: 'failed',
          error: 'Print request failed'
        });
        return false;
      }
    } catch (error) {
      console.error('Failed to sync offline receipt:', error);
      // Hata durumunda
      await this.receiptsDB.put({
        ...offlineReceipt,
        status: 'failed',
        error: (error as Error).message
      });
      return false;
    }
  }

  // Tüm çevrimdışı verileri senkronize et
  async syncAllOfflineData(): Promise<{
    paymentsSynced: number;
    receiptsSynced: number;
    errors: string[];
  }> {
    const errors: string[] = [];
    let paymentsSynced = 0;
    let receiptsSynced = 0;

    try {
      // Çevrimdışı ödemeleri senkronize et
      const offlinePayments = await this.getOfflinePayments();
      const pendingPayments = offlinePayments.filter(p => p.status === 'pending');

      for (const payment of pendingPayments) {
        const success = await this.syncOfflinePayment(payment);
        if (success) {
          paymentsSynced++;
        } else {
          errors.push(`Payment sync failed: ${payment.id}`);
        }
      }

      // Çevrimdışı fişleri senkronize et
      const offlineReceipts = await this.getOfflineReceipts();
      const pendingReceipts = offlineReceipts.filter(r => r.status === 'pending');

      for (const receipt of pendingReceipts) {
        const success = await this.syncOfflineReceipt(receipt);
        if (success) {
          receiptsSynced++;
        } else {
          errors.push(`Receipt sync failed: ${receipt.id}`);
        }
      }

      // Senkronizasyon durumunu güncelle
      await this.saveSyncStatus({
        lastSync: new Date().toISOString(),
        pendingPayments: offlinePayments.filter(p => p.status === 'pending').length,
        pendingReceipts: offlineReceipts.filter(r => r.status === 'pending').length,
        isOnline: true
      });

      console.log(`Sync completed: ${paymentsSynced} payments, ${receiptsSynced} receipts`);
    } catch (error) {
      console.error('Sync failed:', error);
      errors.push(`Sync error: ${(error as Error).message}`);
    }

    return { paymentsSynced, receiptsSynced, errors };
  }

  // Veritabanlarını temizle
  async clearAllData(): Promise<void> {
    try {
      await this.productsDB.destroy();
      await this.paymentsDB.destroy();
      await this.receiptsDB.destroy();
      await this.syncDB.destroy();

      // Yeniden oluştur
      this.productsDB = {} as PouchDBDatabase;
      this.paymentsDB = {} as PouchDBDatabase;
      this.receiptsDB = {} as PouchDBDatabase;
      this.syncDB = {} as PouchDBDatabase;

      console.log('All offline data cleared');
    } catch (error) {
      console.error('Failed to clear offline data:', error);
      throw error;
    }
  }
}

export const offlineManager = new OfflineManager();
export default offlineManager; 