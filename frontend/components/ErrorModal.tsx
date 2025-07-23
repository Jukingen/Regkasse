// Türkçe Açıklama: Ödeme hatalarını kullanıcı dostu şekilde gösteren, çok dilli hata yönetimi sistemi. Backend'den gelen hata kodları UI'da anlaşılır mesajlara çevrilir.

import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Modal } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

// Hata kodları ve çok dilli mesajlar
const ERROR_MESSAGES = {
  'CARD_DECLINED': {
    tr: 'Kart reddedildi. Lütfen başka bir kart deneyin.',
    de: 'Karte abgelehnt. Bitte versuchen Sie eine andere Karte.',
    en: 'Card declined. Please try another card.'
  },
  'INSUFFICIENT_FUNDS': {
    tr: 'Yetersiz bakiye. Lütfen kartınızı kontrol edin.',
    de: 'Unzureichende Mittel. Bitte überprüfen Sie Ihre Karte.',
    en: 'Insufficient funds. Please check your card.'
  },
  'TSE_NOT_CONNECTED': {
    tr: 'TSE cihazı bağlı değil. Lütfen teknik servisle iletişime geçin.',
    de: 'TSE-Gerät nicht verbunden. Bitte kontaktieren Sie den technischen Support.',
    en: 'TSE device not connected. Please contact technical support.'
  },
  'NETWORK_ERROR': {
    tr: 'Bağlantı hatası. İnternet bağlantınızı kontrol edin.',
    de: 'Verbindungsfehler. Überprüfen Sie Ihre Internetverbindung.',
    en: 'Network error. Please check your internet connection.'
  },
  'PAYMENT_TIMEOUT': {
    tr: 'Ödeme zaman aşımına uğradı. Lütfen tekrar deneyin.',
    de: 'Zahlungszeitüberschreitung. Bitte versuchen Sie es erneut.',
    en: 'Payment timeout. Please try again.'
  },
  'INVALID_AMOUNT': {
    tr: 'Geçersiz tutar. Lütfen tutarı kontrol edin.',
    de: 'Ungültiger Betrag. Bitte überprüfen Sie den Betrag.',
    en: 'Invalid amount. Please check the amount.'
  },
  'DEFAULT': {
    tr: 'Bir hata oluştu. Lütfen tekrar deneyin.',
    de: 'Ein Fehler ist aufgetreten. Bitte versuchen Sie es erneut.',
    en: 'An error occurred. Please try again.'
  }
};

type ErrorModalProps = {
  visible: boolean;
  errorCode: string;
  errorMessage?: string;
  onRetry?: () => void;
  onCancel?: () => void;
  language?: 'tr' | 'de' | 'en';
};

const ErrorModal: React.FC<ErrorModalProps> = ({
  visible,
  errorCode,
  errorMessage,
  onRetry,
  onCancel,
  language = 'tr'
}) => {
  const getErrorMessage = () => {
    const messages = ERROR_MESSAGES[errorCode as keyof typeof ERROR_MESSAGES] || ERROR_MESSAGES.DEFAULT;
    return errorMessage || messages[language];
  };

  const getErrorIcon = () => {
    switch (errorCode) {
      case 'CARD_DECLINED':
      case 'INSUFFICIENT_FUNDS':
        return 'card-outline';
      case 'TSE_NOT_CONNECTED':
        return 'hardware-chip-outline';
      case 'NETWORK_ERROR':
        return 'wifi-outline';
      case 'PAYMENT_TIMEOUT':
        return 'time-outline';
      default:
        return 'alert-circle-outline';
    }
  };

  return (
    <Modal visible={visible} transparent animationType="fade">
      <View style={styles.container}>
        <View style={styles.content}>
          <Ionicons name={getErrorIcon()} size={48} color="#d32f2f" style={styles.icon} />
          <Text style={styles.title}>Ödeme Hatası</Text>
          <Text style={styles.message}>{getErrorMessage()}</Text>
          <View style={styles.buttonRow}>
            {onRetry && (
              <TouchableOpacity style={styles.retryButton} onPress={onRetry}>
                <Text style={styles.buttonText}>Tekrar Dene</Text>
              </TouchableOpacity>
            )}
            <TouchableOpacity style={styles.cancelButton} onPress={onCancel}>
              <Text style={styles.buttonText}>İptal</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: 'rgba(0,0,0,0.5)'
  },
  content: {
    backgroundColor: '#fff',
    borderRadius: 12,
    padding: 20,
    alignItems: 'center',
    maxWidth: 300,
    elevation: 5
  },
  icon: {
    marginBottom: 12
  },
  title: {
    fontSize: 18,
    fontWeight: 'bold',
    color: '#d32f2f',
    marginBottom: 8
  },
  message: {
    fontSize: 14,
    textAlign: 'center',
    color: '#333',
    marginBottom: 16,
    lineHeight: 20
  },
  buttonRow: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    width: '100%'
  },
  retryButton: {
    backgroundColor: '#1976d2',
    borderRadius: 8,
    paddingHorizontal: 16,
    paddingVertical: 8,
    marginRight: 8
  },
  cancelButton: {
    backgroundColor: '#757575',
    borderRadius: 8,
    paddingHorizontal: 16,
    paddingVertical: 8
  },
  buttonText: {
    color: '#fff',
    fontWeight: 'bold',
    fontSize: 14
  }
});

export default ErrorModal; 