import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { View, Text, TouchableOpacity, StyleSheet } from 'react-native';

import { Colors, Spacing, Typography, BorderRadius } from '../../constants/Colors';

interface ErrorMessageProps {
  message: string;
  onRetry?: () => void;
  onDismiss?: () => void;
  type?: 'error' | 'warning' | 'info';
}

const ErrorMessage: React.FC<ErrorMessageProps> = ({
  message,
  onRetry,
  onDismiss,
  type = 'error',
}) => {
  const getIconName = () => {
    switch (type) {
      case 'error':
        return 'alert-circle';
      case 'warning':
        return 'warning';
      case 'info':
        return 'information-circle';
      default:
        return 'alert-circle';
    }
  };

  const getBackgroundColor = () => {
    switch (type) {
      case 'error':
        return Colors.light.error;
      case 'warning':
        return Colors.light.warning;
      case 'info':
        return Colors.light.info;
      default:
        return Colors.light.error;
    }
  };

  const getTextColor = () => {
    switch (type) {
      case 'error':
        return '#fff';
      case 'warning':
        return '#000';
      case 'info':
        return '#fff';
      default:
        return '#fff';
    }
  };

  return (
    <View style={[styles.container, { backgroundColor: getBackgroundColor() }]}>
      <View style={styles.content}>
        <Ionicons 
          name={getIconName() as any} 
          size={20} 
          color={getTextColor()} 
        />
        <Text style={[styles.message, { color: getTextColor() }]}>
          {message}
        </Text>
      </View>
      
      <View style={styles.actions}>
        {onRetry && (
          <TouchableOpacity style={styles.button} onPress={onRetry}>
            <Text style={[styles.buttonText, { color: getTextColor() }]}>
              Tekrar Dene
            </Text>
          </TouchableOpacity>
        )}
        
        {onDismiss && (
          <TouchableOpacity style={styles.button} onPress={onDismiss}>
            <Ionicons 
              name="close" 
              size={16} 
              color={getTextColor()} 
            />
          </TouchableOpacity>
        )}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    margin: Spacing.sm,
    padding: Spacing.sm,
    borderRadius: BorderRadius.sm,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  content: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
  },
  message: {
    ...Typography.caption,
    marginLeft: Spacing.xs,
    flex: 1,
  },
  actions: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  button: {
    padding: Spacing.xs,
    marginLeft: Spacing.xs,
  },
  buttonText: {
    ...Typography.caption,
    fontWeight: '600',
  },
});

export default ErrorMessage; 