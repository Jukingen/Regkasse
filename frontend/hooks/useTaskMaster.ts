/**
 * useTaskMaster Hook - Task-Master-AI entegrasyonu için React hook
 * 
 * Bu hook, task-master-ai paketini React Native componentleri ile entegre eder.
 * RKSV kurallarına uygun görev yönetimi sağlar ve AI destekli özellikler sunar.
 * 
 * Özellikler:
 * - Görev CRUD işlemleri
 * - AI destekli görev analizi
 * - Real-time görev güncellemeleri
 * - RKSV compliance tracking
 * - Offline çalışma desteği
 * 
 * @author Frontend Team
 * @version 1.0.0
 * @since 2025-01-10
 */

import { useState, useEffect, useCallback } from 'react';
import { Alert } from 'react-native';
import { useTranslation } from 'react-i18next';
import taskMasterService, { 
  Task, 
  TaskCategory, 
  TaskPriority, 
  TaskStatus 
} from '../services/TaskMasterService';

interface UseTaskMasterReturn {
  // Görev listesi ve durumlar
  tasks: Task[];
  loading: boolean;
  error: string | null;
  
  // CRUD işlemleri
  createTask: (taskData: Omit<Task, 'id' | 'createdAt' | 'updatedAt'>) => Promise<Task | null>;
  updateTask: (taskId: string, updates: Partial<Task>) => Promise<boolean>;
  deleteTask: (taskId: string) => Promise<boolean>;
  refreshTasks: () => Promise<void>;
  
  // Filtreleme ve arama
  filterTasks: (filter: Partial<Task>) => Task[];
  searchTasks: (query: string) => Task[];
  
  // AI destekli özellikler
  analyzeTask: (taskId: string) => Promise<any>;
  generateTaskSuggestions: (category: TaskCategory) => Promise<string[]>;
  
  // RKSV özel işlevler
  getRksvComplianceTasks: () => Task[];
  getTseRequiredTasks: () => Task[];
  getCriticalTasks: () => Task[];
  
  // Durum kontrolü
  isReady: boolean;
}

