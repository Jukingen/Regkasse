/**
 * TaskSuggestionsDemo - Görev önerilerini test etmek için demo component
 * 
 * Bu component, Task-Master AI sisteminden görev önerilerini nasıl alacağınızı
 * ve farklı kategoriler için örnekleri gösterir.
 * 
 * Özellikler:
 * - AI destekli görev önerileri
 * - Kategori bazlı öneriler
 * - RKSV uyumlu template'ler
 * - Interactive demo
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
  Alert,
  ActivityIndicator
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { Ionicons } from '@expo/vector-icons';
import useTaskMaster from '../hooks/useTaskMaster';
import useEnhancedTaskMaster from '../hooks/useEnhancedTaskMaster';
import { TaskCategory, TaskPriority } from '../services/TaskMasterService';
import LanguageSwitcher from './LanguageSwitcher';
import TurkishTaskDemo from './TurkishTaskDemo';

const TaskSuggestionsDemo: React.FC = () => {
  const { t } = useTranslation();
  const { generateTaskSuggestions } = useTaskMaster();
  const { getAISuggestions } = useEnhancedTaskMaster();
  
  const [loading, setLoading] = useState<boolean>(false);
  const [suggestions, setSuggestions] = useState<{[key: string]: string[]}>({});
  const [selectedCategory, setSelectedCategory] = useState<TaskCategory | null>(null);

  /**
   * Kategori tanımları
   */
  const categories = [
    {
      key: TaskCategory.RKSV_COMPLIANCE,
      name: 'RKSV Compliance',
      icon: 'shield-checkmark-outline',
      color: '#FF5722',
      description: 'Österreichische Kassensicherheitsverordnung'
    },
    {
      key: TaskCategory.TSE_INTEGRATION,
      name: 'TSE Integration',
      icon: 'hardware-chip-outline', 
      color: '#FF9800',
      description: 'Technische Sicherheitseinrichtung'
    },
    {
      key: TaskCategory.INVOICE_MANAGEMENT,
      name: 'Invoice Management',
      icon: 'document-text-outline',
      color: '#2196F3',
      description: 'Rechnungsmanagement und PDF Export'
    },
    {
      key: TaskCategory.PAYMENT_PROCESSING,
      name: 'Payment Processing',
      icon: 'card-outline',
      color: '#4CAF50',
      description: 'Zahlungsabwicklung und Gateway'
    },
    {
      key: TaskCategory.AUDIT_LOGGING,
      name: 'Audit Logging',
      icon: 'list-outline',
      color: '#9C27B0',
      description: 'Prüfprotokolle und Compliance'
    },
    {
      key: TaskCategory.DATA_PROTECTION,
      name: 'Data Protection',
      icon: 'lock-closed-outline',
      color: '#F44336',
      description: 'DSGVO und Datenschutz'
    },
    {
      key: TaskCategory.DEVELOPMENT,
      name: 'Development',
      icon: 'code-outline',
      color: '#00BCD4',
      description: 'Software Entwicklung'
    },
    {
      key: TaskCategory.BUG_FIX,
      name: 'Bug Fix',
      icon: 'bug-outline',
      color: '#FFC107',
      description: 'Fehlerbehebung und Patches'
    },
    {
      key: TaskCategory.TESTING,
      name: 'Testing',
      icon: 'checkmark-circle-outline',
      color: '#795548',
      description: 'Tests und Qualitätskontrolle'
    }
  ];

  /**
   * Görev önerilerini al (Basit sistem)
   */
  const getBasicSuggestions = async (category: TaskCategory) => {
    try {
      setLoading(true);
      setSelectedCategory(category);
      
      const basicSuggestions = await generateTaskSuggestions(category);
      
      setSuggestions(prev => ({
        ...prev,
        [`basic_${category}`]: basicSuggestions
      }));
      
    } catch (error) {
      console.error('Basic suggestions failed:', error);
      Alert.alert('Fehler', 'Grundlegende Vorschläge konnten nicht geladen werden');
    } finally {
      setLoading(false);
    }
  };

  /**
   * AI destekli öneriler al (Enhanced sistem)
   */
  const getEnhancedSuggestions = async (category: TaskCategory) => {
    try {
      setLoading(true);
      setSelectedCategory(category);
      
      const aiSuggestions = await getAISuggestions(category);
      
      setSuggestions(prev => ({
        ...prev,
        [`enhanced_${category}`]: aiSuggestions
      }));
      
    } catch (error) {
      console.error('Enhanced suggestions failed:', error);
      Alert.alert('Fehler', 'AI-Vorschläge konnten nicht geladen werden');
    } finally {
      setLoading(false);
    }
  };

  /**
   * Önceden tanımlanmış örnekler göster
   */
  const showPredefinedExamples = () => {
    const examples = {
      [TaskCategory.RKSV_COMPLIANCE]: [
        'TSE Signatur Kontrolü durchführen',
        'Belege für Finanz Audit vorbereiten', 
        'RKSV Compliance Report erstellen',
        'Steuernummer Validierung prüfen',
        'Tagesabschluss dokumentieren'
      ],
      [TaskCategory.TSE_INTEGRATION]: [
        'TSE Gerät Verbindung testen',
        'Epson-TSE Konfiguration prüfen',
        'TSE Backup erstellen',
        'Signatur-Generator testen',
        'Hardware-Status überwachen'
      ],
      [TaskCategory.INVOICE_MANAGEMENT]: [
        'Rechnungsvorlage aktualisieren',
        'PDF Export Funktionalität testen',
        'Mehrwertsteuer Berechnung validieren',
        'Kundendaten Template anpassen',
        'Rechnungsnummern-Format überprüfen'
      ],
      [TaskCategory.PAYMENT_PROCESSING]: [
        'Kartenzahlung-Integration testen',
        'Payment Gateway Performance messen',
        'Transaktions-Logs analysieren',
        'Bargeld-Workflow optimieren',
        'Fehlgeschlagene Zahlungen untersuchen'
      ]
    };

    // Örnekleri suggestions state'ine ekle
    const newSuggestions: {[key: string]: string[]} = {};
    Object.entries(examples).forEach(([category, items]) => {
      newSuggestions[`example_${category}`] = items;
    });
    
    setSuggestions(prev => ({
      ...prev,
      ...newSuggestions
    }));

    Alert.alert(
      'Beispiele geladen',
      'Vordefinierte Aufgabenvorschläge wurden geladen',
      [{ text: 'OK' }]
    );
  };

  /**
   * Tüm önerileri temizle
   */
  const clearAllSuggestions = () => {
    setSuggestions({});
    setSelectedCategory(null);
  };

  /**
   * Öneriyi görev olarak oluştur
   */
  const createTaskFromSuggestion = (suggestion: string, category: TaskCategory) => {
    Alert.alert(
      'Aufgabe erstellen',
      `Möchten Sie folgende Aufgabe erstellen?\n\n"${suggestion}"`,
      [
        { text: 'Abbrechen', style: 'cancel' },
        { 
          text: 'Erstellen', 
          onPress: () => {
            // Burada gerçek task oluşturma işlemi yapılabilir
            console.log(`Creating task: ${suggestion} [${category}]`);
            Alert.alert('Erfolg', 'Aufgabe wurde erstellt');
          }
        }
      ]
    );
  };

  return (
    <ScrollView style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.title}>Task Suggestions Demo</Text>
        <Text style={styles.subtitle}>
          AI-gestützte Aufgabenvorschläge für Ihr RKSV-System
        </Text>
      </View>

      {/* Turkish Demo Component */}
      <TurkishTaskDemo />

      {/* Quick Actions */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Quick Actions</Text>
        
        <View style={styles.actionRow}>
          <TouchableOpacity 
            style={[styles.actionButton, { backgroundColor: '#2196F3' }]}
            onPress={showPredefinedExamples}
          >
            <Ionicons name="bulb-outline" size={20} color="white" />
            <Text style={styles.actionButtonText}>Beispiele laden</Text>
          </TouchableOpacity>

          <TouchableOpacity 
            style={[styles.actionButton, { backgroundColor: '#F44336' }]}
            onPress={clearAllSuggestions}
          >
            <Ionicons name="trash-outline" size={20} color="white" />
            <Text style={styles.actionButtonText}>Alles löschen</Text>
          </TouchableOpacity>
        </View>
      </View>

      {/* Categories Grid */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Kategorien wählen</Text>
        
        <View style={styles.categoriesGrid}>
          {categories.map(category => (
            <View key={category.key} style={styles.categoryCard}>
              <View style={styles.categoryHeader}>
                <View style={[
                  styles.categoryIcon,
                  { backgroundColor: category.color }
                ]}>
                  <Ionicons 
                    name={category.icon as any} 
                    size={24} 
                    color="white" 
                  />
                </View>
                <View style={styles.categoryInfo}>
                  <Text style={styles.categoryName}>{category.name}</Text>
                  <Text style={styles.categoryDescription}>
                    {category.description}
                  </Text>
                </View>
              </View>

              <View style={styles.categoryActions}>
                <TouchableOpacity
                  style={[styles.suggestionButton, { borderColor: category.color }]}
                  onPress={() => getBasicSuggestions(category.key)}
                  disabled={loading}
                >
                  <Text style={[styles.suggestionButtonText, { color: category.color }]}>
                    Basic
                  </Text>
                </TouchableOpacity>

                <TouchableOpacity
                  style={[styles.suggestionButton, { backgroundColor: category.color }]}
                  onPress={() => getEnhancedSuggestions(category.key)}
                  disabled={loading}
                >
                  <Text style={[styles.suggestionButtonText, { color: 'white' }]}>
                    AI Enhanced
                  </Text>
                </TouchableOpacity>
              </View>
            </View>
          ))}
        </View>
      </View>

      {/* Loading Indicator */}
      {loading && (
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color="#2196F3" />
          <Text style={styles.loadingText}>
            {selectedCategory ? 
              `${selectedCategory.replace('_', ' ')} Vorschläge werden geladen...` :
              'Vorschläge werden geladen...'
            }
          </Text>
        </View>
      )}

      {/* Suggestions Results */}
      {Object.keys(suggestions).length > 0 && (
        <View style={styles.section}>
          <Text style={styles.sectionTitle}>Aufgabenvorschläge</Text>
          
          {Object.entries(suggestions).map(([key, items]) => {
            const [type, category] = key.split('_');
            const categoryInfo = categories.find(c => c.key === category);
            
            if (!categoryInfo || items.length === 0) return null;

            return (
              <View key={key} style={styles.suggestionGroup}>
                <View style={styles.suggestionHeader}>
                  <View style={[
                    styles.suggestionIcon,
                    { backgroundColor: categoryInfo.color }
                  ]}>
                    <Ionicons 
                      name={categoryInfo.icon as any} 
                      size={16} 
                      color="white" 
                    />
                  </View>
                  <Text style={styles.suggestionTitle}>
                    {categoryInfo.name} ({type.toUpperCase()})
                  </Text>
                  <Text style={styles.suggestionCount}>
                    {items.length} Vorschläge
                  </Text>
                </View>

                {items.map((suggestion, index) => (
                  <TouchableOpacity
                    key={index}
                    style={styles.suggestionItem}
                    onPress={() => createTaskFromSuggestion(suggestion, categoryInfo.key)}
                  >
                    <Text style={styles.suggestionText}>
                      {index + 1}. {suggestion}
                    </Text>
                    <Ionicons 
                      name="add-circle-outline" 
                      size={20} 
                      color={categoryInfo.color} 
                    />
                  </TouchableOpacity>
                ))}
              </View>
            );
          })}
        </View>
      )}

      {/* Usage Guide */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Verwendungsanleitung</Text>
        
        <View style={styles.guideCard}>
          <Text style={styles.guideTitle}>🎯 Wie verwenden Sie Aufgabenvorschläge?</Text>
          
          <Text style={styles.guideStep}>
            1️⃣ <Text style={styles.guideBold}>Kategorie wählen:</Text> Klicken Sie auf eine der 9 Kategorien oben
          </Text>
          
          <Text style={styles.guideStep}>
            2️⃣ <Text style={styles.guideBold}>Vorschlag-Typ wählen:</Text>{'\n'}
            • <Text style={styles.guideHighlight}>Basic:</Text> Vordefinierte Templates{'\n'}
            • <Text style={styles.guideHighlight}>AI Enhanced:</Text> KI-generierte Vorschläge
          </Text>
          
          <Text style={styles.guideStep}>
            3️⃣ <Text style={styles.guideBold}>Vorschlag auswählen:</Text> Tippen Sie auf einen Vorschlag um eine Aufgabe zu erstellen
          </Text>

          <Text style={styles.guideStep}>
            4️⃣ <Text style={styles.guideBold}>Anpassen:</Text> Passen Sie die Aufgabe nach Ihren Bedürfnissen an
          </Text>
        </View>

        <View style={styles.tipCard}>
          <Ionicons name="lightbulb-outline" size={20} color="#FF9800" />
          <Text style={styles.tipText}>
            <Text style={styles.tipBold}>Pro-Tipp:</Text> Beginnen Sie mit "Beispiele laden" um vordefinierte RKSV-Templates zu sehen!
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
  actionRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  actionButton: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 12,
    borderRadius: 8,
    marginHorizontal: 5,
  },
  actionButtonText: {
    color: 'white',
    fontWeight: 'bold',
    marginLeft: 8,
  },
  categoriesGrid: {
    gap: 15,
  },
  categoryCard: {
    backgroundColor: '#f8f9fa',
    padding: 15,
    borderRadius: 10,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  categoryHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 10,
  },
  categoryIcon: {
    width: 40,
    height: 40,
    borderRadius: 20,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
  },
  categoryInfo: {
    flex: 1,
  },
  categoryName: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 2,
  },
  categoryDescription: {
    fontSize: 12,
    color: '#666',
    lineHeight: 16,
  },
  categoryActions: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  suggestionButton: {
    flex: 1,
    padding: 8,
    borderRadius: 6,
    borderWidth: 1,
    alignItems: 'center',
    marginHorizontal: 2,
  },
  suggestionButtonText: {
    fontSize: 12,
    fontWeight: 'bold',
  },
  loadingContainer: {
    alignItems: 'center',
    padding: 20,
    backgroundColor: 'white',
    margin: 10,
    borderRadius: 10,
  },
  loadingText: {
    marginTop: 10,
    fontSize: 14,
    color: '#666',
    textAlign: 'center',
  },
  suggestionGroup: {
    backgroundColor: '#f8f9fa',
    borderRadius: 10,
    padding: 15,
    marginBottom: 15,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  suggestionHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 10,
  },
  suggestionIcon: {
    width: 24,
    height: 24,
    borderRadius: 12,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 8,
  },
  suggestionTitle: {
    flex: 1,
    fontSize: 14,
    fontWeight: 'bold',
    color: '#333',
  },
  suggestionCount: {
    fontSize: 12,
    color: '#666',
    backgroundColor: 'white',
    paddingHorizontal: 8,
    paddingVertical: 2,
    borderRadius: 10,
  },
  suggestionItem: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: 'white',
    padding: 12,
    borderRadius: 8,
    marginBottom: 5,
  },
  suggestionText: {
    flex: 1,
    fontSize: 14,
    color: '#333',
    lineHeight: 18,
  },
  guideCard: {
    backgroundColor: '#e3f2fd',
    padding: 15,
    borderRadius: 10,
    marginBottom: 10,
  },
  guideTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#1976d2',
    marginBottom: 10,
  },
  guideStep: {
    fontSize: 14,
    color: '#1976d2',
    lineHeight: 20,
    marginBottom: 8,
  },
  guideBold: {
    fontWeight: 'bold',
  },
  guideHighlight: {
    fontWeight: 'bold',
    backgroundColor: 'rgba(255,255,255,0.7)',
    paddingHorizontal: 4,
    borderRadius: 4,
  },
  tipCard: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: '#fff3e0',
    padding: 12,
    borderRadius: 8,
    borderLeftWidth: 4,
    borderLeftColor: '#FF9800',
  },
  tipText: {
    flex: 1,
    marginLeft: 10,
    fontSize: 13,
    color: '#e65100',
    lineHeight: 18,
  },
  tipBold: {
    fontWeight: 'bold',
  },
});

export default TaskSuggestionsDemo;
