// Soft minimal price badge component
import React from 'react';
import { StyleSheet, View, Text } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography } from '../../constants/SoftTheme';

interface PriceBadgeProps {
    price: number;
    currency?: string;
    size?: 'sm' | 'md';
}

export function SoftPriceBadge({ price, currency = '€', size = 'md' }: PriceBadgeProps) {
    return (
        <View style={[styles.badge, size === 'sm' && styles.badgeSm]}>
            <Text style={[styles.price, size === 'sm' && styles.priceSm]}>
                {currency}{price.toFixed(2)}
            </Text>
        </View>
    );
}

const styles = StyleSheet.create({
    badge: {
        backgroundColor: SoftColors.accentLight,
        paddingHorizontal: SoftSpacing.md,
        paddingVertical: SoftSpacing.xs,
        borderRadius: SoftRadius.sm,
        alignSelf: 'flex-start',
    },
    badgeSm: {
        paddingHorizontal: SoftSpacing.sm,
        paddingVertical: 2,
    },
    price: {
        ...SoftTypography.price,
        color: SoftColors.accentDark,
    },
    priceSm: {
        ...SoftTypography.priceSmall,
    },
});
