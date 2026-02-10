import React, { useEffect, useRef } from 'react';
import { View, Text, TouchableOpacity, Animated, StyleSheet } from 'react-native';

import { useTheme } from '../../contexts/ThemeContext';

export interface NotificationToastProps {
  visible: boolean;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message: string;
  duration?: number;
  onClose: () => void;
  onPress?: () => void;
}

export const NotificationToast: React.FC<NotificationToastProps> = ({
  visible,
  type,
  title,
  message,
  duration = 5000,
  onClose,
  onPress
}) => {
  const { theme } = useTheme();
  const translateY = useRef(new Animated.Value(-100)).current;
  const opacity = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    if (visible) {
      // Show animation
      Animated.parallel([
        Animated.timing(translateY, {
          toValue: 0,
          duration: 300,
          useNativeDriver: true,
        }),
        Animated.timing(opacity, {
          toValue: 1,
          duration: 300,
          useNativeDriver: true,
        }),
      ]).start();

      // Auto hide after duration
      const timer = setTimeout(() => {
        hideToast();
      }, duration);

      return () => clearTimeout(timer);
    } else {
      hideToast();
    }
  }, [visible, duration]);

  const hideToast = () => {
    Animated.parallel([
      Animated.timing(translateY, {
        toValue: -100,
        duration: 300,
        useNativeDriver: true,
      }),
      Animated.timing(opacity, {
        toValue: 0,
        duration: 300,
        useNativeDriver: true,
      }),
    ]).start(() => {
      onClose();
    });
  };

  const getTypeStyles = () => {
    switch (type) {
      case 'success':
        return {
          backgroundColor: theme.success,
          icon: '✓',
        };
      case 'error':
        return {
          backgroundColor: theme.error,
          icon: '✕',
        };
      case 'warning':
        return {
          backgroundColor: theme.warning,
          icon: '⚠',
        };
      case 'info':
        return {
          backgroundColor: theme.info,
          icon: 'ℹ',
        };
      default:
        return {
          backgroundColor: theme.primary,
          icon: 'ℹ',
        };
    }
  };

  const typeStyles = getTypeStyles();

  if (!visible) return null;

  return (
    <Animated.View
      style={[
        styles.container,
        {
          transform: [{ translateY }],
          opacity,
        },
      ]}
    >
      <TouchableOpacity
        style={[
          styles.toast,
          { backgroundColor: typeStyles.backgroundColor },
        ]}
        onPress={onPress}
        activeOpacity={onPress ? 0.8 : 1}
      >
        <View style={styles.content}>
          <View style={styles.header}>
            <Text style={styles.icon}>{typeStyles.icon}</Text>
            <Text style={styles.title}>{title}</Text>
            <TouchableOpacity onPress={hideToast} style={styles.closeButton}>
              <Text style={styles.closeText}>✕</Text>
            </TouchableOpacity>
          </View>
          {message && (
            <Text style={styles.message}>{message}</Text>
          )}
        </View>
      </TouchableOpacity>
    </Animated.View>
  );
};

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    top: 50,
    left: 16,
    right: 16,
    zIndex: 1000,
  },
  toast: {
    borderRadius: 12,
    padding: 16,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 2,
    },
    shadowOpacity: 0.25,
    shadowRadius: 3.84,
    elevation: 5,
  },
  content: {
    flex: 1,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 4,
  },
  icon: {
    fontSize: 18,
    marginRight: 8,
    color: 'white',
  },
  title: {
    flex: 1,
    fontSize: 16,
    fontWeight: '600',
    color: 'white',
  },
  closeButton: {
    padding: 4,
  },
  closeText: {
    fontSize: 16,
    color: 'white',
    fontWeight: 'bold',
  },
  message: {
    fontSize: 14,
    color: 'white',
    lineHeight: 20,
    marginLeft: 26,
  },
}); 