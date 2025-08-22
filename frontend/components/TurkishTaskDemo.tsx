/**
 * TurkishTaskDemo - Türkçe Görev Önerileri Demo
 * 
 * Bu component, Node.js v18 uyumlu Simple TaskMaster servisini kullanarak
 * Türkçe görev önerilerini test eder ve gösterir.
 * 
 * @author Frontend Team
 * @version 1.0.0
 * @since 2025-01-10
 */

import React, { useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  Alert
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { Ionicons } from '@expo/vector-icons';
import useTaskMaster from '../hooks/useTaskMaster';
import useEnhancedTaskMaster from '../hooks/useEnhancedTaskMaster';
import { TaskCategory, TaskPriority, TaskStatus } from '../services/TaskMasterService';
import LanguageSwitcher from './LanguageSwitcher';

const TurkishTaskDemo: React.FC = () => {
  const { i18n } = useTranslation();
  const { generateTaskSuggestions, createTask } = useTaskMaster();
  const { getAISuggestions } = useEnhancedTaskMaster();
  
  const [basicSuggestions, setBasicSuggestions] = useState<string[]>([]);
  const [aiSuggestions, setAiSuggestions] = useState<string[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [selectedCategory, setSelectedCategory] = useState<TaskCategory>(TaskCategory.RKSV_COMPLIANCE);

  /**
   * Türkçe basic önerileri test et
   */
  const testBasicTurkishSuggestions = async () => {
    try {
      setLoading(true);
      
      // Dili Türkçe'ye ayarla
      await i18n.changeLanguage('tr');
      console.log('🇹🇷 Language set to Turkish');
      
      // Basic önerileri al
      const suggestions = await generateTaskSuggestions(selectedCategory);
      setBasicSuggestions(suggestions);
      
      Alert.alert(
        '✅ Türkçe Basic Öneriler',
        `${suggestions.length} adet Türkçe öneri alındı!\n\nKategori: ${selectedCategory}`,
        [{ text: 'Harika!' }]
      );
      
      console.log('🔥 Turkish basic suggestions:', suggestions);
      
    } catch (error) {
      console.error('Turkish basic suggestions failed:', error);
      Alert.alert('Hata', 'Türkçe öneriler alınamadı');
    } finally {
      setLoading(false);
    }
  };

  /**
   * Türkçe AI önerileri test et
   */
  const testAITurkishSuggestions = async () => {
    try {
      setLoading(true);
      
      // Dili Türkçe'ye ayarla
      await i18n.changeLanguage('tr');
      console.log('🇹🇷 Language set to Turkish for AI');
      
      // AI önerileri al
      const suggestions = await getAISuggestions(selectedCategory);
      setAiSuggestions(suggestions);
      
      Alert.alert(
        '🤖 Türkçe AI Öneriler',
        `${suggestions.length} adet AI destekli Türkçe öneri alındı!\n\nKategori: ${selectedCategory}`,
        [{ text: 'Muhteşem!' }]
      );
      
      console.log('🚀 Turkish AI suggestions:', suggestions);
      
    } catch (error) {
      console.error('Turkish AI suggestions failed:', error);
      Alert.alert('Hata', 'AI Türkçe öneriler alınamadı');
    } finally {
      setLoading(false);
    }
  };

  /**
   * Örnek Türkçe görev oluştur
   */
  const createSampleTurkishTask = async () => {
    try {
      if (basicSuggestions.length === 0) {
        Alert.alert('Uyarı', 'Önce Türkçe öneriler alın');
        return;
      }

      setLoading(true);
      
      // İlk öneriyi görev olarak oluştur
      const suggestion = basicSuggestions[0];
      
      const task = await createTask({
        title: suggestion,
        description: `Türkçe demo görev: ${suggestion}\n\nBu görev TaskMasterService kullanılarak oluşturuldu.`,
        category: selectedCategory,
        priority: TaskPriority.HIGH,
        status: TaskStatus.PENDING,
        dependencies: [],
        tseRequired: selectedCategory === TaskCategory.RKSV_COMPLIANCE || selectedCategory === TaskCategory.TSE_INTEGRATION,
        tags: ['türkçe-demo', 'test']
      });
      
      Alert.alert(
        '🎯 Türkçe Görev Oluşturuldu',
        `Başarılı!\n\n"${suggestion}"\n\nGörev başarıyla kaydedildi.`,
        [{ text: 'Harika!' }]
      );
      
      console.log('✅ Turkish task created:', task);
      
    } catch (error) {
      console.error('Turkish task creation failed:', error);
      Alert.alert('Hata', 'Türkçe görev oluşturulamadı');
    } finally {
      setLoading(false);
    }
  };

  /**
   * Kategori seç
   */
  const selectCategory = (category: TaskCategory) => {
    setSelectedCategory(category);
    setBasicSuggestions([]);
    setAiSuggestions([]);
  };

  /**
   * Test sonuçlarını göster
   */
  const showTestResults = () => {
    const totalSuggestions = basicSuggestions.length + aiSuggestions.length;
    
    Alert.alert(
      '📊 Test Sonuçları',
      `Kategori: ${selectedCategory}\n\n📝 Basic Öneriler: ${basicSuggestions.length}\n🤖 AI Öneriler: ${aiSuggestions.length}\n\n🎯 Toplam: ${totalSuggestions} Türkçe öneri`,
      [{ text: 'Anladım' }]
    );
  };

  const categories = [
    { key: TaskCategory.RKSV_COMPLIANCE, label: '🛡️ RKSV Uyumluluk', color: '#e74c3c' },
    { key: TaskCategory.TSE_INTEGRATION, label: '🔧 TSE Entegrasyonu', color: '#f39c12' },
    { key: TaskCategory.INVOICE_MANAGEMENT, label: '📄 Fatura Yönetimi', color: '#3498db' },
    { key: TaskCategory.PAYMENT_PROCESSING, label: '💳 Ödeme İşleme', color: '#27ae60' },
  ];

  return (
    <ScrollView style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.title}>🇹🇷 Türkçe Görev Demo</Text>
        <Text style={styles.subtitle}>
          TaskMaster Service v2.0 - Node.js v18 Uyumlu
        </Text>
      </View>

      {/* Language Switcher */}
      <LanguageSwitcher onLanguageChange={(lang) => {
        console.log(`📱 Language changed to: ${lang}`);
        setBasicSuggestions([]);
        setAiSuggestions([]);
      }} />

      {/* Category Selection */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>📂 Kategori Seçin</Text>
        <Text style={styles.currentCategory}>
          Seçili: {categories.find(c => c.key === selectedCategory)?.label}
        </Text>
        
        <View style={styles.categoryGrid}>
          {categories.map((category) => (
            <TouchableOpacity
              key={category.key}
              style={[
                styles.categoryButton,
                { borderColor: category.color },
                selectedCategory === category.key && { backgroundColor: category.color + '20' }
              ]}
              onPress={() => selectCategory(category.key)}
            >
              <Text style={[
                styles.categoryText,
                selectedCategory === category.key && { color: category.color, fontWeight: 'bold' }
              ]}>
                {category.label}
              </Text>
            </TouchableOpacity>
          ))}
        </View>
      </View>

      {/* Test Actions */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>🚀 Test İşlemleri</Text>
        
        <TouchableOpacity 
          style={[styles.actionButton, { backgroundColor: '#3498db' }]}
          onPress={testBasicTurkishSuggestions}
          disabled={loading}
        >
          <Ionicons name="list-outline" size={20} color="white" />
          <Text style={styles.actionButtonText}>
            📝 Basic Türkçe Öneriler Al
          </Text>
        </TouchableOpacity>

        <TouchableOpacity 
          style={[styles.actionButton, { backgroundColor: '#9b59b6' }]}
          onPress={testAITurkishSuggestions}
          disabled={loading}
        >
          <Ionicons name="bulb-outline" size={20} color="white" />
          <Text style={styles.actionButtonText}>
            🤖 AI Türkçe Öneriler Al
          </Text>
        </TouchableOpacity>

        <TouchableOpacity 
          style={[styles.actionButton, { backgroundColor: '#27ae60' }]}
          onPress={createSampleTurkishTask}
          disabled={loading || basicSuggestions.length === 0}
        >
          <Ionicons name="add-circle-outline" size={20} color="white" />
          <Text style={styles.actionButtonText}>
            ✅ Türkçe Görev Oluştur
          </Text>
        </TouchableOpacity>

        <TouchableOpacity 
          style={[styles.actionButton, { backgroundColor: '#95a5a6' }]}
          onPress={showTestResults}
        >
          <Ionicons name="analytics-outline" size={20} color="white" />
          <Text style={styles.actionButtonText}>
            📊 Sonuçları Göster
          </Text>
        </TouchableOpacity>
      </View>

      {/* Results */}
      {basicSuggestions.length > 0 && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>📝 Basic Türkçe Öneriler</Text>
          {basicSuggestions.map((suggestion, index) => (
            <View key={index} style={styles.suggestionCard}>
              <Text style={styles.suggestionIndex}>{index + 1}.</Text>
              <Text style={styles.suggestionText}>{suggestion}</Text>
            </View>
          ))}
        </View>
      )}

      {aiSuggestions.length > 0 && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>🤖 AI Türkçe Öneriler</Text>
          {aiSuggestions.map((suggestion, index) => (
            <View key={index} style={[styles.suggestionCard, { borderLeftColor: '#9b59b6' }]}>
              <Text style={styles.suggestionIndex}>{index + 1}.</Text>
              <Text style={styles.suggestionText}>{suggestion}</Text>
            </View>
          ))}
        </View>
      )}

      {/* Info */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>💡 Bilgi</Text>
        <View style={styles.infoCard}>
          <Text style={styles.infoText}>
            ✅ <Text style={styles.infoBold}>TaskMaster Service v2.0</Text> kullanılıyor{'\n'}
            ✅ <Text style={styles.infoBold}>Node.js v18</Text> uyumlu{'\n'}
            ✅ <Text style={styles.infoBold}>External dependencies</Text> yok{'\n'}
            ✅ <Text style={styles.infoBold}>3 dil desteği:</Text> TR, DE, EN{'\n'}
            ✅ <Text style={styles.infoBold}>9 kategori</Text> için öneriler{'\n'}
            ✅ <Text style={styles.infoBold}>LocalStorage</Text> ile kayıt
          </Text>
        </View>
      </View>

      {loading && (
        <View style={styles.loadingOverlay}>
          <Text style={styles.loadingText}>🔄 İşlem devam ediyor...</Text>
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
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
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
  currentCategory: {
    fontSize: 14,
    color: '#666',
    marginBottom: 10,
    fontStyle: 'italic',
  },
  categoryGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  categoryButton: {
    flex: 1,
    minWidth: '45%',
    padding: 12,
    borderRadius: 8,
    borderWidth: 2,
    alignItems: 'center',
    backgroundColor: '#f8f9fa',
  },
  categoryText: {
    fontSize: 12,
    color: '#333',
    textAlign: 'center',
    lineHeight: 16,
  },
  actionButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 15,
    borderRadius: 10,
    marginBottom: 10,
  },
  actionButtonText: {
    color: 'white',
    fontSize: 14,
    fontWeight: 'bold',
    marginLeft: 8,
  },
  suggestionCard: {
    flexDirection: 'row',
    backgroundColor: '#f8f9fa',
    padding: 12,
    borderRadius: 8,
    marginBottom: 8,
    borderLeftWidth: 4,
    borderLeftColor: '#3498db',
  },
  suggestionIndex: {
    fontSize: 14,
    fontWeight: 'bold',
    color: '#666',
    marginRight: 10,
    minWidth: 25,
  },
  suggestionText: {
    flex: 1,
    fontSize: 14,
    color: '#333',
    lineHeight: 18,
  },
  infoCard: {
    backgroundColor: '#e8f5e8',
    padding: 15,
    borderRadius: 8,
  },
  infoText: {
    fontSize: 14,
    color: '#2e7d32',
    lineHeight: 20,
  },
  infoBold: {
    fontWeight: 'bold',
  },
  loadingOverlay: {
    backgroundColor: 'rgba(255, 255, 255, 0.9)',
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

export default TurkishTaskDemo;
