import PouchDB from 'pouchdb';
import PouchDBIdb from 'pouchdb-adapter-idb';

// PouchDB'yi IndexedDB adapter'ı ile yapılandır
PouchDB.plugin(PouchDBIdb);

export interface OfflineInvoice {
  _id?: string;
  _rev?: string;
  type: 'invoice';
  receiptNumber: string;
  tseSignature: string;
  isPrinted: boolean;
  taxDetails: {
    standard: number;
    reduced: number;
    special: number;
  };
  items: Array<{
    productId: string;
    quantity: number;
    taxType: 'standard' | 'reduced' | 'special';
    price: number;
    name: string;
  }>;
  payment: {
    method: 'cash' | 'card' | 'voucher';
    amount: number;
  };
  createdAt: string;
  synced: boolean;
}

export interface OfflineDailyReport {
  _id?: string;
  _rev?: string;
  type: 'daily_report';
  date: string;
  tseSignature: string;
  cashRegisterId: string;
  receiptCount: number;
  totalAmount: number;
  cashAmount: number;
  cardAmount: number;
  voucherAmount: number;
  taxStandard: number;
  taxReduced: number;
  taxSpecial: number;
  createdAt: string;
  synced: boolean;
}

export interface OfflineProduct {
  _id?: string;
  _rev?: string;
  type: 'product';
  productId: string;
  name: string;
  price: number;
  taxType: 'standard' | 'reduced' | 'special';
  stock: number;
  synced: boolean;
}

class OfflineService {
  private db: PouchDB.Database;
  private isOnline: boolean = navigator.onLine;

  constructor() {
    this.db = new PouchDB('registrierkasse-offline', { adapter: 'idb' });
    this.setupOnlineOfflineListeners();
  }

  private setupOnlineOfflineListeners() {
    window.addEventListener('online', () => {
      this.isOnline = true;
      this.syncData();
    });

    window.addEventListener('offline', () => {
      this.isOnline = false;
    });
  }

  // Fatura işlemleri
  async saveInvoice(invoice: Omit<OfflineInvoice, '_id' | '_rev' | 'type' | 'createdAt' | 'synced'>): Promise<string> {
    try {
      const doc: OfflineInvoice = {
        ...invoice,
        type: 'invoice',
        createdAt: new Date().toISOString(),
        synced: false
      };

      const result = await this.db.put(doc);
      return result.id;
    } catch (error) {
      console.error('Fatura kaydetme hatası:', error);
      throw error;
    }
  }

  async getInvoices(): Promise<OfflineInvoice[]> {
    try {
      const result = await this.db.find({
        selector: { type: 'invoice' },
        sort: [{ createdAt: 'desc' }]
      });
      return result.docs as OfflineInvoice[];
    } catch (error) {
      console.error('Fatura listesi alma hatası:', error);
      return [];
    }
  }

  async getUnsyncedInvoices(): Promise<OfflineInvoice[]> {
    try {
      const result = await this.db.find({
        selector: { type: 'invoice', synced: false }
      });
      return result.docs as OfflineInvoice[];
    } catch (error) {
      console.error('Senkronize edilmemiş faturalar alma hatası:', error);
      return [];
    }
  }

  async markInvoiceAsSynced(id: string): Promise<void> {
    try {
      const doc = await this.db.get(id) as OfflineInvoice;
      doc.synced = true;
      await this.db.put(doc);
    } catch (error) {
      console.error('Fatura senkronizasyon işaretleme hatası:', error);
    }
  }

  // Günlük rapor işlemleri
  async saveDailyReport(report: Omit<OfflineDailyReport, '_id' | '_rev' | 'type' | 'createdAt' | 'synced'>): Promise<string> {
    try {
      const doc: OfflineDailyReport = {
        ...report,
        type: 'daily_report',
        createdAt: new Date().toISOString(),
        synced: false
      };

      const result = await this.db.put(doc);
      return result.id;
    } catch (error) {
      console.error('Günlük rapor kaydetme hatası:', error);
      throw error;
    }
  }

  async getDailyReports(): Promise<OfflineDailyReport[]> {
    try {
      const result = await this.db.find({
        selector: { type: 'daily_report' },
        sort: [{ date: 'desc' }]
      });
      return result.docs as OfflineDailyReport[];
    } catch (error) {
      console.error('Günlük rapor listesi alma hatası:', error);
      return [];
    }
  }

  async getUnsyncedDailyReports(): Promise<OfflineDailyReport[]> {
    try {
      const result = await this.db.find({
        selector: { type: 'daily_report', synced: false }
      });
      return result.docs as OfflineDailyReport[];
    } catch (error) {
      console.error('Senkronize edilmemiş günlük raporlar alma hatası:', error);
      return [];
    }
  }

