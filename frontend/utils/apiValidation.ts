// Bu dosya, API response'larını zod ile otomatik doğrulamak ve hata yönetimini standartlaştırmak için yardımcı fonksiyonlar içerir.
import { z, ZodSchema } from 'zod';
import { Alert } from 'react-native';

/**
 * API'den gelen response'u verilen zod şeması ile doğrular.
 * Hatalıysa kullanıcıya uyarı gösterir, teknik log İngilizce olarak console'a yazılır.
 * @param data API'den gelen veri
 * @param schema Zod şeması
 * @param context Hangi endpoint/model için kullanıldığı (log için)
 * @returns Doğrulanmış veri veya null
 */
export function validateApiResponse<T>(data: unknown, schema: ZodSchema<T>, context: string): T | null {
  const result = schema.safeParse(data);
  if (!result.success) {
    // Teknik log İngilizce
    console.error(`API response validation failed for ${context}:`, result.error);
    // Kullanıcıya uyarı
    Alert.alert('Veri Hatası', 'Sunucudan beklenen formatta veri alınamadı. Lütfen tekrar deneyin.');
    return null;
  }
  return result.data;
}

/**
 * API çağrısı sırasında hata oluşursa backend'in döndürdüğü mesajı kullanıcıya gösterir.
 * Frontend kendi hata mesajı üretmez, sadece backend'in mesajını gösterir.
 * @param err AxiosError veya benzeri hata objesi
 * @param fallbackMessage Backend'den mesaj gelmezse gösterilecek mesaj
 */
export function handleApiError(err: any, fallbackMessage = 'Bilinmeyen hata') {
  const msg = err?.response?.data?.message || fallbackMessage;
  Alert.alert('Hata', msg);
  // Teknik log İngilizce
  console.error('API error:', err?.response?.data || err);
} 