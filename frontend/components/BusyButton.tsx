import React from 'react';
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  View,
  type PressableProps,
  type StyleProp,
  type TextStyle,
  type ViewStyle,
} from 'react-native';

import { SoftColors, SoftSpacing } from '../constants/SoftTheme';

type BusyButtonProps = Omit<PressableProps, 'style' | 'disabled'> & {
  busy: boolean;
  label: string;
  disabled?: boolean;
  spinnerColor?: string;
  style?: StyleProp<ViewStyle>;
  textStyle?: StyleProp<TextStyle>;
};

/** Disables the control and shows a spinner while a save or submit request is in flight. */
export function BusyButton({
  busy,
  label,
  disabled = false,
  spinnerColor = SoftColors.textInverse,
  style,
  textStyle,
  accessibilityState,
  ...rest
}: BusyButtonProps) {
  const blocked = busy || disabled;
  return (
    <Pressable
      accessibilityRole="button"
      {...rest}
      disabled={blocked}
      accessibilityState={{ ...accessibilityState, disabled: blocked, busy }}
      style={style}
    >
      <View style={styles.row}>
        {busy ? (
          <ActivityIndicator
            size="small"
            color={spinnerColor}
            testID="busy-button-spinner"
          />
        ) : null}
        <Text style={textStyle}>{label}</Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: SoftSpacing.sm,
  },
});
