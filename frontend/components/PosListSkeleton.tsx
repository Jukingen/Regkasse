import React, { useEffect, useRef } from 'react';
import { Animated, StyleSheet, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';

type PosListSkeletonProps = {
  rows?: number;
  testID?: string;
};

/** Pulsing row placeholders for POS lists while the first page is loading. */
export function PosListSkeleton({ rows = 6, testID = 'pos-list-skeleton' }: PosListSkeletonProps) {
  const opacity = useRef(new Animated.Value(0.45)).current;

  useEffect(() => {
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(opacity, { toValue: 1, duration: 700, useNativeDriver: true }),
        Animated.timing(opacity, { toValue: 0.45, duration: 700, useNativeDriver: true }),
      ])
    );
    loop.start();
    return () => loop.stop();
  }, [opacity]);

  return (
    <View testID={testID} accessibilityRole="progressbar" style={styles.wrap}>
      {Array.from({ length: rows }).map((_, index) => (
        <Animated.View key={index} style={[styles.row, { opacity }]} />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: {
    paddingVertical: SoftSpacing.sm,
    gap: SoftSpacing.sm,
  },
  row: {
    height: 56,
    borderRadius: SoftRadius.sm,
    backgroundColor: SoftColors.bgSecondary,
  },
});
