/**
 * TaskMasterDashboard - AI destekli görev yönetimi dashboard'u
 * 
 * Bu component, task-master-ai entegrasyonu ile gelişmiş görev yönetimi sağlar.
 * RKSV kurallarına uygun olarak tasarlanmış, Almanca UI ile çok dilli destek sunar.
 * 
 * Özellikler:
 * - Görev listesi ve kategorileri
 * - AI destekli görev analizi
 * - RKSV compliance tracking
 * - Real-time görev güncellemeleri
 * - Filtreleme ve arama
 * - Mobil responsive tasarım
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
  ScrollView,
  TouchableOpacity,
  Alert,
  RefreshControl,
  Modal,
  TextInput,
  Picker
} from 'react-native';
import { useTranslation } from 'react-i18next';
import { Ionicons } from '@expo/vector-icons';
import useTaskMaster from '../hooks/useTaskMaster';
import { Task, TaskCategory, TaskPriority, TaskStatus } from '../services/TaskMasterService';

interface TaskMasterDashboardProps {
  visible: boolean;
  onClose: () => void;
}

const TaskMasterDashboard: React.FC<TaskMasterDashboardProps> = ({ 
  visible, 
  onClose 
}) => {
  const { t } = useTranslation();
  
  // Hook'ları kullan
  const {
    tasks,
    loading,
    error,
    createTask,
    updateTask,
    deleteTask,
    refreshTasks,
    filterTasks,
    searchTasks,
    analyzeTask,
    generateTaskSuggestions,
    getRksvComplianceTasks,
    getTseRequiredTasks,
    getCriticalTasks,
    isReady
  } = useTaskMaster();

  // State tanımlamaları
  const [selectedCategory, setSelectedCategory] = useState<TaskCategory | 'all'>('all');
  const [selectedPriority, setSelectedPriority] = useState<TaskPriority | 'all'>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');
  const [showCreateModal, setShowCreateModal] = useState<boolean>(false);
  const [selectedTask, setSelectedTask] = useState<Task | null>(null);
  const [showTaskDetails, setShowTaskDetails] = useState<boolean>(false);

  // Yeni görev formu state'leri
  const [newTaskTitle, setNewTaskTitle] = useState<string>('');
  const [newTaskDescription, setNewTaskDescription] = useState<string>('');
  const [newTaskCategory, setNewTaskCategory] = useState<TaskCategory>(TaskCategory.DEVELOPMENT);
  const [newTaskPriority, setNewTaskPriority] = useState<TaskPriority>(TaskPriority.MEDIUM);
  const [newTaskTseRequired, setNewTaskTseRequired] = useState<boolean>(false);

  /**
   * Filtrelenmiş görev listesini hesapla
   */
  const filteredTasks = React.useMemo(() => {
    let filtered = tasks;

    // Arama sorgusu varsa uygula
    if (searchQuery.trim()) {
      filtered = searchTasks(searchQuery);
    }

    // Kategori filtresi
    if (selectedCategory !== 'all') {
      filtered = filtered.filter(task => task.category === selectedCategory);
    }

    // Öncelik filtresi
    if (selectedPriority !== 'all') {
      filtered = filtered.filter(task => task.priority === selectedPriority);
    }

    return filtered;
  }, [tasks, searchQuery, selectedCategory, selectedPriority, searchTasks]);

  /**
   * Yeni görev oluştur
   */
  const handleCreateTask = async () => {
    if (!newTaskTitle.trim()) {
      Alert.alert(
        t('error.title', 'Fehler'),
        t('error.task_title_required', 'Aufgabentitel ist erforderlich'),
        [{ text: t('common.ok', 'OK') }]
      );
      return;
    }

    const taskData = {
      title: newTaskTitle,
      description: newTaskDescription,
      category: newTaskCategory,
      priority: newTaskPriority,
      status: TaskStatus.PENDING,
      tseRequired: newTaskTseRequired,
      tags: []
    };

    const success = await createTask(taskData);
    
    if (success) {
      // Form'u temizle ve modal'ı kapat
      setNewTaskTitle('');
      setNewTaskDescription('');
      setNewTaskCategory(TaskCategory.DEVELOPMENT);
      setNewTaskPriority(TaskPriority.MEDIUM);
      setNewTaskTseRequired(false);
      setShowCreateModal(false);
    }
  };

  /**
   * Görev durumunu güncelle
   */
  const handleUpdateTaskStatus = async (taskId: string, newStatus: TaskStatus) => {
    await updateTask(taskId, { status: newStatus });
  };

  /**
   * Görev detaylarını göster
   */
  const handleShowTaskDetails = (task: Task) => {
    setSelectedTask(task);
    setShowTaskDetails(true);
  };

  /**
   * AI analizi başlat
   */
  const handleAnalyzeTask = async (taskId: string) => {
    try {
      const analysis = await analyzeTask(taskId);
      if (analysis) {
        Alert.alert(
          t('ai.analysis_title', 'AI Analyse'),
          `${t('ai.complexity', 'Komplexität')}: ${analysis.complexity}\n${t('ai.duration', 'Geschätzte Dauer')}: ${analysis.estimatedDuration} min\n${t('ai.suggestions', 'Vorschläge')}: ${analysis.suggestions.join(', ')}`,
          [{ text: t('common.ok', 'OK') }]
        );
      }
    } catch (error) {
      console.error('AI analysis failed:', error);
    }
  };

  /**
   * Kategori rengi al
   */
  const getCategoryColor = (category: TaskCategory): string => {
    const colors = {
      [TaskCategory.RKSV_COMPLIANCE]: '#FF5722',
      [TaskCategory.TSE_INTEGRATION]: '#FF9800',
      [TaskCategory.INVOICE_MANAGEMENT]: '#2196F3',
      [TaskCategory.PAYMENT_PROCESSING]: '#4CAF50',
      [TaskCategory.AUDIT_LOGGING]: '#9C27B0',
      [TaskCategory.DATA_PROTECTION]: '#F44336',
      [TaskCategory.DEVELOPMENT]: '#00BCD4',
      [TaskCategory.BUG_FIX]: '#FFC107',
      [TaskCategory.TESTING]: '#795548'
    };
    return colors[category] || '#607D8B';
  };

  /**
   * Öncelik ikonu al
   */
  const getPriorityIcon = (priority: TaskPriority): string => {
    const icons = {
      [TaskPriority.CRITICAL]: 'warning',
      [TaskPriority.HIGH]: 'chevron-up',
      [TaskPriority.MEDIUM]: 'remove',
      [TaskPriority.LOW]: 'chevron-down'
    };
    return icons[priority] || 'remove';
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      presentationStyle="formSheet"
      onRequestClose={onClose}
    >
      <View style={styles.container}>
        {/* Header */}
        <View style={styles.header}>
          <Text style={styles.headerTitle}>
            {t('taskmaster.title', 'Task Master AI')}
          </Text>
          <TouchableOpacity onPress={onClose} style={styles.closeButton}>
            <Ionicons name="close" size={24} color="#333" />
          </TouchableOpacity>
        </View>

        {/* Stats Cards */}
        <View style={styles.statsContainer}>
          <View style={styles.statCard}>
            <Text style={styles.statNumber}>{tasks.length}</Text>
            <Text style={styles.statLabel}>
              {t('taskmaster.total_tasks', 'Gesamt Aufgaben')}
            </Text>
          </View>
          <View style={styles.statCard}>
            <Text style={[styles.statNumber, { color: '#FF5722' }]}>
              {getCriticalTasks().length}
            </Text>
            <Text style={styles.statLabel}>
              {t('taskmaster.critical_tasks', 'Kritische')}
            </Text>
          </View>
          <View style={styles.statCard}>
            <Text style={[styles.statNumber, { color: '#FF9800' }]}>
              {getRksvComplianceTasks().length}
            </Text>
            <Text style={styles.statLabel}>
              {t('taskmaster.rksv_tasks', 'RKSV')}
            </Text>
          </View>
          <View style={styles.statCard}>
            <Text style={[styles.statNumber, { color: '#4CAF50' }]}>
              {getTseRequiredTasks().length}
            </Text>
            <Text style={styles.statLabel}>
              {t('taskmaster.tse_tasks', 'TSE')}
            </Text>
          </View>
        </View>

        {/* Search and Filters */}
        <View style={styles.filterContainer}>
          <TextInput
            style={styles.searchInput}
            placeholder={t('taskmaster.search_placeholder', 'Aufgaben suchen...')}
            value={searchQuery}
            onChangeText={setSearchQuery}
          />
          
          <View style={styles.filterRow}>
            <View style={styles.filterGroup}>
              <Text style={styles.filterLabel}>
                {t('taskmaster.category', 'Kategorie')}
              </Text>
              <Picker
                selectedValue={selectedCategory}
                style={styles.picker}
                onValueChange={setSelectedCategory}
              >
                <Picker.Item label={t('common.all', 'Alle')} value="all" />
                <Picker.Item label="RKSV" value={TaskCategory.RKSV_COMPLIANCE} />
                <Picker.Item label="TSE" value={TaskCategory.TSE_INTEGRATION} />
                <Picker.Item label={t('taskmaster.development', 'Entwicklung')} value={TaskCategory.DEVELOPMENT} />
                <Picker.Item label={t('taskmaster.bug_fix', 'Bug Fix')} value={TaskCategory.BUG_FIX} />
                <Picker.Item label={t('taskmaster.testing', 'Testing')} value={TaskCategory.TESTING} />
              </Picker>
            </View>

            <View style={styles.filterGroup}>
              <Text style={styles.filterLabel}>
                {t('taskmaster.priority', 'Priorität')}
              </Text>
              <Picker
                selectedValue={selectedPriority}
                style={styles.picker}
                onValueChange={setSelectedPriority}
              >
                <Picker.Item label={t('common.all', 'Alle')} value="all" />
                <Picker.Item label={t('taskmaster.critical', 'Kritisch')} value={TaskPriority.CRITICAL} />
                <Picker.Item label={t('taskmaster.high', 'Hoch')} value={TaskPriority.HIGH} />
                <Picker.Item label={t('taskmaster.medium', 'Mittel')} value={TaskPriority.MEDIUM} />
                <Picker.Item label={t('taskmaster.low', 'Niedrig')} value={TaskPriority.LOW} />
              </Picker>
            </View>
          </View>
        </View>

        {/* Task List */}
        <ScrollView
          style={styles.taskList}
          refreshControl={
            <RefreshControl refreshing={loading} onRefresh={refreshTasks} />
          }
        >
          {filteredTasks.map((task) => (
            <TouchableOpacity
              key={task.id}
              style={styles.taskCard}
              onPress={() => handleShowTaskDetails(task)}
            >
              <View style={styles.taskHeader}>
                <View style={[
                  styles.categoryBadge,
                  { backgroundColor: getCategoryColor(task.category) }
                ]}>
                  <Text style={styles.categoryText}>
                    {task.category.replace('_', ' ').toUpperCase()}
                  </Text>
                </View>
                <Ionicons
                  name={getPriorityIcon(task.priority)}
                  size={20}
                  color={getCategoryColor(task.category)}
                />
              </View>
              
              <Text style={styles.taskTitle}>{task.title}</Text>
              <Text style={styles.taskDescription} numberOfLines={2}>
                {task.description}
              </Text>
              
              <View style={styles.taskFooter}>
                <View style={styles.taskStatus}>
                  <Text style={[
                    styles.statusText,
                    { color: task.status === TaskStatus.COMPLETED ? '#4CAF50' : '#FF9800' }
                  ]}>
                    {task.status.replace('_', ' ').toUpperCase()}
                  </Text>
                </View>
                
                {task.tseRequired && (
                  <View style={styles.tseBadge}>
                    <Text style={styles.tseText}>TSE</Text>
                  </View>
                )}
                
                <TouchableOpacity
                  style={styles.analyzeButton}
                  onPress={() => handleAnalyzeTask(task.id)}
                >
                  <Ionicons name="analytics" size={16} color="#2196F3" />
                </TouchableOpacity>
              </View>
            </TouchableOpacity>
          ))}
        </ScrollView>

        {/* Create Task Button */}
        <TouchableOpacity
          style={styles.createButton}
          onPress={() => setShowCreateModal(true)}
        >
          <Ionicons name="add" size={24} color="white" />
        </TouchableOpacity>

        {/* Create Task Modal */}
        <Modal
          visible={showCreateModal}
          animationType="slide"
          presentationStyle="formSheet"
          onRequestClose={() => setShowCreateModal(false)}
        >
          <View style={styles.modalContainer}>
            <View style={styles.modalHeader}>
              <Text style={styles.modalTitle}>
                {t('taskmaster.create_task', 'Neue Aufgabe erstellen')}
              </Text>
              <TouchableOpacity onPress={() => setShowCreateModal(false)}>
                <Ionicons name="close" size={24} color="#333" />
              </TouchableOpacity>
            </View>

            <ScrollView style={styles.modalContent}>
              <View style={styles.formGroup}>
                <Text style={styles.formLabel}>
                  {t('taskmaster.task_title', 'Aufgabentitel')} *
                </Text>
                <TextInput
                  style={styles.formInput}
                  value={newTaskTitle}
                  onChangeText={setNewTaskTitle}
                  placeholder={t('taskmaster.enter_title', 'Titel eingeben...')}
                />
              </View>

              <View style={styles.formGroup}>
                <Text style={styles.formLabel}>
                  {t('taskmaster.description', 'Beschreibung')}
                </Text>
                <TextInput
                  style={[styles.formInput, styles.textArea]}
                  value={newTaskDescription}
                  onChangeText={setNewTaskDescription}
                  placeholder={t('taskmaster.enter_description', 'Beschreibung eingeben...')}
                  multiline
                  numberOfLines={4}
                />
              </View>

              <View style={styles.formGroup}>
                <Text style={styles.formLabel}>
                  {t('taskmaster.category', 'Kategorie')}
                </Text>
                <Picker
                  selectedValue={newTaskCategory}
                  style={styles.formPicker}
                  onValueChange={setNewTaskCategory}
                >
                  <Picker.Item label="RKSV Compliance" value={TaskCategory.RKSV_COMPLIANCE} />
                  <Picker.Item label="TSE Integration" value={TaskCategory.TSE_INTEGRATION} />
                  <Picker.Item label={t('taskmaster.development', 'Entwicklung')} value={TaskCategory.DEVELOPMENT} />
                  <Picker.Item label={t('taskmaster.bug_fix', 'Bug Fix')} value={TaskCategory.BUG_FIX} />
                  <Picker.Item label={t('taskmaster.testing', 'Testing')} value={TaskCategory.TESTING} />
                </Picker>
              </View>

              <View style={styles.formGroup}>
                <Text style={styles.formLabel}>
                  {t('taskmaster.priority', 'Priorität')}
                </Text>
                <Picker
                  selectedValue={newTaskPriority}
                  style={styles.formPicker}
                  onValueChange={setNewTaskPriority}
                >
                  <Picker.Item label={t('taskmaster.critical', 'Kritisch')} value={TaskPriority.CRITICAL} />
                  <Picker.Item label={t('taskmaster.high', 'Hoch')} value={TaskPriority.HIGH} />
                  <Picker.Item label={t('taskmaster.medium', 'Mittel')} value={TaskPriority.MEDIUM} />
                  <Picker.Item label={t('taskmaster.low', 'Niedrig')} value={TaskPriority.LOW} />
                </Picker>
              </View>

              <View style={styles.checkboxGroup}>
                <TouchableOpacity
                  style={styles.checkbox}
                  onPress={() => setNewTaskTseRequired(!newTaskTseRequired)}
                >
                  <Ionicons
                    name={newTaskTseRequired ? "checkbox" : "square-outline"}
                    size={24}
                    color="#2196F3"
                  />
                  <Text style={styles.checkboxLabel}>
                    {t('taskmaster.tse_required', 'TSE Signatur erforderlich')}
                  </Text>
                </TouchableOpacity>
              </View>
            </ScrollView>

            <View style={styles.modalFooter}>
              <TouchableOpacity
                style={[styles.modalButton, styles.cancelButton]}
                onPress={() => setShowCreateModal(false)}
              >
                <Text style={styles.cancelButtonText}>
                  {t('common.cancel', 'Abbrechen')}
                </Text>
              </TouchableOpacity>
              <TouchableOpacity
                style={[styles.modalButton, styles.createTaskButton]}
                onPress={handleCreateTask}
              >
                <Text style={styles.createButtonText}>
                  {t('taskmaster.create', 'Erstellen')}
                </Text>
              </TouchableOpacity>
            </View>
          </View>
        </Modal>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    backgroundColor: 'white',
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  headerTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
  },
  closeButton: {
    padding: 5,
  },
  statsContainer: {
    flexDirection: 'row',
    padding: 15,
    justifyContent: 'space-between',
  },
  statCard: {
    backgroundColor: 'white',
    padding: 15,
    borderRadius: 10,
    alignItems: 'center',
    flex: 1,
    marginHorizontal: 5,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  statNumber: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#2196F3',
  },
  statLabel: {
    fontSize: 12,
    color: '#666',
    textAlign: 'center',
    marginTop: 5,
  },
  filterContainer: {
    backgroundColor: 'white',
    padding: 15,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  searchInput: {
    backgroundColor: '#f5f5f5',
    padding: 12,
    borderRadius: 8,
    marginBottom: 15,
    fontSize: 16,
  },
  filterRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
  },
  filterGroup: {
    flex: 1,
    marginHorizontal: 5,
  },
  filterLabel: {
    fontSize: 14,
    fontWeight: '600',
    color: '#333',
    marginBottom: 5,
  },
  picker: {
    backgroundColor: '#f5f5f5',
    borderRadius: 8,
  },
  taskList: {
    flex: 1,
    padding: 15,
  },
  taskCard: {
    backgroundColor: 'white',
    padding: 15,
    borderRadius: 10,
    marginBottom: 10,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  taskHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 10,
  },
  categoryBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  categoryText: {
    color: 'white',
    fontSize: 10,
    fontWeight: 'bold',
  },
  taskTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 5,
  },
  taskDescription: {
    fontSize: 14,
    color: '#666',
    marginBottom: 10,
    lineHeight: 20,
  },
  taskFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  taskStatus: {
    flex: 1,
  },
  statusText: {
    fontSize: 12,
    fontWeight: '600',
  },
  tseBadge: {
    backgroundColor: '#FF9800',
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 8,
    marginHorizontal: 5,
  },
  tseText: {
    color: 'white',
    fontSize: 10,
    fontWeight: 'bold',
  },
  analyzeButton: {
    padding: 5,
  },
  createButton: {
    position: 'absolute',
    bottom: 30,
    right: 30,
    backgroundColor: '#2196F3',
    width: 60,
    height: 60,
    borderRadius: 30,
    justifyContent: 'center',
    alignItems: 'center',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 6,
    elevation: 8,
  },
  modalContainer: {
    flex: 1,
    backgroundColor: 'white',
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#333',
  },
  modalContent: {
    flex: 1,
    padding: 20,
  },
  formGroup: {
    marginBottom: 20,
  },
  formLabel: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 8,
  },
  formInput: {
    backgroundColor: '#f5f5f5',
    padding: 12,
    borderRadius: 8,
    fontSize: 16,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  textArea: {
    height: 100,
    textAlignVertical: 'top',
  },
  formPicker: {
    backgroundColor: '#f5f5f5',
    borderRadius: 8,
  },
  checkboxGroup: {
    marginBottom: 20,
  },
  checkbox: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  checkboxLabel: {
    fontSize: 16,
    color: '#333',
    marginLeft: 10,
  },
  modalFooter: {
    flexDirection: 'row',
    padding: 20,
    borderTopWidth: 1,
    borderTopColor: '#e0e0e0',
  },
  modalButton: {
    flex: 1,
    padding: 15,
    borderRadius: 8,
    alignItems: 'center',
    marginHorizontal: 5,
  },
  cancelButton: {
    backgroundColor: '#f5f5f5',
  },
  cancelButtonText: {
    fontSize: 16,
    color: '#666',
    fontWeight: '600',
  },
  createTaskButton: {
    backgroundColor: '#2196F3',
  },
  createButtonText: {
    fontSize: 16,
    color: 'white',
    fontWeight: 'bold',
  },
});

export default TaskMasterDashboard;