export const useTaskMaster = (): UseTaskMasterReturn => {
  const { t } = useTranslation();
  
  // State tanımlamaları
  const [tasks, setTasks] = useState<Task[]>([]);
  const [loading, setLoading] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);
  const [isReady, setIsReady] = useState<boolean>(false);

  /**
   * Servis başlatma ve başlangıç görevlerini yükleme
   */
  useEffect(() => {
    initializeService();
  }, []);

  /**
   * TaskMaster servisini başlat
   */
  const initializeService = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      
      if (!taskMasterService.isReady()) {
        await taskMasterService.initialize();
      }
      
      setIsReady(true);
      await refreshTasks();
      
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Unknown error occurred';
      setError(errorMessage);
      console.error('TaskMaster initialization failed:', err);
      
      // Kullanıcıya Almanca hata mesajı göster
      Alert.alert(
        t('error.title', 'Fehler'),
        t('error.taskmaster_init', 'Task-Management System konnte nicht initialisiert werden'),
        [{ text: t('common.ok', 'OK') }]
      );
    } finally {
      setLoading(false);
    }
  }, [t]);

  /**
   * Görev listesini yenile
   */
  const refreshTasks = useCallback(async (): Promise<void> => {
    try {
      setLoading(true);
      setError(null);
      
      const fetchedTasks = await taskMasterService.getTasks();
      setTasks(fetchedTasks);
      
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to refresh tasks';
      setError(errorMessage);
      console.error('Failed to refresh tasks:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  /**
   * Yeni görev oluştur
   */
  const createTask = useCallback(async (
    taskData: Omit<Task, 'id' | 'createdAt' | 'updatedAt'>
  ): Promise<Task | null> => {
    try {
      setLoading(true);
      setError(null);
      
      const newTask = await taskMasterService.createTask(taskData);
      
      // Görev listesini güncelle
      setTasks(prevTasks => [...prevTasks, newTask]);
      
      // Başarı mesajı göster
      Alert.alert(
        t('success.title', 'Erfolg'),
        t('success.task_created', 'Aufgabe erfolgreich erstellt'),
        [{ text: t('common.ok', 'OK') }]
      );
      
      return newTask;
      
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to create task';
      setError(errorMessage);
      console.error('Failed to create task:', err);
      
      Alert.alert(
        t('error.title', 'Fehler'),
        t('error.task_create', 'Aufgabe konnte nicht erstellt werden'),
        [{ text: t('common.ok', 'OK') }]
      );
      
      return null;
    } finally {
      setLoading(false);
    }
  }, [t]);

  /**
   * Görevi güncelle
   */
  const updateTask = useCallback(async (
    taskId: string, 
    updates: Partial<Task>
  ): Promise<boolean> => {
    try {
      setLoading(true);
      setError(null);
      
      const updatedTask = await taskMasterService.updateTask(taskId, updates);
      
      if (updatedTask) {
        // Görev listesini güncelle
        setTasks(prevTasks => 
          prevTasks.map(task => 
            task.id === taskId ? updatedTask : task
          )
        );
        
        return true;
      }
      
      return false;
      
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to update task';
      setError(errorMessage);
      console.error('Failed to update task:', err);
      
      Alert.alert(
        t('error.title', 'Fehler'),
        t('error.task_update', 'Aufgabe konnte nicht aktualisiert werden'),
        [{ text: t('common.ok', 'OK') }]
      );
      
      return false;
    } finally {
      setLoading(false);
    }
  }, [t]);

  /**
   * Görevi sil
   */
  const deleteTask = useCallback(async (taskId: string): Promise<boolean> => {
    try {
      setLoading(true);
      setError(null);
      
      const success = await taskMasterService.deleteTask(taskId);
      
      if (success) {
        // Görev listesinden kaldır
        setTasks(prevTasks => prevTasks.filter(task => task.id !== taskId));
        
        Alert.alert(
          t('success.title', 'Erfolg'),
          t('success.task_deleted', 'Aufgabe erfolgreich gelöscht'),
          [{ text: t('common.ok', 'OK') }]
        );
        
        return true;
      }
      
      return false;
      
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to delete task';
      setError(errorMessage);
      console.error('Failed to delete task:', err);
      
      Alert.alert(
        t('error.title', 'Fehler'),
        t('error.task_delete', 'Aufgabe konnte nicht gelöscht werden'),
        [{ text: t('common.ok', 'OK') }]
      );
      
      return false;
    } finally {
      setLoading(false);
    }
  }, [t]);

  /**
   * Görevleri filtrele
   */
  const filterTasks = useCallback((filter: Partial<Task>): Task[] => {
    return tasks.filter(task => {
      return Object.entries(filter).every(([key, value]) => {
        if (value === undefined || value === null) return true;
        return task[key as keyof Task] === value;
      });
    });
  }, [tasks]);

  /**
   * Görevlerde arama yap
   */
  const searchTasks = useCallback((query: string): Task[] => {
    if (!query.trim()) return tasks;
    
    const lowerQuery = query.toLowerCase();
    return tasks.filter(task => 
      task.title.toLowerCase().includes(lowerQuery) ||
      task.description.toLowerCase().includes(lowerQuery) ||
      task.tags?.some(tag => tag.toLowerCase().includes(lowerQuery))
    );
  }, [tasks]);

  /**
   * AI ile görev analizi yap
   */
  const analyzeTask = useCallback(async (taskId: string) => {
    try {
      setLoading(true);
      setError(null);
      
      const analysis = await taskMasterService.analyzeTaskWithAI(taskId);
      return analysis;
      
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'AI analysis failed';
      setError(errorMessage);
      console.error('AI task analysis failed:', err);
      return null;
    } finally {
      setLoading(false);
    }
  }, []);

  /**
   * Kategori için AI görev önerileri oluştur (Çok dilli)
   */
  const generateTaskSuggestions = useCallback(async (category: TaskCategory): Promise<string[]> => {
    try {
      // Mevcut dil ayarını al
      const currentLanguage = i18n.language || 'de';
      
      return await taskMasterService.generateTaskSuggestions(category, currentLanguage);
      
    } catch (err) {
      console.error('Failed to generate task suggestions:', err);
      return [];
    }
  }, [i18n.language]);

  /**
   * RKSV Compliance görevlerini getir
   */
  const getRksvComplianceTasks = useCallback((): Task[] => {
    return tasks.filter(task => 
      task.category === TaskCategory.RKSV_COMPLIANCE ||
      task.category === TaskCategory.TSE_INTEGRATION ||
      task.tseRequired === true
    );
  }, [tasks]);

  /**
   * TSE gerekli görevleri getir
   */
  const getTseRequiredTasks = useCallback((): Task[] => {
    return tasks.filter(task => task.tseRequired === true);
  }, [tasks]);

  /**
   * Kritik görevleri getir
   */
  const getCriticalTasks = useCallback((): Task[] => {
    return tasks.filter(task => task.priority === TaskPriority.CRITICAL);
  }, [tasks]);

  return {
    // Görev listesi ve durumlar
    tasks,
    loading,
    error,
    
    // CRUD işlemleri
    createTask,
    updateTask,
    deleteTask,
    refreshTasks,
    
    // Filtreleme ve arama
    filterTasks,
    searchTasks,
    
    // AI destekli özellikler
    analyzeTask,
    generateTaskSuggestions,
    
    // RKSV özel işlevler
    getRksvComplianceTasks,
    getTseRequiredTasks,
    getCriticalTasks,
    
    // Durum kontrolü
    isReady
  };
};

export default useTaskMaster;
