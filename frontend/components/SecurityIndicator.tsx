import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

interface SecurityIndicatorProps {
  isSecure: boolean;
  message?: string;
}

export const SecurityIndicator: React.FC<SecurityIndicatorProps> = ({
  isSecure,
  message
}) => {
  return (
    <View style={styles.container}>
      <View style={[styles.indicator, isSecure ? styles.secure : styles.insecure]}>
        <Ionicons 
          name={isSecure ? "shield-checkmark" : "warning"} 
          size={16} 
          color={isSecure ? "#34C759" : "#FF9500"} 
        />
      </View>
      <Text style={[styles.text, isSecure ? styles.secureText : styles.insecureText]}>
        {message || (isSecure ? 'Güvenli Bağlantı' : 'Güvenlik Uyarısı')}
      </Text>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 8,
    paddingHorizontal: 12,
    backgroundColor: '#f8f9fa',
    borderRadius: 8,
    marginBottom: 16,
  },
  indicator: {
    width: 24,
    height: 24,
    borderRadius: 12,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 8,
  },
  secure: {
    backgroundColor: '#d4edda',
  },
  insecure: {
    backgroundColor: '#fff3cd',
  },
  text: {
    fontSize: 14,
    fontWeight: '500',
  },
  secureText: {
    color: '#155724',
  },
  insecureText: {
    color: '#856404',
  },
});
