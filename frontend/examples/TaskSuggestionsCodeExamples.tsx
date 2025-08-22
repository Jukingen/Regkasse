/**
 * Task Suggestions Code Examples - Görev önerileri kod örnekleri
 * 
 * Bu dosya, Task-Master AI sisteminden görev önerilerini nasıl alacağınıza
 * dair pratik kod örnekleri içerir.
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
import useTaskMaster from '../hooks/useTaskMaster';
import useEnhancedTaskMaster from '../hooks/useEnhancedTaskMaster';
import { TaskCategory, TaskPriority } from '../services/TaskMasterService';

const TaskSuggestionsCodeExamples: React.FC = () => {
  const { generateTaskSuggestions, createTask } = useTaskMaster();
  const { getAISuggestions, createEnhancedTask } = useEnhancedTaskMaster();

  const [suggestions, setSuggestions] = useState<string[]>([]);

  // ==========================================
  // 1. TEMEL GÖREV ÖNERİLERİ ALMA
  // ==========================================
  
  /**
   * ÖRNEK 1: Basit görev önerileri alma
   */
  const example1_BasicSuggestions = async () => {
    try {
      // RKSV kategorisi için basit öneriler al
      const rksvSuggestions = await generateTaskSuggestions(TaskCategory.RKSV_COMPLIANCE);
      
      console.log('🎯 RKSV Önerileri:', rksvSuggestions);
      // Çıktı: [
      //   'TSE Signatur Kontrolü',
      //   'Belege für Finanz Audit vorbereiten',
      //   'RKSV Compliance Report erstellen',
      //   'Steuernummer Validierung prüfen'
      // ]
      
      setSuggestions(rksvSuggestions);
      Alert.alert('Basit Öneriler', `${rksvSuggestions.length} öneri alındı`);
      
    } catch (error) {
      console.error('Basit öneriler hatası:', error);
    }
  };

  /**
   * ÖRNEK 2: AI destekli gelişmiş öneriler
   */
  const example2_AIEnhancedSuggestions = async () => {
    try {
      // TSE kategorisi için AI önerileri al
      const aiSuggestions = await getAISuggestions(TaskCategory.TSE_INTEGRATION);
      
      console.log('🤖 AI TSE Önerileri:', aiSuggestions);
      // Çıktı: [
      //   'Smart: TSE Health Monitoring System',
      //   'AI-Enhanced: Automatische Backup Scheduling',
      //   'Predictive: TSE Failure Prevention',
      //   'Advanced: Multi-TSE Load Balancing'
      // ]
      
      setSuggestions(aiSuggestions);
      Alert.alert('AI Öneriler', `${aiSuggestions.length} gelişmiş öneri alındı`);
      
    } catch (error) {
      console.error('AI öneriler hatası:', error);
    }
  };

  // ==========================================
  // 2. KATEGORİ BAZLI ÖNERİLER
  // ==========================================

  /**
   * ÖRNEK 3: Tüm kategoriler için öneriler al
   */
  const example3_AllCategorySuggestions = async () => {
    try {
      const allSuggestions: {[key: string]: string[]} = {};
      
      // Tüm kategoriler için döngü
      const categories = [
        TaskCategory.RKSV_COMPLIANCE,
        TaskCategory.TSE_INTEGRATION,
        TaskCategory.INVOICE_MANAGEMENT,
        TaskCategory.PAYMENT_PROCESSING,
        TaskCategory.AUDIT_LOGGING
      ];
      
      for (const category of categories) {
        const categorySuggestions = await generateTaskSuggestions(category);
        allSuggestions[category] = categorySuggestions;
        
        console.log(`📋 ${category}:`, categorySuggestions);
      }
      
      // En çok öneri olan kategoriyi bul
      const mostSuggestions = Object.entries(allSuggestions)
        .sort(([,a], [,b]) => b.length - a.length)[0];
      
      Alert.alert(
        'Tüm Kategoriler', 
        `En çok öneri: ${mostSuggestions[0]} (${mostSuggestions[1].length} öneri)`
      );
      
    } catch (error) {
      console.error('Kategori önerileri hatası:', error);
    }
  };

  /**
   * ÖRNEK 4: Günlük RKSV kontrol listesi oluştur
   */
  const example4_DailyRKSVChecklist = async () => {
    try {
      // RKSV önerileri al
      const rksvSuggestions = await generateTaskSuggestions(TaskCategory.RKSV_COMPLIANCE);
      
      // TSE önerileri al
      const tseSuggestions = await generateTaskSuggestions(TaskCategory.TSE_INTEGRATION);
      
      // Günlük kontrol listesi oluştur
      const dailyChecklist = [
        ...rksvSuggestions.slice(0, 3), // İlk 3 RKSV görevi
        ...tseSuggestions.slice(0, 2),  // İlk 2 TSE görevi
        'System Backup Status kontrol et',
        'Compliance Log Review yap'
      ];
      
      console.log('📅 Günlük RKSV Kontrol Listesi:', dailyChecklist);
      
      // Liste'yi Alert ile göster
      Alert.alert(
        'Günlük RKSV Checklist',
        dailyChecklist.map((item, index) => `${index + 1}. ${item}`).join('\n'),
        [
          { text: 'İptal', style: 'cancel' },
          { 
            text: 'Görev Olarak Oluştur', 
            onPress: () => createDailyTasks(dailyChecklist)
          }
        ]
      );
      
    } catch (error) {
      console.error('Günlük checklist hatası:', error);
    }
  };

  // ==========================================
  // 3. ÖNERİLERDEN GÖREV OLUŞTURMA
  // ==========================================

  /**
   * ÖRNEK 5: Öneriden görev oluştur (Basit)
   */
  const example5_CreateTaskFromSuggestion = async () => {
    try {
      // Önce öneri al
      const suggestions = await generateTaskSuggestions(TaskCategory.RKSV_COMPLIANCE);
      
      if (suggestions.length > 0) {
        const selectedSuggestion = suggestions[0]; // İlk öneriyi seç
        
        // Görev olarak oluştur
        const newTask = await createTask({
          title: selectedSuggestion,
          description: `Bu görev AI önerisi ile oluşturuldu: ${selectedSuggestion}`,
          category: TaskCategory.RKSV_COMPLIANCE,
          priority: TaskPriority.HIGH,
          status: 'pending' as any,
          tags: ['ai-suggestion', 'rksv', 'auto-generated']
        });
        
        console.log('✅ Öneri görev olarak oluşturuldu:', newTask);
        Alert.alert('Başarılı', `"${selectedSuggestion}" görevi oluşturuldu`);
      }
      
    } catch (error) {
      console.error('Görev oluşturma hatası:', error);
    }
  };

  /**
   * ÖRNEK 6: Enhanced görev oluştur
   */
  const example6_CreateEnhancedTaskFromSuggestion = async () => {
    try {
      // AI önerisi al
      const aiSuggestions = await getAISuggestions(TaskCategory.TSE_INTEGRATION);
      
      if (aiSuggestions.length > 0) {
        const selectedSuggestion = aiSuggestions[0];
        
        // Enhanced görev oluştur
        const enhancedTask = await createEnhancedTask({
          title: selectedSuggestion,
          description: `AI Enhanced Task: ${selectedSuggestion}\n\nBu görev gelişmiş AI analizi ile oluşturulmuştur.`,
          category: TaskCategory.TSE_INTEGRATION,
          priority: TaskPriority.CRITICAL,
          status: 'pending' as any,
          tseRequired: true,
          dependencies: [],
          tags: ['ai-enhanced', 'tse', 'critical']
        });
        
        console.log('🚀 Enhanced görev oluşturuldu:', enhancedTask);
        Alert.alert('Enhanced Task', `"${selectedSuggestion}" enhanced görev olarak oluşturuldu`);
      }
      
    } catch (error) {
      console.error('Enhanced görev hatası:', error);
    }
  };

  // ==========================================
  // 4. BATCH İŞLEMLER
  // ==========================================

  /**
   * Günlük görevleri batch olarak oluştur
   */
  const createDailyTasks = async (checklist: string[]) => {
    try {
      const createdTasks = [];
      
      for (const [index, taskTitle] of checklist.entries()) {
        // Kategori belirleme
        let category = TaskCategory.RKSV_COMPLIANCE;
        let priority = TaskPriority.MEDIUM;
        let tseRequired = false;
        
        if (taskTitle.toLowerCase().includes('tse')) {
          category = TaskCategory.TSE_INTEGRATION;
          priority = TaskPriority.HIGH;
          tseRequired = true;
        } else if (taskTitle.toLowerCase().includes('backup')) {
          category = TaskCategory.AUDIT_LOGGING;
          priority = TaskPriority.HIGH;
        }
        
        // Görev oluştur
        const task = await createTask({
          title: `${index + 1}. ${taskTitle}`,
          description: `Günlük kontrol görevi: ${taskTitle}`,
          category,
          priority,
          status: 'pending' as any,
          tseRequired,
          tags: ['daily-checklist', 'auto-generated']
        });
        
        createdTasks.push(task);
      }
      
      console.log('📅 Günlük görevler oluşturuldu:', createdTasks);
      Alert.alert('Başarılı', `${createdTasks.length} günlük görev oluşturuldu`);
      
    } catch (error) {
      console.error('Batch görev oluşturma hatası:', error);
    }
  };

  // ==========================================
  // 5. FİLTRELEME VE ARAMA
  // ==========================================

  /**
   * ÖRNEK 7: Önerileri filtrele ve ara
   */
  const example7_FilterAndSearchSuggestions = async () => {
    try {
      // Birden fazla kategoriden öneri al
      const allSuggestions = await Promise.all([
        generateTaskSuggestions(TaskCategory.RKSV_COMPLIANCE),
        generateTaskSuggestions(TaskCategory.TSE_INTEGRATION),
        generateTaskSuggestions(TaskCategory.PAYMENT_PROCESSING)
      ]);
      
      // Tüm önerileri birleştir
      const flatSuggestions = allSuggestions.flat();
      
      // 'kontrol' kelimesi içeren önerileri filtrele
      const controlSuggestions = flatSuggestions.filter(suggestion =>
        suggestion.toLowerCase().includes('kontrol')
      );
      
      // 'test' kelimesi içeren önerileri filtrele
      const testSuggestions = flatSuggestions.filter(suggestion =>
        suggestion.toLowerCase().includes('test')
      );
      
      console.log('🔍 Kontrol görevleri:', controlSuggestions);
      console.log('🔍 Test görevleri:', testSuggestions);
      
      Alert.alert(
        'Filtrelenmiş Öneriler',
        `Kontrol: ${controlSuggestions.length}\nTest: ${testSuggestions.length}\nToplam: ${flatSuggestions.length}`
      );
      
    } catch (error) {
      console.error('Filtreleme hatası:', error);
    }
  };

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>Task Suggestions - Code Examples</Text>
        <Text style={styles.subtitle}>
          Pratik kod örnekleri ile görev önerilerini nasıl kullanacağınızı öğrenin
        </Text>
      </View>

      {/* Basic Examples */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>🎯 Temel Örnekler</Text>
        
        <TouchableOpacity style={styles.exampleButton} onPress={example1_BasicSuggestions}>
          <Text style={styles.exampleButtonText}>1. Basit Görev Önerileri</Text>
        </TouchableOpacity>

        <TouchableOpacity style={styles.exampleButton} onPress={example2_AIEnhancedSuggestions}>
          <Text style={styles.exampleButtonText}>2. AI Destekli Öneriler</Text>
        </TouchableOpacity>

        <TouchableOpacity style={styles.exampleButton} onPress={example3_AllCategorySuggestions}>
          <Text style={styles.exampleButtonText}>3. Tüm Kategori Önerileri</Text>
        </TouchableOpacity>
      </View>

      {/* Advanced Examples */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>🚀 Gelişmiş Örnekler</Text>
        
        <TouchableOpacity style={styles.exampleButton} onPress={example4_DailyRKSVChecklist}>
          <Text style={styles.exampleButtonText}>4. Günlük RKSV Checklist</Text>
        </TouchableOpacity>

        <TouchableOpacity style={styles.exampleButton} onPress={example5_CreateTaskFromSuggestion}>
          <Text style={styles.exampleButtonText}>5. Öneri → Görev</Text>
        </TouchableOpacity>

        <TouchableOpacity style={styles.exampleButton} onPress={example6_CreateEnhancedTaskFromSuggestion}>
          <Text style={styles.exampleButtonText}>6. Enhanced Görev Oluştur</Text>
        </TouchableOpacity>

        <TouchableOpacity style={styles.exampleButton} onPress={example7_FilterAndSearchSuggestions}>
          <Text style={styles.exampleButtonText}>7. Filtreleme & Arama</Text>
        </TouchableOpacity>
      </View>

      {/* Current Suggestions */}
      {suggestions.length > 0 && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>📋 Son Alınan Öneriler</Text>
          {suggestions.map((suggestion, index) => (
            <View key={index} style={styles.suggestionItem}>
              <Text style={styles.suggestionText}>
                {index + 1}. {suggestion}
              </Text>
            </View>
          ))}
        </View>
      )}

      {/* Code Snippets */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>💻 Kod Snippet'leri</Text>
        
        <View style={styles.codeBlock}>
          <Text style={styles.codeTitle}>Basit Öneri Alma:</Text>
          <Text style={styles.codeText}>
{`const suggestions = await generateTaskSuggestions(
  TaskCategory.RKSV_COMPLIANCE
);
console.log(suggestions);`}
          </Text>
        </View>

        <View style={styles.codeBlock}>
          <Text style={styles.codeTitle}>AI Öneri Alma:</Text>
          <Text style={styles.codeText}>
{`const aiSuggestions = await getAISuggestions(
  TaskCategory.TSE_INTEGRATION
);
console.log(aiSuggestions);`}
          </Text>
        </View>

        <View style={styles.codeBlock}>
          <Text style={styles.codeTitle}>Görev Oluşturma:</Text>
          <Text style={styles.codeText}>
{`const task = await createTask({
  title: suggestions[0],
  category: TaskCategory.RKSV_COMPLIANCE,
  priority: TaskPriority.HIGH,
  status: 'pending'
});`}
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
  exampleButton: {
    backgroundColor: '#2196F3',
    padding: 15,
    borderRadius: 8,
    marginBottom: 10,
    alignItems: 'center',
  },
  exampleButtonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: 'bold',
  },
  suggestionItem: {
    backgroundColor: '#f8f9fa',
    padding: 12,
    borderRadius: 8,
    marginBottom: 5,
    borderLeftWidth: 4,
    borderLeftColor: '#2196F3',
  },
  suggestionText: {
    fontSize: 14,
    color: '#333',
    lineHeight: 18,
  },
  codeBlock: {
    backgroundColor: '#f8f9fa',
    padding: 15,
    borderRadius: 8,
    marginBottom: 10,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  codeTitle: {
    fontSize: 14,
    fontWeight: 'bold',
    color: '#2196F3',
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
});

export default TaskSuggestionsCodeExamples;
