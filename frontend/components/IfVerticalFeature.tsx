import React from 'react';

import { useVerticalFeatures } from '../contexts/VerticalProfileContext';

export interface IfVerticalFeatureProps {
  feature: string;
  children: React.ReactNode;
  fallback?: React.ReactNode;
}

export function IfVerticalFeature({
  feature,
  children,
  fallback = null,
}: IfVerticalFeatureProps) {
  const { posFeatures } = useVerticalFeatures();
  return <>{posFeatures[feature] ? children : fallback}</>;
}
