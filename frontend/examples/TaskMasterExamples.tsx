/**
 * TaskMaster Usage Examples - Task-Master-AI kullanım örnekleri
 * 
 * Bu dosya, task-master-ai paketinin projedeki kullanım örneklerini gösterir.
 * RKSV kurallarına uygun görev yönetimi için hazırlanmış template'ler içerir.
 * 
 * Özellikler:
 * - RKSV Compliance görevleri
 * - TSE entegrasyon görevleri
 * - AI destekli görev analizi örnekleri
 * - Çok dilli görev şablonları
 * 
 * @author Frontend Team
 * @version 1.0.0
 * @since 2025-01-10
 */

import React, { useState, useEffect } from 'react';
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
import { TaskCategory, TaskPriority, TaskStatus } from '../services/TaskMasterService';

const TaskMasterExamples: React.FC = () => {
  const { t } = useTranslation();
  const {
    createTask,
    tasks,
    loading,
    error,
    analyzeTask,
    generateTaskSuggestions,
    getRksvComplianceTasks,
    getTseRequiredTasks,
    getCriticalTasks
  } = useTaskMaster();

  const [showResults, setShowResults] = useState(false);

  /**
   * Örnek RKSV Compliance görevi oluştur
   */
  const createRksvComplianceTask = async () => {
    const taskData = {
      title: 'RKSV Compliance Prüfung',
      description: 'Tägliche Überprüfung der RKSV-Konformität:\n- TSE-Signatur validieren\n- Belegformat kontrollieren\n- Steuernummer prüfen\n- Audit-Log überprüfen',
      category: TaskCategory.RKSV_COMPLIANCE,
      priority: TaskPriority.HIGH,
      status: TaskStatus.PENDING,
      tseRequired: true,
      tags: ['rksv', 'compliance', 'daily-check', 'audit']
    };

    const result = await createTask(taskData);
    if (result) {
      Alert.alert(
        'Erfolg',
        'RKSV Compliance Aufgabe wurde erstellt',
        [{ text: 'OK' }]
      );
    }
  };

  /**
   * Örnek TSE entegrasyon görevi oluştur
   */
  const createTseIntegrationTask = async () => {
    const taskData = {
      title: 'TSE Gerät Verbindung testen',
      description: 'Epson-TSE Cihazı entegrasyonu:\n- USB bağlantısını kontrol et\n- Signatur üretimini test et\n- Tagesabschluss işlemini kontrol et\n- Backup durumunu kontrol et',
      category: TaskCategory.TSE_INTEGRATION,
      priority: TaskPriority.CRITICAL,
      status: TaskStatus.PENDING,
      tseRequired: true,
      tags: ['tse', 'epson', 'integration', 'test']
    };

    const result = await createTask(taskData);
    if (result) {
      Alert.alert(
        'Erfolg',
        'TSE Integration Aufgabe wurde erstellt',
        [{ text: 'OK' }]
      );
    }
  };

  /**
   * Örnek fatura yönetimi görevi oluştur
   */
  const createInvoiceManagementTask = async () => {
    const taskData = {
      title: 'Rechnungsvorlage aktualisieren',
      description: 'Rechnungsvorlage RKSV-Anforderungen anpassen:\n- Pflichtfelder hinzufügen\n- TSE-Signatur Feld integrieren\n- Steuerberechnung validieren\n- PDF Export testen',
      category: TaskCategory.INVOICE_MANAGEMENT,
      priority: TaskPriority.MEDIUM,
      status: TaskStatus.PENDING,
      tseRequired: false,
      tags: ['invoice', 'template', 'pdf', 'validation']
    };

    const result = await createTask(taskData);
    if (result) {
      Alert.alert(
        'Erfolg',
        'Invoice Management Aufgabe wurde erstellt',
        [{ text: 'OK' }]
      );
    }
  };

  /**
   * Örnek ödeme sistemi görevi oluştur
   */
  const createPaymentProcessingTask = async () => {
    const taskData = {
      title: 'Payment Gateway testen',
      description: 'Ödeme sistemi entegrasyonu test etme:\n- Kartenzahlung workflow\n- Bargeld işlemleri\n- Transaction logs\n- Error handling',
      category: TaskCategory.PAYMENT_PROCESSING,
      priority: TaskPriority.HIGH,
      status: TaskStatus.PENDING,
      tseRequired: true,
      tags: ['payment', 'gateway', 'test', 'transaction']
    };

    const result = await createTask(taskData);
    if (result) {
      Alert.alert(
        'Erfolg',
        'Payment Processing Aufgabe wurde erstellt',
        [{ text: 'OK' }]
      );
    }
  };

  /**
   * AI görev analizi örneği
   */
  const runAiAnalysisExample = async () => {
    const criticalTasks = getCriticalTasks();
    if (criticalTasks.length === 0) {
      Alert.alert(
        'Info',
        'Önce kritik görev oluşturun, sonra AI analizi yapın',
        [{ text: 'OK' }]
      );
      return;
    }

    const taskToAnalyze = criticalTasks[0];
    const analysis = await analyzeTask(taskToAnalyze.id);
    
    if (analysis) {
      Alert.alert(
        'AI Analyse Ergebnis',
        `Aufgabe: ${taskToAnalyze.title}\n\nKomplexität: ${analysis.complexity}\nGeschätzte Dauer: ${analysis.estimatedDuration} min\n\nVorschläge:\n${analysis.suggestions.join('\n- ')}`,
        [{ text: 'OK' }]
      );
    }
  };

  /**
   * Görev önerileri oluştur
   */
  const generateSuggestions = async (category: TaskCategory) => {
    const suggestions = await generateTaskSuggestions(category);
    Alert.alert(
      `${category.replace('_', ' ').toUpperCase()} Vorschläge`,
      suggestions.join('\n• '),
      [{ text: 'OK' }]
    );
  };

  /**
   * Mevcut görev istatistiklerini göster
   */
  const showTaskStatistics = () => {
    const totalTasks = tasks.length;
    const rksvTasks = getRksvComplianceTasks().length;
    const tseTasks = getTseRequiredTasks().length;
    const criticalTasks = getCriticalTasks().length;

    Alert.alert(
      'Aufgaben Statistiken',
      `Gesamt Aufgaben: ${totalTasks}\nRKSV Aufgaben: ${rksvTasks}\nTSE Aufgaben: ${tseTasks}\nKritische Aufgaben: ${criticalTasks}`,
      [{ text: 'OK' }]
    );
  };

  const exampleCategories = [
    {
      title: 'RKSV Compliance',
      description: 'Österreichische Kassensicherheitsverordnung',
      color: '#FF5722',
      action: createRksvComplianceTask
    },
    {
      title: 'TSE Integration',
      description: 'Technische Sicherheitseinrichtung',
      color: '#FF9800',
      action: createTseIntegrationTask
    },
    {
      title: 'Invoice Management',
      description: 'Rechnungsmanagement und PDF Export',
      color: '#2196F3',
      action: createInvoiceManagementTask
    },
    {
      title: 'Payment Processing',
      description: 'Zahlungsabwicklung und Gateway Tests',
      color: '#4CAF50',
      action: createPaymentProcessingTask
    }
  ];

  return (
    <ScrollView style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>TaskMaster AI Examples</Text>
        <Text style={styles.subtitle}>
          RKSV-konforme Aufgabenverwaltung mit KI-Unterstützung
        </Text>
      </View>

      {/* Task Creation Examples */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>Beispiel-Aufgaben erstellen</Text>
        {exampleCategories.map((category, index) => (
          <TouchableOpacity
            key={index}
            style={[styles.exampleCard, { borderLeftColor: category.color }]}
            onPress={category.action}
            disabled={loading}
          >
            <View style={styles.cardContent}>
              <Text style={styles.cardTitle}>{category.title}</Text>
              <Text style={styles.cardDescription}>{category.description}</Text>
            </View>
            <Ionicons name="add-circle-outline" size={24} color={category.color} />
          </TouchableOpacity>
        ))}
      </View>

      {/* AI Features Examples */}
      <View style={styles.section}>
        <Text style={styles.sectionTitle}>KI-Funktionen</Text>
        
        <TouchableOpacity
          style={styles.aiFeatureCard}
          onPress={runAiAnalysisExample}
          disabled={loading}
        >
          <Ionicons name="analytics-outline" size={24} color="#2196F3" />
          <View style={styles.featureContent}>
            <Text style={styles.featureTitle}>AI Aufgaben-Analyse</Text>
            <Text style={styles.featureDescription}>
              Kritische Aufgaben mit KI analysieren
            </Text>
          </View>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.aiFeatureCard}
          onPress={() => generateSuggestions(TaskCategory.RKSV_COMPLIANCE)}
          disabled={loading}
        >
          <Ionicons name="bulb-outline" size={24} color="#FF9800" />
          <View style={styles.featureContent}>
            <Text style={styles.featureTitle}>RKSV Vorschläge</Text>
            <Text style={styles.featureDescription}>
              KI-generierte RKSV Aufgaben-Vorschläge
            </Text>
          </View>
        </TouchableOpacity>

        <TouchableOpacity
          style={styles.aiFeatureCard}
          onPress={showTaskStatistics}
          disabled={loading}
        >
          <Ionicons name="bar-chart-outline" size={24} color="#4CAF50" />
          <View style={styles.featureContent}>
            <Text style={styles.featureTitle}>Statistiken anzeigen</Text>
            <Text style={styles.featureDescription}>
              Aktuelle Aufgaben-Statistiken
            </Text>
          </View>
        </TouchableOpacity>
      </View>

      {/* Status Display */}
      {error && (
        <View style={styles.errorCard}>
          <Ionicons name="alert-circle-outline" size={20} color="#F44336" />
          <Text style={styles.errorText}>{error}</Text>
        </View>
      )}

      <View style={styles.statusCard}>
        <Text style={styles.statusTitle}>System Status</Text>
        <Text style={styles.statusText}>
          Aufgaben geladen: {tasks.length}
        </Text>
        <Text style={styles.statusText}>
          RKSV Aufgaben: {getRksvComplianceTasks().length}
        </Text>
        <Text style={styles.statusText}>
          TSE Aufgaben: {getTseRequiredTasks().length}
        </Text>
        <Text style={styles.statusText}>
          Kritische Aufgaben: {getCriticalTasks().length}
        </Text>
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
    padding: 20,
    backgroundColor: 'white',
    marginBottom: 10,
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
  },
  sectionTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 15,
  },
  exampleCard: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: 15,
    marginBottom: 10,
    backgroundColor: '#f9f9f9',
    borderRadius: 8,
    borderLeftWidth: 4,
  },
  cardContent: {
    flex: 1,
  },
  cardTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  cardDescription: {
    fontSize: 14,
    color: '#666',
    lineHeight: 18,
  },
  aiFeatureCard: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 15,
    marginBottom: 10,
    backgroundColor: '#f0f7ff',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#e3f2fd',
  },
  featureContent: {
    flex: 1,
    marginLeft: 12,
  },
  featureTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  featureDescription: {
    fontSize: 14,
    color: '#666',
    lineHeight: 18,
  },
  errorCard: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 15,
    margin: 10,
    backgroundColor: '#ffebee',
    borderRadius: 8,
    borderWidth: 1,
    borderColor: '#ffcdd2',
  },
  errorText: {
    flex: 1,
    marginLeft: 10,
    fontSize: 14,
    color: '#F44336',
  },
  statusCard: {
    backgroundColor: 'white',
    margin: 10,
    padding: 15,
    borderRadius: 10,
    marginBottom: 30,
  },
  statusTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 10,
  },
  statusText: {
    fontSize: 14,
    color: '#666',
    marginBottom: 5,
  },
});

export default TaskMasterExamples;
