// Soft minimal category chip component
import React from 'react';
import { StyleSheet, View, Text, Pressable } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography } from '../../constants/SoftTheme';

interface ChipProps {
    label: string;
    selected?: boolean;
    onPress?: () => void;
}

export function SoftChip({ label, selected, onPress }: ChipProps) {
    return (
        <Pressable onPress={onPress}>
            <View style={[styles.chip, selected && styles.chipSelected]}>
                <Text style={[styles.chipText, selected && styles.chipTextSelected]}>
                    {label}
                </Text>
            </View>
        </Pressable>
    );
}

const styles = StyleSheet.create({
    chip: {
        paddingHorizontal: SoftSpacing.lg,
        paddingVertical: SoftSpacing.sm,
        borderRadius: SoftRadius.full,
        backgroundColor: SoftColors.bgSecondary,
        marginRight: SoftSpacing.sm,
    },
    chipSelected: {
        backgroundColor: SoftColors.accent,
    },
    chipText: {
        ...SoftTypography.label,
        color: SoftColors.textSecondary,
    },
    chipTextSelected: {
        color: SoftColors.textInverse,
    },
});
