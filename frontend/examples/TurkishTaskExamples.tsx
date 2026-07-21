/**
 * TurkishTaskExamples - Türkçe görev örnekleri
 *
 * Bu component, Türkçe görev önerilerini test etmek ve örneklerini göstermek için
 * oluşturulmuştur. Task-Master AI sisteminin Türkçe desteğini sergiler.
 *
 * @author Frontend Team
 * @version 1.0.0
 * @since 2025-01-10
 */

import { Ionicons } from '@expo/vector-icons';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { View, Text, StyleSheet, TouchableOpacity, ScrollView, Alert } from 'react-native';

import useEnhancedTaskMaster from '../hooks/useEnhancedTaskMaster';
import useTaskMaster from '../hooks/useTaskMaster';
import { TaskCategory, TaskPriority } from '../services/TaskMasterService';

const TurkishTaskExamples: React.FC = () => {
  const { i18n } = useTranslation();
  const { generateTaskSuggestions, createTask } = useTaskMaster();
  const { getAISuggestions, createEnhancedTask } = useEnhancedTaskMaster();

  const [turkishSuggestions, setTurkishSuggestions] = useState<{ [key: string]: string[] }>({});
  const [loading, setLoading] = useState<boolean>(false);

  /**
   * Türkçe dile geç ve görev önerilerini test et
   */
  const testTurkishSuggestions = async () => {
    try {
      setLoading(true);

      // Dili Türkçe'ye değiştir
      await i18n.changeLanguage('tr');
      console.log('🇹🇷 Language switched to Turkish');

      // Farklı kategorilerden örnekler al
      const categories = [
        TaskCategory.RKSV_COMPLIANCE,
        TaskCategory.TSE_INTEGRATION,
        TaskCategory.INVOICE_MANAGEMENT,
        TaskCategory.PAYMENT_PROCESSING,
      ];

      const newSuggestions: { [key: string]: string[] } = {};

      for (const category of categories) {
        // Basit öneriler
        const basicSuggestions = await generateTaskSuggestions(category);
        newSuggestions[`basic_${category}`] = basicSuggestions;

        // AI önerileri
        const aiSuggestions = await getAISuggestions(category);
        newSuggestions[`ai_${category}`] = aiSuggestions;
      }

      setTurkishSuggestions(newSuggestions);

      Alert.alert(
        '🇹🇷 Türkçe Test Tamamlandı',
        'Türkçe görev önerileri başarıyla alındı!\n\n4 kategori x 2 tip = 8 farklı öneri grubu yüklendi.',
        [{ text: 'Harika!' }]
      );
    } catch (error) {
      console.error('Turkish test failed:', error);
      Alert.alert('Hata', 'Türkçe test sırasında hata oluştu');
    } finally {
      setLoading(false);
    }
  };

  /**
   * Örnek Türkçe görevler oluştur
   */
  const createSampleTurkishTasks = async () => {
    try {
      setLoading(true);

      // Dili Türkçe'ye ayarla
      await i18n.changeLanguage('tr');

      // Örnek görevler
      const sampleTasks = [
        {
          title: 'TSE imza kontrolü yap',
          description: 'Günlük TSE cihaz imza kontrolünü gerçekleştir ve sonuçları kaydet',
          category: TaskCategory.TSE_INTEGRATION,
          priority: TaskPriority.HIGH,
          tseRequired: true,
        },
        {
          title: 'RKSV uyumluluk raporu oluştur',
          description: 'Aylık RKSV uyumluluk raporunu hazırla ve mali müfettişlik için belgele',
          category: TaskCategory.RKSV_COMPLIANCE,
          priority: TaskPriority.CRITICAL,
          tseRequired: true,
        },
        {
          title: 'Fatura şablonunu güncelle',
          description: 'Yeni RKSV gereksinimlerine göre fatura şablonunu güncelle',
          category: TaskCategory.INVOICE_MANAGEMENT,
          priority: TaskPriority.MEDIUM,
          tseRequired: false,
        },
      ];

      const createdTasks = [];

      for (const taskData of sampleTasks) {
        const task = await createTask({
          ...taskData,
          status: 'pending' as any,
          tags: ['türkçe-test', 'örnek-görev'],
        });
        createdTasks.push(task);
      }

      Alert.alert(
        '✅ Türkçe Görevler Oluşturuldu',
        `${createdTasks.length} adet Türkçe örnek görev başarıyla oluşturuldu:\n\n${createdTasks.map((t) => `• ${t?.title || 'Görev'}`).join('\n')}`,
        [{ text: 'Harika!' }]
      );
    } catch (error) {
      console.error('Turkish task creation failed:', error);
      Alert.alert('Hata', 'Türkçe görev oluşturma sırasında hata oluştu');
    } finally {
      setLoading(false);
    }
  };

  /**
   * Enhanced Türkçe görevler oluştur
   */
  const createEnhancedTurkishTasks = async () => {
    try {
      setLoading(true);

      // Dili Türkçe'ye ayarla
      await i18n.changeLanguage('tr');

      // AI önerisi al
      const aiSuggestions = await getAISuggestions(TaskCategory.RKSV_COMPLIANCE);

      if (aiSuggestions.length > 0) {
        const selectedSuggestion = aiSuggestions[0];

        // Enhanced görev oluştur
        const enhancedTask = await createEnhancedTask({
          title: selectedSuggestion,
          description: `AI Enhanced Türkçe Görev:\n\n${selectedSuggestion}\n\nBu görev yapay zeka desteği ile oluşturulmuş ve Türkçe olarak optimize edilmiştir.`,
          category: TaskCategory.RKSV_COMPLIANCE,
          priority: TaskPriority.CRITICAL,
          status: 'pending' as any,
          tseRequired: true,
          dependencies: [],
          tags: ['ai-türkçe', 'enhanced', 'rksv'],
        });

        Alert.alert(
          '🚀 Enhanced Türkçe Görev',
          `AI destekli Türkçe görev başarıyla oluşturuldu:\n\n"${selectedSuggestion}"\n\nGörev otomatik AI analizi ile geliştirildi.`,
          [{ text: 'Mükemmel!' }]
        );
      }
    } catch (error) {
      console.error('Enhanced Turkish task creation failed:', error);
      Alert.alert('Hata', 'Enhanced Türkçe görev oluşturma hatası');
    } finally {
      setLoading(false);
    }
  };

  /**
   * Kategoriye göre örnekleri göster
   */
  const showCategoryExamples = (category: TaskCategory) => {
    const categoryNames = {
      [TaskCategory.RKSV_COMPLIANCE]: 'RKSV Uyumluluk',
      [TaskCategory.TSE_INTEGRATION]: 'TSE Entegrasyonu',
      [TaskCategory.INVOICE_MANAGEMENT]: 'Fatura Yönetimi',
      [TaskCategory.PAYMENT_PROCESSING]: 'Ödeme İşleme',
      [TaskCategory.AUDIT_LOGGING]: 'Denetim Kayıtları',
      [TaskCategory.DATA_PROTECTION]: 'Veri Koruma',
      [TaskCategory.DEVELOPMENT]: 'Geliştirme',
      [TaskCategory.BUG_FIX]: 'Hata Düzeltme',
      [TaskCategory.TESTING]: 'Test',
    };

    const examples = {
      [TaskCategory.RKSV_COMPLIANCE]: [
        '🔍 TSE imza kontrolü yap',
        '📋 Mali müfettiş için belgeler hazırla',
        '📊 RKSV uyumluluk raporu oluştur',
        '✅ Vergi numarası doğrulaması kontrol et',
      ],
      [TaskCategory.TSE_INTEGRATION]: [
        '🔌 TSE cihaz bağlantısını test et',
        '⚙️ Epson-TSE konfigürasyonunu kontrol et',
        '💾 TSE yedekleme işlemi yap',
        '🔒 Gün sonu kapanışını gerçekleştir',
      ],
      [TaskCategory.INVOICE_MANAGEMENT]: [
        '📄 Fatura şablonunu güncelle',
        '🔢 Fatura numarası formatını kontrol et',
        '📁 PDF dışa aktarma işlemini optimize et',
        '💰 KDV hesaplama doğrulaması yap',
      ],
      [TaskCategory.PAYMENT_PROCESSING]: [
        '💳 Kart ödeme entegrasyonunu test et',
        '💵 Nakit ödeme iş akışını optimize et',
        '🌐 Ödeme geçidi bağlantısını kontrol et',
        '📊 İşlem günlüklerini incele',
      ],
    };

    const categoryExamples = examples[category] || [];
    const categoryName = categoryNames[category];

    Alert.alert(`🇹🇷 ${categoryName} Örnekleri`, categoryExamples.join('\n\n'), [
      { text: 'Anladım' },
    ]);
  };

  return (
    <ScrollView style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.title}>🇹🇷 Türkçe Görev Örnekleri</Text>
        <Text style={styles.subtitle}>Task-Master AI sisteminin Türkçe desteğini test edin</Text>
      </View>

      {/* Quick Test Actions */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>🚀 Hızlı Test İşlemleri</Text>

        <TouchableOpacity
          style={[styles.actionButton, { backgroundColor: '#e74c3c' }]}
          onPress={testTurkishSuggestions}
          disabled={loading}>
          <Text style={styles.actionButtonText}>🇹🇷 Türkçe Önerileri Test Et</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.actionButton, { backgroundColor: '#3498db' }]}
          onPress={createSampleTurkishTasks}
          disabled={loading}>
          <Text style={styles.actionButtonText}>✅ Örnek Türkçe Görevler Oluştur</Text>
        </TouchableOpacity>

        <TouchableOpacity
          style={[styles.actionButton, { backgroundColor: '#9b59b6' }]}
          onPress={createEnhancedTurkishTasks}
          disabled={loading}>
          <Text style={styles.actionButtonText}>🚀 Enhanced Türkçe Görev Oluştur</Text>
        </TouchableOpacity>
      </View>

      {/* Category Examples */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>📂 Kategori Örnekleri</Text>

        <View style={styles.categoryGrid}>
          <TouchableOpacity
            style={[styles.categoryCard, { borderLeftColor: '#e74c3c' }]}
            onPress={() => {
              showCategoryExamples(TaskCategory.RKSV_COMPLIANCE);
            }}>
            <Text style={styles.categoryTitle}>🛡️ RKSV Uyumluluk</Text>
            <Text style={styles.categoryDescription}>
              Yasal gereksinimler ve uyumluluk kontrolleri
            </Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[styles.categoryCard, { borderLeftColor: '#f39c12' }]}
            onPress={() => {
              showCategoryExamples(TaskCategory.TSE_INTEGRATION);
            }}>
            <Text style={styles.categoryTitle}>🔧 TSE Entegrasyonu</Text>
            <Text style={styles.categoryDescription}>Teknik güvenlik cihazı işlemleri</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[styles.categoryCard, { borderLeftColor: '#3498db' }]}
            onPress={() => {
              showCategoryExamples(TaskCategory.INVOICE_MANAGEMENT);
            }}>
            <Text style={styles.categoryTitle}>📄 Fatura Yönetimi</Text>
            <Text style={styles.categoryDescription}>Fatura oluşturma ve yönetim işlemleri</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[styles.categoryCard, { borderLeftColor: '#27ae60' }]}
            onPress={() => {
              showCategoryExamples(TaskCategory.PAYMENT_PROCESSING);
            }}>
            <Text style={styles.categoryTitle}>💳 Ödeme İşleme</Text>
            <Text style={styles.categoryDescription}>Ödeme sistemleri ve işlem yönetimi</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Results */}
      {Object.keys(turkishSuggestions).length > 0 && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>📋 Türkçe Öneriler Sonuçları</Text>

          {Object.entries(turkishSuggestions).map(([key, suggestions]) => {
            const [type, category] = key.split('_');
            const isAI = type === 'ai';

            return (
              <View key={key} style={styles.resultCard}>
                <View style={styles.resultHeader}>
                  <Text style={styles.resultTitle}>
                    {isAI ? '🤖 AI Önerileri' : '📝 Basit Öneriler'} -{' '}
                    {category.replace('_', ' ').toUpperCase()}
                  </Text>
                  <Text style={styles.resultCount}>{suggestions.length} öneri</Text>
                </View>

                {suggestions.map((suggestion, index) => (
                  <View key={index} style={styles.suggestionItem}>
                    <Text style={styles.suggestionText}>
                      {index + 1}. {suggestion}
                    </Text>
                  </View>
                ))}
              </View>
            );
          })}
        </View>
      )}

      {/* Usage Guide */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>📖 Kullanım Kılavuzu</Text>

        <View style={styles.guideCard}>
          <Text style={styles.guideTitle}>🎯 Türkçe Görev Önerilerini Nasıl Kullanırsınız?</Text>

          <Text style={styles.guideStep}>
            1️⃣ <Text style={styles.guideBold}>Dil Değiştirin:</Text> i18n dil ayarını 'tr' yapın
          </Text>

          <Text style={styles.guideStep}>
            2️⃣ <Text style={styles.guideBold}>Önerileri Alın:</Text> generateTaskSuggestions() veya
            getAISuggestions() kullanın
          </Text>

          <Text style={styles.guideStep}>
            3️⃣ <Text style={styles.guideBold}>Görev Oluşturun:</Text> Türkçe önerilerden görev
            yaratın
          </Text>

          <Text style={styles.guideStep}>
            4️⃣ <Text style={styles.guideBold}>Test Edin:</Text> Yukarıdaki butonlarla test yapın
          </Text>
        </View>

        <View style={styles.codeExample}>
          <Text style={styles.codeTitle}>💻 Kod Örneği:</Text>
          <Text style={styles.codeText}>
            {`// Dil değiştir
await i18n.changeLanguage('tr');

// Türkçe öneriler al
const turkishSuggestions = await generateTaskSuggestions(
  TaskCategory.RKSV_COMPLIANCE
);

// Sonuç: ['TSE imza kontrolü yap', 'Mali müfettiş için belgeler hazırla', ...]`}
          </Text>
        </View>
      </View>

      {loading && (
        <View style={styles.loadingOverlay}>
          <Text style={styles.loadingText}>🔄 Türkçe öneriler yükleniyor...</Text>
        </View>
      )}
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    backgroundColor: 'white',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
    alignItems: 'center',
  },
  title: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 5,
  },
  subtitle: {
    fontSize: 16,
    color: '#666',
    textAlign: 'center',
    lineHeight: 22,
  },
  section: {
    backgroundColor: 'white',
    margin: 10,
    padding: 15,
    borderRadius: 10,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 15,
  },
  actionButton: {
    padding: 15,
    borderRadius: 10,
    alignItems: 'center',
    marginBottom: 10,
  },
  actionButtonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: 'bold',
  },
  categoryGrid: {
    gap: 10,
  },
  categoryCard: {
    backgroundColor: '#f8f9fa',
    padding: 15,
    borderRadius: 10,
    borderLeftWidth: 4,
  },
  categoryTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 5,
  },
  categoryDescription: {
    fontSize: 14,
    color: '#666',
    lineHeight: 18,
  },
  resultCard: {
    backgroundColor: '#f8f9fa',
    padding: 15,
    borderRadius: 10,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  resultHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 10,
  },
  resultTitle: {
    fontSize: 14,
    fontWeight: 'bold',
    color: '#333',
    flex: 1,
  },
  resultCount: {
    fontSize: 12,
    color: '#666',
    backgroundColor: 'white',
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 10,
  },
  suggestionItem: {
    backgroundColor: 'white',
    padding: 10,
    borderRadius: 6,
    marginBottom: 5,
  },
  suggestionText: {
    fontSize: 14,
    color: '#333',
    lineHeight: 18,
  },
  guideCard: {
    backgroundColor: '#e8f5e8',
    padding: 15,
    borderRadius: 10,
    marginBottom: 10,
  },
  guideTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#2e7d32',
    marginBottom: 10,
  },
  guideStep: {
    fontSize: 14,
    color: '#2e7d32',
    lineHeight: 20,
    marginBottom: 8,
  },
  guideBold: {
    fontWeight: 'bold',
  },
  codeExample: {
    backgroundColor: '#f8f9fa',
    padding: 15,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  codeTitle: {
    fontSize: 14,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 8,
  },
  codeText: {
    fontSize: 12,
    fontFamily: 'monospace',
    color: '#333',
    backgroundColor: 'white',
    padding: 10,
    borderRadius: 4,
    lineHeight: 16,
  },
  loadingOverlay: {
    backgroundColor: 'white',
    padding: 20,
    margin: 10,
    borderRadius: 10,
    alignItems: 'center',
  },
  loadingText: {
    fontSize: 16,
    color: '#666',
    fontWeight: '600',
  },
});

export default TurkishTaskExamples;
