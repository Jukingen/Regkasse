// POS table tile — touch-friendly card; selection uses border + fill, not focus ring.
import React from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  Pressable,
  StyleSheet,
  Platform,
  type ViewStyle,
} from 'react-native';

import { SoftColors, SoftShadows, SoftSpacing, SoftRadius, SoftState, SoftTypography } from '../constants/SoftTheme';
import { WaveLoader } from '../src/components/common/WaveLoader';

/** Web-only: no default browser focus outline on these tiles (semantic styles show selection). */
export const webTablePressableOutlineOff: ViewStyle =
  Platform.OS === 'web'
    ? ({ outlineStyle: 'none', outlineWidth: 0 } as unknown as ViewStyle)
    : {};

export interface TableSelectorTileProps {
  tableNumber: number;
  itemCount: number;
  isSelected: boolean;
  isLoading: boolean;
  onPress: () => void;
}

export function TableSelectorTile({
  tableNumber,
  itemCount,
  isSelected,
  isLoading,
  onPress,
}: TableSelectorTileProps) {
  const { t } = useTranslation(['checkout']);
  const hasItems = itemCount > 0;
  const statusLabel = hasItems
    ? t('checkout:posFlow.tableSelector.statusOpen')
    : t('checkout:posFlow.tableSelector.statusFree');

  const accessibilityLabel = isSelected
    ? t('checkout:posFlow.tableSelector.a11ySelected', { number: tableNumber })
    : t('checkout:posFlow.tableSelector.a11ySelect', { number: tableNumber });

  return (
    <Pressable
      style={({ pressed }) => [
        styles.tile,
        webTablePressableOutlineOff,
        !isSelected && !hasItems && styles.tileFree,
        !isSelected && hasItems && styles.tileOccupied,
        isSelected && styles.tileSelected,
        isLoading && styles.tileDisabled,
        pressed && !isLoading && styles.tilePressed,
      ]}
      onPress={onPress}
      disabled={isLoading}
      hitSlop={{ top: 12, bottom: 12, left: 12, right: 12 }}
      accessibilityLabel={accessibilityLabel}
      accessibilityRole="button"
      accessibilityState={{ selected: isSelected, disabled: isLoading }}
    >
      <View style={styles.inner} pointerEvents="none">
        <Text
          style={[
            styles.number,
            !isSelected && !hasItems && styles.numberFree,
            !isSelected && hasItems && styles.numberOccupied,
            isSelected && styles.numberSelected,
          ]}
          maxFontSizeMultiplier={1.4}
        >
          {tableNumber}
        </Text>
        <Text
          style={[
            styles.status,
            !isSelected && !hasItems && styles.statusFree,
            !isSelected && hasItems && styles.statusOccupied,
            isSelected && hasItems && styles.statusSelectedOccupied,
            isSelected && !hasItems && styles.statusSelectedFree,
          ]}
          numberOfLines={1}
          maxFontSizeMultiplier={1.2}
        >
          {statusLabel}
        </Text>

        {isLoading && (
          <View style={styles.loadingOverlay}>
            <WaveLoader
              size={18}
              color={isSelected ? SoftColors.accentDark : SoftColors.accent}
            />
          </View>
        )}

        {hasItems && !isLoading && (
          <View style={styles.countBadge}>
            <Text style={styles.countBadgeText}>{itemCount}</Text>
          </View>
        )}
      </View>
    </Pressable>
  );
}

const TILE_W = 88;
const TILE_H = 92;

const styles = StyleSheet.create({
  tile: {
    width: TILE_W,
    minHeight: TILE_H,
    borderRadius: SoftRadius.lg,
    marginRight: SoftSpacing.sm,
    justifyContent: 'center',
    ...SoftShadows.sm,
  },
  tileFree: {
    backgroundColor: SoftColors.bgCard,
    borderWidth: 1,
    borderColor: SoftColors.border,
  },
  tileOccupied: {
    backgroundColor: SoftColors.successBg,
    borderWidth: 2,
    borderColor: SoftColors.success,
    ...SoftShadows.md,
  },
  /** Selected: subtle fill + strong border (not focus-outline). */
  tileSelected: {
    backgroundColor: SoftColors.accentLight,
    borderWidth: 3,
    borderColor: SoftColors.accentDark,
    ...SoftShadows.md,
  },
  tileDisabled: {
    opacity: 0.82,
  },
  tilePressed: SoftState.pressedScale,
  inner: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingVertical: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.xs,
    position: 'relative',
    minHeight: TILE_H - 4,
  },
  number: {
    fontSize: 22,
    fontWeight: '800',
    letterSpacing: -0.5,
    lineHeight: 28,
  },
  numberFree: {
    color: SoftColors.textPrimary,
  },
  numberOccupied: {
    color: SoftColors.success,
  },
  numberSelected: {
    color: SoftColors.accentDark,
  },
  status: {
    ...SoftTypography.caption,
    marginTop: 2,
    fontWeight: '600',
    letterSpacing: 0.2,
  },
  statusFree: {
    color: SoftColors.textMuted,
  },
  statusOccupied: {
    color: SoftColors.success,
  },
  statusSelectedFree: {
    color: SoftColors.accentDark,
    opacity: 0.85,
  },
  statusSelectedOccupied: {
    color: SoftColors.success,
  },
  loadingOverlay: {
    ...StyleSheet.absoluteFillObject,
    justifyContent: 'center',
    alignItems: 'center',
    borderRadius: SoftRadius.lg,
    backgroundColor: 'rgba(255,255,255,0.35)',
  },
  countBadge: {
    position: 'absolute',
    top: 4,
    right: 4,
    backgroundColor: SoftColors.warning,
    borderRadius: SoftRadius.full,
    minWidth: 22,
    height: 22,
    paddingHorizontal: 5,
    alignItems: 'center',
    justifyContent: 'center',
    ...SoftShadows.sm,
  },
  countBadgeText: {
    ...SoftTypography.caption,
    fontSize: 11,
    fontWeight: '800',
    color: SoftColors.textInverse,
  },
});
