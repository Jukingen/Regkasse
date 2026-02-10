/**
 * Simple Todo Tab Screen - Basit todo list ekranı
 * 
 * Bu ekran, Task-Master sisteminden bağımsız basit todo listesi sağlar.
 * Hızlı notlar ve günlük görevler için kullanılabilir.
 * 
 * Özellikler:
 * - Basit todo ekleme/silme/tamamlama
 * - Kategori desteği (RKSV, TSE, Allgemein)
 * - Öncelik sistemi
 * - Local storage
 * - Almanca UI
 * 
 * @author Frontend Team
 * @version 1.0.0
 * @since 2025-01-10
 */

import React from 'react';
import { Stack } from 'expo-router';
import { useTranslation } from 'react-i18next';
import SimpleTodo from '../../components/SimpleTodo';

export default function SimpleTodoScreen() {
  const { t } = useTranslation();

  return (
    <>
      <Stack.Screen
        options={{
          title: t('todo.title', 'Einfache Aufgaben'),
          headerStyle: { backgroundColor: '#f8f9fa' },
          headerTitleStyle: { fontWeight: 'bold' }
        }}
      />
      
      <SimpleTodo
        storageKey="registrierkasse_simple_todos"
        maxItems={100}
        enableCategories={true}
        enablePriority={true}
      />
    </>
  );
}
