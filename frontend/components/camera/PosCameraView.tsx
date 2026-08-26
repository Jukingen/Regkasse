import { CameraView } from 'expo-camera';
import React from 'react';

import type { PosCameraViewProps } from './posCameraViewTypes';

export type { PosCameraViewProps } from './posCameraViewTypes';

/** Native camera view. Web bundle uses `PosCameraView.web.tsx` instead. */
export function PosCameraView(props: PosCameraViewProps) {
  return <CameraView {...(props as React.ComponentProps<typeof CameraView>)} />;
}
