import React from 'react';
import { View, Text, StyleSheet } from 'react-native';

import { useTheme } from '../../contexts/ThemeContext';
import { WaveLoader } from '../../src/components/common/WaveLoader';

interface LoadingOverlayProps {
  visible: boolean;
  message?: string;
  size?: 'small' | 'large';
  color?: string;
  backgroundColor?: string;
}

export const LoadingOverlay: React.FC<LoadingOverlayProps> = ({
  visible,
  message = 'Bitte warten…',
  size = 'large',
  color,
  backgroundColor,
}) => {
  const { theme } = useTheme();

  if (!visible) return null;

  const waveSize = size === 'small' ? 22 : 32;

  return (
    <View
      style={[styles.container, { backgroundColor: backgroundColor || theme.background + 'CC' }]}>
      <View style={styles.content}>
        <WaveLoader size={waveSize} color={color || theme.primary} />
        {message ? <Text style={[styles.message, { color: theme.text }]}>{message}</Text> : null}
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    justifyContent: 'center',
    alignItems: 'center',
    zIndex: 1000,
  },
  content: {
    backgroundColor: 'rgba(0, 0, 0, 0.8)',
    borderRadius: 12,
    padding: 24,
    alignItems: 'center',
    justifyContent: 'center',
    minWidth: 120,
  },
  message: {
    marginTop: 12,
    fontSize: 16,
    textAlign: 'center',
    fontWeight: '500',
  },
});