  // Ürün işlemleri
  async saveProduct(product: Omit<OfflineProduct, '_id' | '_rev' | 'type' | 'synced'>): Promise<string> {
    try {
      const doc: OfflineProduct = {
        ...product,
        type: 'product',
        synced: false
      };

      const result = await this.db.put(doc);
      return result.id;
    } catch (error) {
      console.error('Ürün kaydetme hatası:', error);
      throw error;
    }
  }

  async getProducts(): Promise<OfflineProduct[]> {
    try {
      const result = await this.db.find({
        selector: { type: 'product' }
      });
      return result.docs as OfflineProduct[];
    } catch (error) {
      console.error('Ürün listesi alma hatası:', error);
      return [];
    }
  }

  async updateProductStock(productId: string, newStock: number): Promise<void> {
    try {
      const result = await this.db.find({
        selector: { type: 'product', productId }
      });

      if (result.docs.length > 0) {
        const product = result.docs[0] as OfflineProduct;
        product.stock = newStock;
        product.synced = false;
        await this.db.put(product);
      }
    } catch (error) {
      console.error('Ürün stok güncelleme hatası:', error);
    }
  }

  // Senkronizasyon işlemleri
  async syncData(): Promise<void> {
    if (!this.isOnline) {
      console.log('Çevrimdışı mod - senkronizasyon atlanıyor');
      return;
    }

    try {
      console.log('Veri senkronizasyonu başlatılıyor...');

      // Senkronize edilmemiş faturaları gönder
      const unsyncedInvoices = await this.getUnsyncedInvoices();
      for (const invoice of unsyncedInvoices) {
        try {
          // API'ye gönder
          const response = await fetch('/api/invoices', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
            },
            body: JSON.stringify({
              items: invoice.items,
              payment: invoice.payment
            })
          });

          if (response.ok) {
            await this.markInvoiceAsSynced(invoice._id!);
            console.log(`Fatura senkronize edildi: ${invoice.receiptNumber}`);
          }
        } catch (error) {
          console.error(`Fatura senkronizasyon hatası: ${invoice.receiptNumber}`, error);
        }
      }

      // Senkronize edilmemiş günlük raporları gönder
      const unsyncedReports = await this.getUnsyncedDailyReports();
      for (const report of unsyncedReports) {
        try {
          const response = await fetch('/api/tse/daily-report', {
            method: 'POST',
            headers: {
              'Content-Type': 'application/json',
            }
          });

          if (response.ok) {
            const doc = await this.db.get(report._id!) as OfflineDailyReport;
            doc.synced = true;
            await this.db.put(doc);
            console.log(`Günlük rapor senkronize edildi: ${report.date}`);
          }
        } catch (error) {
          console.error(`Günlük rapor senkronizasyon hatası: ${report.date}`, error);
        }
      }

      console.log('Veri senkronizasyonu tamamlandı');
    } catch (error) {
      console.error('Veri senkronizasyon hatası:', error);
    }
  }

  // Durum bilgileri
  async getDatabaseInfo(): Promise<{ docCount: number; updateSeq: number }> {
    try {
      return await this.db.info();
    } catch (error) {
      console.error('Veritabanı bilgisi alma hatası:', error);
      return { docCount: 0, updateSeq: 0 };
    }
  }

  async getSyncStatus(): Promise<{
    isOnline: boolean;
    unsyncedInvoices: number;
    unsyncedReports: number;
    totalDocuments: number;
  }> {
    try {
      const [unsyncedInvoices, unsyncedReports, dbInfo] = await Promise.all([
        this.getUnsyncedInvoices(),
        this.getUnsyncedDailyReports(),
        this.getDatabaseInfo()
      ]);

      return {
        isOnline: this.isOnline,
        unsyncedInvoices: unsyncedInvoices.length,
        unsyncedReports: unsyncedReports.length,
        totalDocuments: dbInfo.docCount
      };
    } catch (error) {
      console.error('Senkronizasyon durumu alma hatası:', error);
      return {
        isOnline: this.isOnline,
        unsyncedInvoices: 0,
        unsyncedReports: 0,
        totalDocuments: 0
      };
    }
  }

  // Veritabanı temizleme
  async clearDatabase(): Promise<void> {
    try {
      await this.db.destroy();
      this.db = new PouchDB('registrierkasse-offline', { adapter: 'idb' });
      console.log('Veritabanı temizlendi');
    } catch (error) {
      console.error('Veritabanı temizleme hatası:', error);
    }
  }
}

// Singleton instance
export const offlineService = new OfflineService();
export default offlineService; 