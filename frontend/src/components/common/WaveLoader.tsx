import { memo, useEffect, useMemo } from 'react';
import { StyleSheet, useColorScheme, View, type ViewProps } from 'react-native';
import Animated, {
  Easing,
  useAnimatedStyle,
  useSharedValue,
  withDelay,
  withRepeat,
  withSequence,
  withTiming,
} from 'react-native-reanimated';

import { Colors } from '../../../constants/Colors';

const DEFAULT_SIZE = 30;
const HALF_CYCLE_MS = 500;
const STAGGER_MS = 100;

/** Bar width as a fraction of height (pill bars). */
const BAR_WIDTH_RATIO = 8 / 32;
/** Gap between bars as a fraction of height. */
const GAP_RATIO = 4 / 32;

const INDICES = [0, 1, 2, 3, 4, 5, 6] as const;

const EASE_IN_OUT = Easing.inOut(Easing.ease);

export type WaveLoaderProps = {
  /** Total bar height; width and spacing scale proportionally. */
  size?: number;
  /** Bar fill color; defaults to theme primary (POS accent). */
  color?: string;
} & Pick<ViewProps, 'style' | 'testID'>;

type WaveLoaderBarProps = {
  index: number;
  color: string;
  barWidth: number;
  barHeight: number;
};

/**
 * One vertical bar: infinite scaleY pulse on the UI thread (no React re-renders).
 */
const WaveLoaderBar = memo(function WaveLoaderBar({
  index,
  color,
  barWidth,
  barHeight,
}: WaveLoaderBarProps) {
  const scaleY = useSharedValue(0.5);

  useEffect(() => {
    const delayMs = index * STAGGER_MS;

    scaleY.value = withDelay(
      delayMs,
      withRepeat(
        withSequence(
          withTiming(1.5, { duration: HALF_CYCLE_MS, easing: EASE_IN_OUT }),
          withTiming(0.5, { duration: HALF_CYCLE_MS, easing: EASE_IN_OUT }),
        ),
        -1,
        false,
      ),
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps -- mount-only; shared value ref is stable
  }, [index]);

  const animatedStyle = useAnimatedStyle(() => ({
    transform: [{ scaleY: scaleY.value }],
  }));

  return (
    <Animated.View
      style={[
        styles.bar,
        {
          width: barWidth,
          height: barHeight,
          borderRadius: barWidth / 2,
          backgroundColor: color,
        },
        animatedStyle,
      ]}
      accessibilityElementsHidden
      importantForAccessibility="no-hide-descendants"
    />
  );
});

/**
 * Global seven-bar wave loader for buttons, overlays, and modals.
 * Uses Reanimated shared values so animation stays on the UI thread.
 */
export function WaveLoader({
  size = DEFAULT_SIZE,
  color: colorProp,
  style,
  testID,
}: WaveLoaderProps) {
  const colorScheme = useColorScheme();
  const themePrimary = Colors[colorScheme === 'dark' ? 'dark' : 'light'].primary;
  const color = colorProp ?? themePrimary;

  const { barHeight, barWidth, gap } = useMemo(() => {
    const barHeight = size;
    const barWidth = Math.max(4, Math.round(size * BAR_WIDTH_RATIO));
    const gap = Math.max(2, Math.round(size * GAP_RATIO));
    return { barHeight, barWidth, gap };
  }, [size]);

  return (
    <View
      style={[styles.container, { gap }, style]}
      accessibilityRole="progressbar"
      accessibilityLabel="Lädt"
      testID={testID}
    >
      {INDICES.map((i) => (
        <WaveLoaderBar
          key={i}
          index={i}
          color={color}
          barWidth={barWidth}
          barHeight={barHeight}
        />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
  },
  bar: {
    alignSelf: 'center',
  },
});
