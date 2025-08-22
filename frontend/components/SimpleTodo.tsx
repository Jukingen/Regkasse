/**
 * SimpleTodo - Basit React Native Todo Komponenti
 * 
 * Bu component, klasik todo list fonksiyonalitesi sağlar.
 * Task-Master sisteminden bağımsız, basit kullanım için tasarlanmıştır.
 * 
 * Özellikler:
 * - Todo ekleme/silme/tamamlama
 * - Local state management
 * - RKSV iş akışları için hızlı notlar
 * - Almanca UI
 * 
 * @author Frontend Team
 * @version 1.0.0
 * @since 2025-01-10
 */

import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  FlatList,
  StyleSheet,
  Alert,
  SafeAreaView
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import AsyncStorage from '@react-native-async-storage/async-storage';

// Basit Todo item interface'i
interface TodoItem {
  id: string;
  text: string;
  completed: boolean;
  createdAt: Date;
  priority: 'low' | 'medium' | 'high';
  category?: 'rksv' | 'tse' | 'allgemein';
}

interface SimpleTodoProps {
  storageKey?: string;
  maxItems?: number;
  enableCategories?: boolean;
  enablePriority?: boolean;
}

const SimpleTodo: React.FC<SimpleTodoProps> = ({
  storageKey = 'simple_todo_items',
  maxItems = 50,
  enableCategories = true,
  enablePriority = true
}) => {
  const { t } = useTranslation();
  
  // State tanımlamaları
  const [todos, setTodos] = useState<TodoItem[]>([]);
  const [inputText, setInputText] = useState<string>('');
  const [selectedPriority, setSelectedPriority] = useState<'low' | 'medium' | 'high'>('medium');
  const [selectedCategory, setSelectedCategory] = useState<'rksv' | 'tse' | 'allgemein'>('allgemein');
  const [loading, setLoading] = useState<boolean>(false);

  /**
   * Component mount olduğunda todo'ları yükle
   */
  useEffect(() => {
    loadTodos();
  }, []);

  /**
   * Todo'ları AsyncStorage'dan yükle
   */
  const loadTodos = async () => {
    try {
      setLoading(true);
      const savedTodos = await AsyncStorage.getItem(storageKey);
      if (savedTodos) {
        const parsedTodos = JSON.parse(savedTodos).map((todo: any) => ({
          ...todo,
          createdAt: new Date(todo.createdAt)
        }));
        setTodos(parsedTodos);
      }
    } catch (error) {
      console.error('Todo loading failed:', error);
      Alert.alert(
        t('error.title', 'Fehler'),
        t('error.todo_load', 'Aufgaben konnten nicht geladen werden'),
        [{ text: t('common.ok', 'OK') }]
      );
    } finally {
      setLoading(false);
    }
  };

  /**
   * Todo'ları AsyncStorage'a kaydet
   */
  const saveTodos = async (todoList: TodoItem[]) => {
    try {
      await AsyncStorage.setItem(storageKey, JSON.stringify(todoList));
    } catch (error) {
      console.error('Todo saving failed:', error);
    }
  };

  /**
   * Yeni todo ekle
   */
  const addTodo = () => {
    if (inputText.trim() === '') {
      Alert.alert(
        t('error.title', 'Fehler'),
        t('error.todo_empty', 'Bitte geben Sie eine Aufgabe ein'),
        [{ text: t('common.ok', 'OK') }]
      );
      return;
    }

    if (todos.length >= maxItems) {
      Alert.alert(
        t('error.title', 'Fehler'),
        t('error.todo_limit', `Maximum ${maxItems} Aufgaben erreicht`),
        [{ text: t('common.ok', 'OK') }]
      );
      return;
    }

    const newTodo: TodoItem = {
      id: generateTodoId(),
      text: inputText.trim(),
      completed: false,
      createdAt: new Date(),
      priority: selectedPriority,
      category: enableCategories ? selectedCategory : undefined
    };

    const updatedTodos = [newTodo, ...todos];
    setTodos(updatedTodos);
    saveTodos(updatedTodos);
    setInputText('');

    // Success feedback
    console.log(`✅ Todo added: ${newTodo.text}`);
  };

  /**
   * Todo'yu tamamlandı olarak işaretle
   */
  const toggleTodo = (id: string) => {
    const updatedTodos = todos.map(todo =>
      todo.id === id ? { ...todo, completed: !todo.completed } : todo
    );
    setTodos(updatedTodos);
    saveTodos(updatedTodos);
  };

  /**
   * Todo'yu sil
   */
  const deleteTodo = (id: string) => {
    Alert.alert(
      t('common.confirm', 'Bestätigen'),
      t('todo.delete_confirm', 'Möchten Sie diese Aufgabe löschen?'),
      [
        { text: t('common.cancel', 'Abbrechen'), style: 'cancel' },
        { 
          text: t('common.delete', 'Löschen'), 
          style: 'destructive',
          onPress: () => {
            const updatedTodos = todos.filter(todo => todo.id !== id);
            setTodos(updatedTodos);
            saveTodos(updatedTodos);
          }
        }
      ]
    );
  };

  /**
   * Tüm tamamlanmış todo'ları temizle
   */
  const clearCompleted = () => {
    if (todos.filter(todo => todo.completed).length === 0) {
      Alert.alert(
        t('info.title', 'Info'),
        t('todo.no_completed', 'Keine abgeschlossenen Aufgaben vorhanden'),
        [{ text: t('common.ok', 'OK') }]
      );
      return;
    }

    Alert.alert(
      t('common.confirm', 'Bestätigen'),
      t('todo.clear_completed_confirm', 'Möchten Sie alle abgeschlossenen Aufgaben löschen?'),
      [
        { text: t('common.cancel', 'Abbrechen'), style: 'cancel' },
        { 
          text: t('common.delete', 'Löschen'), 
          style: 'destructive',
          onPress: () => {
            const updatedTodos = todos.filter(todo => !todo.completed);
            setTodos(updatedTodos);
            saveTodos(updatedTodos);
          }
        }
      ]
    );
  };

  /**
   * Benzersiz ID oluştur
   */
  const generateTodoId = (): string => {
    return `todo_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  };

  /**
   * Kategori rengi al
   */
  const getCategoryColor = (category?: string): string => {
    const colors = {
      rksv: '#FF5722',
      tse: '#FF9800',
      allgemein: '#2196F3'
    };
    return colors[category as keyof typeof colors] || '#2196F3';
  };

  /**
   * Öncelik ikonu al
   */
  const getPriorityIcon = (priority: string): string => {
    const icons = {
      high: 'chevron-up',
      medium: 'remove',
      low: 'chevron-down'
    };
    return icons[priority as keyof typeof icons] || 'remove';
  };

  /**
   * Todo item render
   */
  const renderTodoItem = ({ item }: { item: TodoItem }) => (
    <View style={[
      styles.todoItem,
      item.completed && styles.completedTodo
    ]}>
      {/* Todo content */}
      <TouchableOpacity 
        style={styles.todoContent}
        onPress={() => toggleTodo(item.id)}
      >
        <Ionicons
          name={item.completed ? "checkbox" : "square-outline"}
          size={24}
          color={item.completed ? "#4CAF50" : "#666"}
        />
        <Text style={[
          styles.todoText,
          item.completed && styles.completedText
        ]}>
          {item.text}
        </Text>
      </TouchableOpacity>

      {/* Todo metadata */}
      <View style={styles.todoMeta}>
        {enablePriority && (
          <Ionicons
            name={getPriorityIcon(item.priority)}
            size={16}
            color={item.priority === 'high' ? '#F44336' : '#666'}
          />
        )}
        {enableCategories && item.category && (
          <View style={[
            styles.categoryBadge,
            { backgroundColor: getCategoryColor(item.category) }
          ]}>
            <Text style={styles.categoryText}>
              {item.category.toUpperCase()}
            </Text>
          </View>
        )}
        <TouchableOpacity onPress={() => deleteTodo(item.id)}>
          <Ionicons name="trash-outline" size={20} color="#F44336" />
        </TouchableOpacity>
      </View>
    </View>
  );

  /**
   * Statistics
   */
  const totalTodos = todos.length;
  const completedTodos = todos.filter(todo => todo.completed).length;
  const pendingTodos = totalTodos - completedTodos;

  return (
    <SafeAreaView style={styles.container}>
      {/* Header */}
      <View style={styles.header}>
        <Text style={styles.title}>
          {t('todo.title', 'Einfache Aufgaben')}
        </Text>
        <View style={styles.stats}>
          <Text style={styles.statsText}>
            {t('todo.stats', `${pendingTodos} offen, ${completedTodos} erledigt`)}
          </Text>
        </View>
      </View>

      {/* Input Section */}
      <View style={styles.inputContainer}>
        <TextInput
          style={styles.textInput}
          value={inputText}
          onChangeText={setInputText}
          placeholder={t('todo.placeholder', 'Neue Aufgabe hinzufügen...')}
          placeholderTextColor="#999"
          multiline
          onSubmitEditing={addTodo}
          returnKeyType="done"
        />
        
        {/* Priority & Category Selection */}
        {(enablePriority || enableCategories) && (
          <View style={styles.optionsRow}>
            {enablePriority && (
              <View style={styles.optionGroup}>
                <Text style={styles.optionLabel}>
                  {t('todo.priority', 'Priorität')}
                </Text>
                <View style={styles.priorityButtons}>
                  {(['low', 'medium', 'high'] as const).map(priority => (
                    <TouchableOpacity
                      key={priority}
                      style={[
                        styles.priorityButton,
                        selectedPriority === priority && styles.selectedPriority
                      ]}
                      onPress={() => setSelectedPriority(priority)}
                    >
                      <Text style={[
                        styles.priorityText,
                        selectedPriority === priority && styles.selectedPriorityText
                      ]}>
                        {priority === 'high' ? 'H' : priority === 'medium' ? 'M' : 'L'}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </View>
            )}

            {enableCategories && (
              <View style={styles.optionGroup}>
                <Text style={styles.optionLabel}>
                  {t('todo.category', 'Kategorie')}
                </Text>
                <View style={styles.categoryButtons}>
                  {(['allgemein', 'rksv', 'tse'] as const).map(category => (
                    <TouchableOpacity
                      key={category}
                      style={[
                        styles.categoryButton,
                        { backgroundColor: getCategoryColor(category) },
                        selectedCategory === category && styles.selectedCategory
                      ]}
                      onPress={() => setSelectedCategory(category)}
                    >
                      <Text style={styles.categoryButtonText}>
                        {category.toUpperCase()}
                      </Text>
                    </TouchableOpacity>
                  ))}
                </View>
              </View>
            )}
          </View>
        )}

        <TouchableOpacity style={styles.addButton} onPress={addTodo}>
          <Ionicons name="add" size={24} color="white" />
        </TouchableOpacity>
      </View>

      {/* Todo List */}
      <FlatList
        data={todos}
        renderItem={renderTodoItem}
        keyExtractor={item => item.id}
        style={styles.todoList}
        showsVerticalScrollIndicator={false}
        ListEmptyComponent={
          <View style={styles.emptyContainer}>
            <Ionicons name="clipboard-outline" size={64} color="#ccc" />
            <Text style={styles.emptyText}>
              {t('todo.empty', 'Keine Aufgaben vorhanden')}
            </Text>
            <Text style={styles.emptySubtext}>
              {t('todo.empty_subtitle', 'Fügen Sie Ihre erste Aufgabe hinzu')}
            </Text>
          </View>
        }
      />

      {/* Clear Completed Button */}
      {completedTodos > 0 && (
        <TouchableOpacity style={styles.clearButton} onPress={clearCompleted}>
          <Text style={styles.clearButtonText}>
            {t('todo.clear_completed', `${completedTodos} erledigte löschen`)}
          </Text>
        </TouchableOpacity>
      )}
    </SafeAreaView>
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
  stats: {
    marginTop: 5,
  },
  statsText: {
    fontSize: 14,
    color: '#666',
  },
  inputContainer: {
    backgroundColor: 'white',
    padding: 15,
    borderBottomWidth: 1,
    borderBottomColor: '#e0e0e0',
  },
  textInput: {
    backgroundColor: '#f8f9fa',
    padding: 15,
    borderRadius: 10,
    fontSize: 16,
    maxHeight: 100,
    marginBottom: 10,
  },
  optionsRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 10,
  },
  optionGroup: {
    flex: 1,
    marginHorizontal: 5,
  },
  optionLabel: {
    fontSize: 12,
    color: '#666',
    marginBottom: 5,
    fontWeight: '600',
  },
  priorityButtons: {
    flexDirection: 'row',
  },
  priorityButton: {
    width: 30,
    height: 30,
    borderRadius: 15,
    backgroundColor: '#e0e0e0',
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 5,
  },
  selectedPriority: {
    backgroundColor: '#2196F3',
  },
  priorityText: {
    fontSize: 12,
    fontWeight: 'bold',
    color: '#666',
  },
  selectedPriorityText: {
    color: 'white',
  },
  categoryButtons: {
    flexDirection: 'row',
  },
  categoryButton: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 8,
    marginRight: 5,
  },
  selectedCategory: {
    borderWidth: 2,
    borderColor: '#333',
  },
  categoryButtonText: {
    fontSize: 10,
    fontWeight: 'bold',
    color: 'white',
  },
  addButton: {
    backgroundColor: '#2196F3',
    width: 50,
    height: 50,
    borderRadius: 25,
    justifyContent: 'center',
    alignItems: 'center',
    alignSelf: 'flex-end',
  },
  todoList: {
    flex: 1,
    paddingHorizontal: 15,
  },
  todoItem: {
    backgroundColor: 'white',
    padding: 15,
    marginVertical: 5,
    borderRadius: 10,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.1,
    shadowRadius: 2,
    elevation: 2,
  },
  completedTodo: {
    opacity: 0.6,
  },
  todoContent: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
  },
  todoText: {
    fontSize: 16,
    color: '#333',
    marginLeft: 12,
    flex: 1,
  },
  completedText: {
    textDecorationLine: 'line-through',
    color: '#999',
  },
  todoMeta: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  categoryBadge: {
    paddingHorizontal: 6,
    paddingVertical: 2,
    borderRadius: 8,
    marginHorizontal: 5,
  },
  categoryText: {
    fontSize: 10,
    fontWeight: 'bold',
    color: 'white',
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingVertical: 50,
  },
  emptyText: {
    fontSize: 18,
    color: '#999',
    marginTop: 15,
    fontWeight: '600',
  },
  emptySubtext: {
    fontSize: 14,
    color: '#ccc',
    marginTop: 5,
  },
  clearButton: {
    backgroundColor: '#F44336',
    margin: 15,
    padding: 15,
    borderRadius: 10,
    alignItems: 'center',
  },
  clearButtonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: 'bold',
  },
});

export default SimpleTodo;
