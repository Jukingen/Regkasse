import React from 'react';
import { View, Text, StyleSheet } from 'react-native';

import { Colors, Spacing, Typography } from '../../constants/Colors';
import { WaveLoader } from '../../src/components/common/WaveLoader';

interface LoadingSpinnerProps {
  message?: string;
  size?: 'small' | 'large';
  color?: string;
}

const WAVE_SIZE: Record<'small' | 'large', number> = {
  small: 22,
  large: 32,
};

const LoadingSpinner: React.FC<LoadingSpinnerProps> = ({
  message = 'Wird geladen…',
  size = 'large',
  color = Colors.light.primary,
}) => {
  return (
    <View style={styles.container}>
      <WaveLoader size={WAVE_SIZE[size]} color={color} />
      {message ? <Text style={styles.message}>{message}</Text> : null}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: Spacing.lg,
  },
  message: {
    ...Typography.body,
    color: Colors.light.textSecondary,
    marginTop: Spacing.sm,
    textAlign: 'center',
  },
});

export default LoadingSpinner;
