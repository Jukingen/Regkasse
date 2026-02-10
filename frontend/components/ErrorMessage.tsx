import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

interface ErrorMessageProps {
  message: string;
  type?: 'error' | 'warning' | 'info';
  showIcon?: boolean;
}

export const ErrorMessage: React.FC<ErrorMessageProps> = ({
  message,
  type = 'error',
  showIcon = true
}) => {
  const getTypeStyles = () => {
    switch (type) {
      case 'warning':
        return {
          backgroundColor: '#fff3cd',
          borderColor: '#ffeaa7',
          textColor: '#856404',
          iconColor: '#f39c12',
          iconName: 'warning-outline'
        };
      case 'info':
        return {
          backgroundColor: '#d1ecf1',
          borderColor: '#bee5eb',
          textColor: '#0c5460',
          iconColor: '#17a2b8',
          iconName: 'information-circle-outline'
        };
      default: // error
        return {
          backgroundColor: '#f8d7da',
          borderColor: '#f5c6cb',
          textColor: '#721c24',
          iconColor: '#dc3545',
          iconName: 'alert-circle-outline'
        };
    }
  };

  const styles = getTypeStyles();

  return (
    <View style={[styles.container, { backgroundColor: styles.backgroundColor, borderColor: styles.borderColor }]}>
      {showIcon && (
        <Ionicons 
          name={styles.iconName as any} 
          size={16} 
          color={styles.iconColor} 
          style={styles.icon}
        />
      )}
      <Text style={[styles.text, { color: styles.textColor }]}>
        {message}
      </Text>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 8,
    borderWidth: 1,
    marginBottom: 16,
  },
  icon: {
    marginRight: 8,
  },
  text: {
    fontSize: 14,
    fontWeight: '500',
    flex: 1,
  },
});
