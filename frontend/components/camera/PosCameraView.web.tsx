import React from 'react';
import { View } from 'react-native';

import type { PosCameraViewProps } from './posCameraViewTypes';

export type { PosCameraViewProps } from './posCameraViewTypes';

/** Web stub — expo-camera is not loaded on web. */
export function PosCameraView({ style, children }: PosCameraViewProps) {
  return <View style={style}>{children}</View>;
}
