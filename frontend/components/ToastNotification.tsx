import React, { useEffect } from 'react';
import { View, Text, StyleSheet, Animated, Pressable } from 'react-native';
import { SoftColors, SoftMotion, SoftRadius, SoftShadows, SoftSpacing, SoftState, SoftTypography, Space8 } from '../constants/SoftTheme';

// Toast notification interface
interface ToastNotification {
  id: string;
  type: 'success' | 'error' | 'info' | 'warning';
  message: string;
  duration?: number;
}

// Toast notification props
interface ToastNotificationProps {
  toast: ToastNotification;
  onRemove: (id: string) => void;
}

// Toast notification component
export const ToastNotification: React.FC<ToastNotificationProps> = ({ toast, onRemove }) => {
  const fadeAnim = new Animated.Value(0);
  const slideAnim = new Animated.Value(-100);

  useEffect(() => {
    // Animate in (micro duration for snappy feel)
    Animated.parallel([
      Animated.timing(fadeAnim, {
        toValue: 1,
        duration: SoftMotion.micro,
        useNativeDriver: true,
      }),
      Animated.timing(slideAnim, {
        toValue: 0,
        duration: SoftMotion.micro,
        useNativeDriver: true,
      }),
    ]).start();

    // Auto remove after duration
    const timer = setTimeout(() => {
      animateOut();
    }, toast.duration || 5000);

    return () => clearTimeout(timer);
  }, []);

  const animateOut = () => {
    Animated.parallel([
      Animated.timing(fadeAnim, {
        toValue: 0,
        duration: SoftMotion.micro,
        useNativeDriver: true,
      }),
      Animated.timing(slideAnim, {
        toValue: -100,
        duration: SoftMotion.micro,
        useNativeDriver: true,
      }),
    ]).start(() => {
      onRemove(toast.id);
    });
  };

  const getToastStyle = () => {
    switch (toast.type) {
      case 'success':
        return styles.success;
      case 'error':
        return styles.error;
      case 'warning':
        return styles.warning;
      case 'info':
        return styles.info;
      default:
        return styles.info;
    }
  };

  const getIcon = () => {
    switch (toast.type) {
      case 'success':
        return '✅';
      case 'error':
        return '❌';
      case 'warning':
        return '⚠️';
      case 'info':
        return 'ℹ️';
      default:
        return 'ℹ️';
    }
  };

  return (
    <Animated.View
      style={[
        styles.container,
        getToastStyle(),
        {
          opacity: fadeAnim,
          transform: [{ translateY: slideAnim }],
        },
      ]}
      accessibilityRole="alert"
      accessibilityLabel={toast.message}
    >
      <View style={styles.content} accessibilityElementsHidden>
        <Text style={styles.icon}>{getIcon()}</Text>
        <Text style={styles.message}>{toast.message}</Text>
      </View>
      <Pressable
        onPress={animateOut}
        style={({ pressed, focused }) => [styles.closeButton, pressed && SoftState.pressed, focused && SoftState.focusVisible]}
        hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
        accessibilityLabel="Nachricht schließen"
        accessibilityRole="button"
      >
        <Text style={styles.closeText}>×</Text>
      </Pressable>
    </Animated.View>
  );
};

// Toast container component
interface ToastContainerProps {
  toasts: ToastNotification[];
  onRemove: (id: string) => void;
}

export const ToastContainer: React.FC<ToastContainerProps> = ({ toasts, onRemove }) => {
  return (
    <View style={styles.containerWrapper}>
      {toasts.map((toast) => (
        <ToastNotification
          key={toast.id}
          toast={toast}
          onRemove={onRemove}
        />
      ))}
    </View>
  );
};

// Styles – SoftTheme tokens
const styles = StyleSheet.create({
  containerWrapper: {
    position: 'absolute',
    top: Space8[6],
    left: SoftSpacing.md,
    right: SoftSpacing.md,
    zIndex: 1000,
  },
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    borderRadius: SoftRadius.md,
    marginBottom: SoftSpacing.sm,
    ...SoftShadows.md,
  },
  content: {
    flex: 1,
    flexDirection: 'row',
    alignItems: 'center',
  },
  icon: {
    fontSize: 18,
    marginRight: SoftSpacing.sm,
  },
  message: {
    flex: 1,
    ...SoftTypography.body,
    fontWeight: '500',
    color: SoftColors.textInverse,
  },
  closeButton: {
    marginLeft: SoftSpacing.sm,
    padding: SoftSpacing.xs,
    minWidth: 44,
    minHeight: 44,
    alignItems: 'center',
    justifyContent: 'center',
  },
  closeText: {
    ...SoftTypography.h3,
    color: SoftColors.textInverse,
  },
  success: {
    backgroundColor: SoftColors.success,
  },
  error: {
    backgroundColor: SoftColors.error,
  },
  warning: {
    backgroundColor: SoftColors.warning,
  },
  info: {
    backgroundColor: SoftColors.info,
  },
});
