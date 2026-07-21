/**
 * SimpleTodo Usage Examples - Basit React Todo kullanım örnekleri
 *
 * Bu dosya, SimpleTodo componentinin farklı senaryolarda nasıl kullanılacağını gösterir.
 * React Native todo list için best practices ve RKSV-specific örnekler içerir.
 *
 * @author Frontend Team
 * @version 1.0.0
 * @since 2025-01-10
 */

import { Ionicons } from '@expo/vector-icons';
import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { View, Text, StyleSheet, TouchableOpacity, ScrollView, Alert } from 'react-native';

import SimpleTodo from '../components/SimpleTodo';

const SimpleTodoExamples: React.FC = () => {
  const { t } = useTranslation();
  const [activeExample, setActiveExample] = useState<string | null>(null);

  /**
   * Örnek kullanım senaryoları
   */
  const examples = [
    {
      id: 'basic',
      title: 'Basic Todo List',
      description: 'Temel todo list fonksiyonalitesi',
      component: (
        <SimpleTodo
          storageKey="basic_todos"
          maxItems={20}
          enableCategories={false}
          enablePriority={false}
        />
      ),
    },
    {
      id: 'rksv',
      title: 'RKSV Compliance Todos',
      description: 'RKSV uyumluluk görevleri için özelleştirilmiş',
      component: (
        <SimpleTodo
          storageKey="rksv_compliance_todos"
          maxItems={50}
          enableCategories
          enablePriority
        />
      ),
    },
    {
      id: 'minimal',
      title: 'Minimal Todo',
      description: 'Sadece temel özellikler',
      component: (
        <SimpleTodo
          storageKey="minimal_todos"
          maxItems={10}
          enableCategories={false}
          enablePriority={false}
        />
      ),
    },
  ];

  /**
   * Quick action examples
   */
  const showQuickTipsAlert = () => {
    Alert.alert(
      'SimpleTodo Kullanım İpuçları',
      `🎯 KULLANIM ÖRNEKLERİ:

📋 RKSV Günlük Kontroller:
• "TSE cihaz durumu kontrol et"
• "Dünkü fişleri kontrol et"
• "Compliance raporunu hazırla"

⚡ Hızlı Notlar:
• "Müşteri X ile görüşme"
• "Yazılım güncelleme yap"
• "Backup kontrolü"

🏷️ KATEGORİLER:
• RKSV: Yasal gereksinimler
• TSE: Teknik güvenlik
• ALLGEMEIN: Genel görevler

⭐ ÖNCELİKLER:
• H (High): Kritik/Acil
• M (Medium): Normal
• L (Low): İsteğe bağlı

💡 İPUÇLARI:
• Checkmark'a tıklayarak tamamla
• Çöp kutusu ile sil
• Swipe ile hızlı işlemler`,
      [{ text: 'OK' }]
    );
  };

  /**
   * RKSV template examples
   */
  const showRksvTemplates = () => {
    Alert.alert(
      'RKSV Todo Templates',
      `📋 GÜNLÜK RKSV KONTROL LİSTESİ:

🌅 SABAH (08:00):
☐ TSE cihaz bağlantısını kontrol et
☐ Dün kalan işlemleri kontrol et
☐ System backup durumunu kontrol et
☐ Compliance log'ları incele

☀️ GÜN İÇİ:
☐ Fatura işlemlerini takip et
☐ Payment gateway durumunu izle
☐ TSE signatur hatalarını kontrol et
☐ Müşteri şikayetlerini kaydet

🌆 AKŞAM (18:00):
☐ Tagesabschluss işlemini yap
☐ Günlük raporları hazırla
☐ Audit trail'i kontrol et
☐ Backup'ları doğrula

⚠️ HAFTALIK:
☐ RKSV compliance score kontrolü
☐ System güvenlik güncellemeleri
☐ TSE cihaz bakımı
☐ Finans audit hazırlığı`,
      [{ text: 'Kopieren' }, { text: 'Schließen' }]
    );
  };

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>SimpleTodo Examples</Text>
        <Text style={styles.subtitle}>React Native Todo List - Usage Examples</Text>
      </View>

      {/* Quick Actions */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Quick Actions</Text>

        <TouchableOpacity style={styles.actionCard} onPress={showQuickTipsAlert}>
          <Ionicons name="lightbulb-outline" size={24} color="#2196F3" />
          <View style={styles.actionContent}>
            <Text style={styles.actionTitle}>Kullanım İpuçları</Text>
            <Text style={styles.actionDescription}>Nasıl kullanılacağını öğrenin</Text>
          </View>
        </TouchableOpacity>

        <TouchableOpacity style={styles.actionCard} onPress={showRksvTemplates}>
          <Ionicons name="document-text-outline" size={24} color="#FF9800" />
          <View style={styles.actionContent}>
            <Text style={styles.actionTitle}>RKSV Templates</Text>
            <Text style={styles.actionDescription}>Hazır görev şablonları</Text>
          </View>
        </TouchableOpacity>
      </View>

      {/* Examples */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Todo Variants</Text>

        {examples.map((example) => (
          <View key={example.id} style={styles.exampleCard}>
            <TouchableOpacity
              style={styles.exampleHeader}
              onPress={() => {
                setActiveExample(activeExample === example.id ? null : example.id);
              }}>
              <View style={styles.exampleInfo}>
                <Text style={styles.exampleTitle}>{example.title}</Text>
                <Text style={styles.exampleDescription}>{example.description}</Text>
              </View>
              <Ionicons
                name={activeExample === example.id ? 'chevron-up' : 'chevron-down'}
                size={20}
                color="#666"
              />
            </TouchableOpacity>

            {activeExample === example.id && (
              <View style={styles.exampleContent}>{example.component}</View>
            )}
          </View>
        ))}
      </View>

      {/* Usage Guide */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Usage Guide</Text>

        <View style={styles.guideCard}>
          <Text style={styles.guideTitle}>🚀 Projenizde Nasıl Kullanılır?</Text>

          <View style={styles.codeBlock}>
            <Text style={styles.codeTitle}>1. Import Component:</Text>
            <Text style={styles.codeText}>
              {`import SimpleTodo from '../components/SimpleTodo';`}
            </Text>
          </View>

          <View style={styles.codeBlock}>
            <Text style={styles.codeTitle}>2. Basic Usage:</Text>
            <Text style={styles.codeText}>
              {`<SimpleTodo
  storageKey="my_todos"
  maxItems={50}
  enableCategories={true}
  enablePriority={true}
/>`}
            </Text>
          </View>

          <View style={styles.codeBlock}>
            <Text style={styles.codeTitle}>3. RKSV Optimized:</Text>
            <Text style={styles.codeText}>
              {`<SimpleTodo
  storageKey="rksv_daily_tasks"
  maxItems={100}
  enableCategories={true}
  enablePriority={true}
/>`}
            </Text>
          </View>
        </View>

        <View style={styles.featureList}>
          <Text style={styles.featureTitle}>✨ Özellikler:</Text>
          <Text style={styles.featureItem}>• ✅ AsyncStorage ile local storage</Text>
          <Text style={styles.featureItem}>• 🏷️ Kategori desteği (RKSV, TSE, Allgemein)</Text>
          <Text style={styles.featureItem}>• ⭐ Öncelik sistemi (High, Medium, Low)</Text>
          <Text style={styles.featureItem}>• 🌍 Çok dilli destek (Almanca UI)</Text>
          <Text style={styles.featureItem}>• 📱 React Native optimize</Text>
          <Text style={styles.featureItem}>• 🎨 Material Design inspired</Text>
          <Text style={styles.featureItem}>• 🔄 Real-time state management</Text>
          <Text style={styles.featureItem}>• 💾 Otomatik kaydetme</Text>
        </View>
      </View>

      {/* Best Practices */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Best Practices</Text>

        <View style={styles.practiceCard}>
          <Text style={styles.practiceTitle}>📋 RKSV İş Akışı İçin:</Text>
          <Text style={styles.practiceText}>
            • Günlük kontrol listeleri oluşturun{'\n'}• TSE işlemlerini kategorize edin{'\n'}•
            Compliance görevleri için HIGH priority kullanın{'\n'}• Tamamlanan görevleri düzenli
            temizleyin
          </Text>
        </View>

        <View style={styles.practiceCard}>
          <Text style={styles.practiceTitle}>⚡ Performance:</Text>
          <Text style={styles.practiceText}>
            • maxItems ile liste boyutunu sınırlayın{'\n'}• Gereksiz kategoriler/priority'yi devre
            dışı bırakın{'\n'}• Benzersiz storageKey kullanın{'\n'}• Tamamlanan görevleri düzenli
            silin
          </Text>
        </View>

        <View style={styles.practiceCard}>
          <Text style={styles.practiceTitle}>🎯 UX/UI:</Text>
          <Text style={styles.practiceText}>
            • Kısa ve net görev tanımları yazın{'\n'}• Kategori renklerini tutarlı kullanın{'\n'}•
            Priority sistemini anlamlı şekilde kullanın{'\n'}• User feedback için Alert'leri aktif
            bırakın
          </Text>
        </View>
      </View>
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
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 15,
  },
  actionCard: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 15,
    backgroundColor: '#f8f9fa',
    borderRadius: 10,
    marginBottom: 10,
  },
  actionContent: {
    flex: 1,
    marginLeft: 15,
  },
  actionTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  actionDescription: {
    fontSize: 14,
    color: '#666',
  },
  exampleCard: {
    borderWidth: 1,
    borderColor: '#e0e0e0',
    borderRadius: 10,
    marginBottom: 10,
    overflow: 'hidden',
  },
  exampleHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 15,
    backgroundColor: '#f8f9fa',
  },
  exampleInfo: {
    flex: 1,
  },
  exampleTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  exampleDescription: {
    fontSize: 14,
    color: '#666',
  },
  exampleContent: {
    height: 400,
    backgroundColor: 'white',
  },
  guideCard: {
    backgroundColor: '#f8f9fa',
    padding: 15,
    borderRadius: 10,
    marginBottom: 15,
  },
  guideTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 15,
  },
  codeBlock: {
    backgroundColor: 'white',
    padding: 10,
    borderRadius: 8,
    marginBottom: 10,
    borderLeftWidth: 4,
    borderLeftColor: '#2196F3',
  },
  codeTitle: {
    fontSize: 12,
    fontWeight: 'bold',
    color: '#2196F3',
    marginBottom: 5,
  },
  codeText: {
    fontSize: 12,
    fontFamily: 'monospace',
    color: '#333',
    backgroundColor: '#f5f5f5',
    padding: 8,
    borderRadius: 4,
  },
  featureList: {
    backgroundColor: '#e8f5e8',
    padding: 15,
    borderRadius: 10,
    marginBottom: 15,
  },
  featureTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#2e7d32',
    marginBottom: 10,
  },
  featureItem: {
    fontSize: 14,
    color: '#2e7d32',
    marginBottom: 5,
    lineHeight: 20,
  },
  practiceCard: {
    backgroundColor: '#fff3e0',
    padding: 15,
    borderRadius: 10,
    marginBottom: 10,
    borderLeftWidth: 4,
    borderLeftColor: '#ff9800',
  },
  practiceTitle: {
    fontSize: 14,
    fontWeight: 'bold',
    color: '#e65100',
    marginBottom: 8,
  },
  practiceText: {
    fontSize: 13,
    color: '#e65100',
    lineHeight: 18,
  },
});

export default SimpleTodoExamples;
