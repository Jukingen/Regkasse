// Türkçe Açıklama: E-posta ile fatura gönderimi için kullanıcı arayüzü. Müşteri e-posta adresi alınır ve backend'e gönderilir.

import React, { useState } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet, Modal, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

type EmailInvoiceProps = {
  visible: boolean;
  invoiceData: {
    id: string;
    totalAmount: number;
    receiptNumber: string;
  };
  onSend: (email: string) => Promise<boolean>;
  onClose: () => void;
};

const EmailInvoice: React.FC<EmailInvoiceProps> = ({
  visible,
  invoiceData,
  onSend,
  onClose
}) => {
  const [email, setEmail] = useState('');
  const [sending, setSending] = useState(false);

  const handleSend = async () => {
    if (!email || !email.includes('@')) {
      Alert.alert('Hata', 'Lütfen geçerli bir e-posta adresi girin.');
      return;
    }

    setSending(true);
    try {
      const success = await onSend(email);
      if (success) {
        Alert.alert('Başarılı', 'Fatura e-posta ile gönderildi.');
        onClose();
      } else {
        Alert.alert('Hata', 'E-posta gönderilemedi. Lütfen tekrar deneyin.');
      }
    } catch (error) {
      Alert.alert('Hata', 'Bir sorun oluştu.');
    } finally {
      setSending(false);
    }
  };

  return (
    <Modal visible={visible} transparent animationType="slide">
      <View style={styles.container}>
        <View style={styles.content}>
          <Ionicons name="mail" size={48} color="#1976d2" style={styles.icon} />
          <Text style={styles.title}>E-posta ile Fatura Gönder</Text>
          <Text style={styles.subtitle}>Fatura #{invoiceData.receiptNumber}</Text>
          <Text style={styles.amount}>{invoiceData.totalAmount.toFixed(2)} €</Text>
          
          <TextInput
            style={styles.emailInput}
            placeholder="müşteri@email.com"
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
            autoCorrect={false}
          />
          
          <View style={styles.buttonRow}>
            <TouchableOpacity style={styles.cancelButton} onPress={onClose}>
              <Text style={styles.buttonText}>İptal</Text>
            </TouchableOpacity>
            <TouchableOpacity 
              style={[styles.sendButton, { opacity: sending ? 0.6 : 1 }]} 
              onPress={handleSend}
              disabled={sending}
            >
              <Text style={styles.buttonText}>
                {sending ? 'Gönderiliyor...' : 'Gönder'}
              </Text>
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
    width: '90%',
    maxWidth: 350
  },
  icon: {
    marginBottom: 12
  },
  title: {
    fontSize: 18,
    fontWeight: 'bold',
    marginBottom: 4
  },
  subtitle: {
    fontSize: 14,
    color: '#666',
    marginBottom: 8
  },
  amount: {
    fontSize: 16,
    fontWeight: 'bold',
    color: '#27ae60',
    marginBottom: 20
  },
  emailInput: {
    width: '100%',
    borderWidth: 1,
    borderColor: '#ccc',
    borderRadius: 8,
    padding: 12,
    fontSize: 16,
    marginBottom: 20
  },
  buttonRow: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    width: '100%'
  },
  cancelButton: {
    backgroundColor: '#757575',
    borderRadius: 8,
    paddingHorizontal: 20,
    paddingVertical: 12,
    flex: 1,
    marginRight: 8
  },
  sendButton: {
    backgroundColor: '#27ae60',
    borderRadius: 8,
    paddingHorizontal: 20,
    paddingVertical: 12,
    flex: 1,
    marginLeft: 8
  },
  buttonText: {
    color: '#fff',
    fontWeight: 'bold',
    fontSize: 16,
    textAlign: 'center'
  }
});

export default EmailInvoice; 