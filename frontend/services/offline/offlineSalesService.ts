// Bu servis, çevrimdışı satışları localde saklar ve internet gelince otomatik olarak buluta senkronize eder.
import PouchDB from 'pouchdb-browser';

const db = new PouchDB('offline_sales');

// Satışı çevrimdışı kaydet
export async function saveSaleOffline(sale: any) {
  // Her satış benzersiz _id ile kaydedilir
  await db.put({ ...sale, _id: `${sale.receiptNumber || Date.now()}` });
}

// Tüm çevrimdışı satışları getir
export async function getOfflineSales() {
  const result = await db.allDocs({ include_docs: true });
  return result.rows.map(row => row.doc);
}

// Çevrimdışı satışları buluta senkronize et
export async function syncOfflineSales(apiSyncFn: (sale: any) => Promise<any>) {
  const sales = await getOfflineSales();
  for (const sale of sales) {
    try {
      await apiSyncFn(sale);
      await db.remove(sale);
    } catch (err) {
      // Teknik log: İngilizce
      console.error('Offline sale sync failed:', err);
    }
  }
}

// Ağ bağlantısı değişimini dinle ve otomatik senkronizasyon başlat
export function listenNetworkAndSync(apiSyncFn: (sale: any) => Promise<any>) {
  const syncIfOnline = async () => {
    if (navigator.onLine) {
      await syncOfflineSales(apiSyncFn);
    }
  };
  window.addEventListener('online', syncIfOnline);
  // Uygulama ilk açıldığında da dener
  syncIfOnline();
  return () => {
    window.removeEventListener('online', syncIfOnline);
  };
} 