// Soft minimal grid product card component
import React from 'react';
import { StyleSheet, View, Text, Image, Pressable } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography, SoftShadows } from '../../constants/SoftTheme';
import { SoftPriceBadge } from './SoftPriceBadge';

interface ProductCardGridProps {
    name: string;
    price: number;
    imageUrl?: string;
    category?: string;
    inStock?: boolean;
    onPress?: () => void;
}

export function SoftProductCardGrid({
    name,
    price,
    imageUrl,
    category,
    inStock = true,
    onPress
}: ProductCardGridProps) {
    return (
        <Pressable
            onPress={onPress}
            style={({ pressed }) => [
                styles.card,
                pressed && styles.pressed,
                !inStock && styles.outOfStock
            ]}
            disabled={!inStock}
        >
            {/* Image Container */}
            <View style={styles.imageWrapper}>
                {imageUrl ? (
                    <Image source={{ uri: imageUrl }} style={styles.image} />
                ) : (
                    <View style={styles.imagePlaceholder}>
                        <Text style={styles.placeholderEmoji}>☕</Text>
                    </View>
                )}
                {!inStock && (
                    <View style={styles.outOfStockBadge}>
                        <Text style={styles.outOfStockText}>Out of Stock</Text>
                    </View>
                )}
            </View>

            {/* Content */}
            <View style={styles.content}>
                {category && <Text style={styles.category}>{category}</Text>}
                <Text style={styles.name} numberOfLines={2}>{name}</Text>
                <SoftPriceBadge price={price} size="sm" />
            </View>
        </Pressable>
    );
}

const styles = StyleSheet.create({
    card: {
        width: '48%',
        backgroundColor: SoftColors.bgCard,
        borderRadius: SoftRadius.xl,
        overflow: 'hidden',
        marginBottom: SoftSpacing.lg,
        ...SoftShadows.sm,
    },
    pressed: {
        opacity: 0.9,
        transform: [{ scale: 0.98 }],
    },
    outOfStock: {
        opacity: 0.6,
    },
    imageWrapper: {
        aspectRatio: 1,
        backgroundColor: SoftColors.bgSecondary,
        position: 'relative',
    },
    image: {
        width: '100%',
        height: '100%',
        resizeMode: 'cover',
    },
    imagePlaceholder: {
        flex: 1,
        justifyContent: 'center',
        alignItems: 'center',
    },
    placeholderEmoji: {
        fontSize: 40,
    },
    outOfStockBadge: {
        position: 'absolute',
        bottom: SoftSpacing.sm,
        left: SoftSpacing.sm,
        backgroundColor: SoftColors.overlay,
        paddingHorizontal: SoftSpacing.sm,
        paddingVertical: SoftSpacing.xs,
        borderRadius: SoftRadius.sm,
    },
    outOfStockText: {
        ...SoftTypography.caption,
        color: SoftColors.textInverse,
    },
    content: {
        padding: SoftSpacing.md,
        gap: SoftSpacing.xs,
    },
    category: {
        ...SoftTypography.caption,
        color: SoftColors.textMuted,
        textTransform: 'uppercase',
    },
    name: {
        ...SoftTypography.body,
        fontWeight: '600',
        color: SoftColors.textPrimary,
    },
});
