/**
 * TypeScript type definitions for task-master-ai package
 * 
 * Bu dosya, task-master-ai paketinin TypeScript type tanımlarını içerir.
 * Paket için resmi type definitions mevcut olmadığı için özel tanımlar oluşturuldu.
 * 
 * @author Frontend Team
 * @version 1.0.0
 * @since 2025-01-10
 */

declare module 'task-master-ai' {
  
  // Task Manager konfigürasyon interface'i
  export interface TaskManagerConfig {
    storageAdapter?: {
      get: (key: string) => Promise<any>;
      set: (key: string, value: any) => Promise<boolean>;
      remove: (key: string) => Promise<boolean>;
    };
    enableAI?: boolean;
    maxConcurrentTasks?: number;
    autoSave?: boolean;
    context?: {
      project?: string;
      framework?: string;
      compliance?: string;
      [key: string]: any;
    };
  }

  // Task interface
  export interface TaskMasterTask {
    id: string;
    title: string;
    description: string;
    priority: number;
    category: string;
    status?: string;
    createdAt: string | Date;
    updatedAt: string | Date;
    metadata?: {
      [key: string]: any;
    };
  }

  // AI Analysis sonucu
  export interface TaskAnalysis {
    suggestions?: string[];
    estimatedDuration?: number;
    complexity?: 'low' | 'medium' | 'high';
    dependencies?: string[];
    [key: string]: any;
  }

  // TaskManager sınıfı
  export class TaskManager {
    constructor(config?: TaskManagerConfig);
    
    initialize(): Promise<void>;
    
    addTask(task: Partial<TaskMasterTask>): Promise<TaskMasterTask>;
    
    getTasks(filter?: any): Promise<TaskMasterTask[]>;
    
    updateTask(taskId: string, updates: Partial<TaskMasterTask>): Promise<TaskMasterTask>;
    
    removeTask(taskId: string): Promise<void>;
    
    analyzeTask(taskId: string): Promise<TaskAnalysis>;
    
    shutdown?(): Promise<void>;
  }

  // Export edilecek diğer tipler
  export type TaskPriorityLevel = 1 | 2 | 3 | 4 | 5;
  export type TaskStatusType = 'pending' | 'in_progress' | 'completed' | 'cancelled' | 'blocked';
}
