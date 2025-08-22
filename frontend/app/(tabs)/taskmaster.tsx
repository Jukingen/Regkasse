/**
 * TaskMaster Tab Screen - AI destekli görev yönetimi ekranı
 * 
 * Bu ekran, task-master-ai entegrasyonu ile gelişmiş görev yönetimi sunar.
 * RKSV kurallarına uygun olarak tasarlanmış, Almanca UI ile çok dilli destek içerir.
 * 
 * Özellikler:
 * - Task-Master-AI dashboard entegrasyonu
 * - Responsive mobil tasarım
 * - Offline çalışma desteği
 * - RKSV compliance tracking
 * - AI destekli görev analizi
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
  SafeAreaView,
  Alert
} from 'react-native';
import { Stack } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';
import TaskMasterDashboard from '../../components/TaskMasterDashboard';
import { useAuth } from '../../contexts/AuthContext';

export default function TaskMasterScreen() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [showDashboard, setShowDashboard] = useState(false);

  /**
   * TaskMaster dashboard'unu aç
   */
  const handleOpenDashboard = () => {
    // Kullanıcı yetki kontrolü
    if (!user) {
      Alert.alert(
        t('error.title', 'Fehler'),
        t('error.not_authenticated', 'Sie sind nicht angemeldet'),
        [{ text: t('common.ok', 'OK') }]
      );
      return;
    }

    setShowDashboard(true);
  };

  /**
   * Dashboard'u kapat
   */
  const handleCloseDashboard = () => {
    setShowDashboard(false);
  };

  /**
   * Hızlı görev oluşturma örnekleri
   */
  const quickTaskExamples = [
    {
      title: t('taskmaster.quick_rksv', 'RKSV Compliance Prüfung'),
      description: t('taskmaster.quick_rksv_desc', 'TSE Signatur und Compliance überprüfen'),
      icon: 'shield-checkmark-outline',
      color: '#FF5722'
    },
    {
      title: t('taskmaster.quick_tse', 'TSE Integration'),
      description: t('taskmaster.quick_tse_desc', 'TSE Gerät Verbindung testen'),
      icon: 'hardware-chip-outline',
      color: '#FF9800'
    },
    {
      title: t('taskmaster.quick_invoice', 'Rechnungsmanagement'),
      description: t('taskmaster.quick_invoice_desc', 'Rechnungen verwalten und prüfen'),
      icon: 'document-outline',
      color: '#2196F3'
    },
    {
      title: t('taskmaster.quick_payment', 'Zahlungsabwicklung'),
      description: t('taskmaster.quick_payment_desc', 'Payment Gateway testen'),
      icon: 'card-outline',
      color: '#4CAF50'
    }
  ];

  return (
    <SafeAreaView style={styles.container}>
      <Stack.Screen
        options={{
          title: t('taskmaster.title', 'Task Master AI'),
          headerStyle: { backgroundColor: '#f8f9fa' },
          headerTitleStyle: { fontWeight: 'bold' }
        }}
      />

      <View style={styles.content}>
        {/* Header Section */}
        <View style={styles.header}>
          <View style={styles.headerIcon}>
            <Ionicons name="analytics" size={32} color="#2196F3" />
          </View>
          <Text style={styles.headerTitle}>
            {t('taskmaster.welcome_title', 'AI-powered Task Management')}
          </Text>
          <Text style={styles.headerSubtitle}>
            {t('taskmaster.welcome_subtitle', 'Optimieren Sie Ihren Arbeitsablauf mit künstlicher Intelligenz')}
          </Text>
        </View>

        {/* Main Dashboard Button */}
        <TouchableOpacity 
          style={styles.mainButton}
          onPress={handleOpenDashboard}
        >
          <View style={styles.mainButtonContent}>
            <Ionicons name="grid-outline" size={24} color="white" />
            <Text style={styles.mainButtonText}>
              {t('taskmaster.open_dashboard', 'Task Dashboard öffnen')}
            </Text>
          </View>
          <Ionicons name="chevron-forward" size={20} color="white" />
        </TouchableOpacity>

        {/* Features Overview */}
        <View style={styles.featuresSection}>
          <Text style={styles.sectionTitle}>
            {t('taskmaster.features_title', 'Hauptfunktionen')}
          </Text>

          <View style={styles.featureGrid}>
            {quickTaskExamples.map((feature, index) => (
              <TouchableOpacity
                key={index}
                style={[styles.featureCard, { borderLeftColor: feature.color }]}
                onPress={handleOpenDashboard}
              >
                <View style={styles.featureIconContainer}>
                  <Ionicons 
                    name={feature.icon as any} 
                    size={24} 
                    color={feature.color} 
                  />
                </View>
                <View style={styles.featureContent}>
                  <Text style={styles.featureTitle}>{feature.title}</Text>
                  <Text style={styles.featureDescription}>
                    {feature.description}
                  </Text>
                </View>
                <Ionicons 
                  name="chevron-forward" 
                  size={16} 
                  color="#999" 
                />
              </TouchableOpacity>
            ))}
          </View>
        </View>

        {/* Info Section */}
        <View style={styles.infoSection}>
          <View style={styles.infoCard}>
            <Ionicons name="information-circle-outline" size={20} color="#2196F3" />
            <Text style={styles.infoText}>
              {t('taskmaster.info_text', 'Task Master AI hilft Ihnen bei der effizienten Verwaltung Ihrer Aufgaben mit KI-Unterstützung und RKSV-Compliance.')}
            </Text>
          </View>
        </View>

        {/* User Info */}
        {user && (
          <View style={styles.userInfo}>
            <Text style={styles.userInfoText}>
              {t('taskmaster.logged_in_as', 'Angemeldet als')}: {user.email}
            </Text>
            <Text style={styles.userRoleText}>
              {t('taskmaster.role', 'Rolle')}: {user.role}
            </Text>
          </View>
        )}
      </View>

      {/* TaskMaster Dashboard Modal */}
      <TaskMasterDashboard
        visible={showDashboard}
        onClose={handleCloseDashboard}
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f8f9fa',
  },
  content: {
    flex: 1,
    padding: 20,
  },
  header: {
    alignItems: 'center',
    marginBottom: 30,
    paddingVertical: 20,
  },
  headerIcon: {
    width: 80,
    height: 80,
    borderRadius: 40,
    backgroundColor: 'white',
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 15,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 8,
    elevation: 4,
  },
  headerTitle: {
    fontSize: 24,
    fontWeight: 'bold',
    color: '#333',
    textAlign: 'center',
    marginBottom: 8,
  },
  headerSubtitle: {
    fontSize: 16,
    color: '#666',
    textAlign: 'center',
    lineHeight: 22,
  },
  mainButton: {
    backgroundColor: '#2196F3',
    paddingVertical: 18,
    paddingHorizontal: 20,
    borderRadius: 12,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 30,
    shadowColor: '#2196F3',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
    elevation: 6,
  },
  mainButtonContent: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  mainButtonText: {
    color: 'white',
    fontSize: 18,
    fontWeight: 'bold',
    marginLeft: 12,
  },
  featuresSection: {
    marginBottom: 30,
  },
  sectionTitle: {
    fontSize: 20,
    fontWeight: 'bold',
    color: '#333',
    marginBottom: 15,
  },
  featureGrid: {
    gap: 12,
  },
  featureCard: {
    backgroundColor: 'white',
    padding: 16,
    borderRadius: 12,
    flexDirection: 'row',
    alignItems: 'center',
    borderLeftWidth: 4,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  featureIconContainer: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: '#f5f5f5',
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
  },
  featureContent: {
    flex: 1,
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
  infoSection: {
    marginBottom: 20,
  },
  infoCard: {
    backgroundColor: '#E3F2FD',
    padding: 16,
    borderRadius: 12,
    flexDirection: 'row',
    alignItems: 'flex-start',
  },
  infoText: {
    flex: 1,
    fontSize: 14,
    color: '#1976D2',
    lineHeight: 20,
    marginLeft: 12,
  },
  userInfo: {
    backgroundColor: 'white',
    padding: 16,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: '#e0e0e0',
  },
  userInfoText: {
    fontSize: 14,
    color: '#333',
    marginBottom: 4,
  },
  userRoleText: {
    fontSize: 12,
    color: '#666',
    textTransform: 'capitalize',
  },
});
