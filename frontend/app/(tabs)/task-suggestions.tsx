/**
 * Task Suggestions Screen - Görev önerileri demo ekranı
 * 
 * Bu ekran, Task-Master AI sisteminden nasıl görev önerileri alacağınızı
 * gösterir ve test etmenize olanak sağlar.
 * 
 * @author Frontend Team
 * @version 1.0.0
 * @since 2025-01-10
 */

import React from 'react';
import { Stack } from 'expo-router';
import { useTranslation } from 'react-i18next';
import TaskSuggestionsDemo from '../../components/TaskSuggestionsDemo';

export default function TaskSuggestionsScreen() {
  const { t } = useTranslation();

  return (
    <>
      <Stack.Screen
        options={{
          title: 'Task Suggestions Demo',
          headerStyle: { backgroundColor: '#f8f9fa' },
          headerTitleStyle: { fontWeight: 'bold' }
        }}
      />
      
      <TaskSuggestionsDemo />
    </>
  );
}
