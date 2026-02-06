// Soft minimal list product card component (for cart items)
import React from 'react';
import { StyleSheet, View, Text, Image, Pressable } from 'react-native';
import { SoftColors, SoftSpacing, SoftRadius, SoftTypography, SoftShadows } from '../../constants/SoftTheme';
import { SoftPriceBadge } from './SoftPriceBadge';

interface ProductCardListProps {
    name: string;
    price: number;
    description?: string;
    imageUrl?: string;
    quantity?: number;
    onPress?: () => void;
    onIncrement?: () => void;
    onDecrement?: () => void;
    onRemove?: () => void;
}

export function SoftProductCardList({
    name,
    price,
    description,
    imageUrl,
    quantity,
    onPress,
    onIncrement,
    onDecrement,
    onRemove
}: ProductCardListProps) {
    return (
        <Pressable
            onPress={onPress}
            style={({ pressed }) => [styles.card, pressed && styles.pressed]}
        >
            {/* Thumbnail */}
            <View style={styles.thumbnail}>
                {imageUrl ? (
                    <Image source={{ uri: imageUrl }} style={styles.image} />
                ) : (
                    <View style={styles.imagePlaceholder}>
                        <Text style={styles.placeholderEmoji}>☕</Text>
                    </View>
                )}
            </View>

            {/* Info */}
            <View style={styles.info}>
                <Text style={styles.name} numberOfLines={1}>{name}</Text>
                {description && (
                    <Text style={styles.description} numberOfLines={1}>{description}</Text>
                )}
                <SoftPriceBadge price={price * (quantity || 1)} size="sm" />
            </View>

            {/* Quantity Controls */}
            {quantity !== undefined && (
                <View style={styles.quantityWrapper}>
                    <Pressable
                        style={styles.qtyButton}
                        onPress={quantity <= 1 ? onRemove : onDecrement}
                    >
                        <Text style={styles.qtyButtonText}>{quantity <= 1 ? '×' : '−'}</Text>
                    </Pressable>
                    <Text style={styles.qtyText}>{quantity}</Text>
                    <Pressable style={styles.qtyButton} onPress={onIncrement}>
                        <Text style={styles.qtyButtonText}>+</Text>
                    </Pressable>
                </View>
            )}
        </Pressable>
    );
}

const styles = StyleSheet.create({
    card: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: SoftColors.bgCard,
        borderRadius: SoftRadius.lg,
        padding: SoftSpacing.md,
        marginBottom: SoftSpacing.sm,
        ...SoftShadows.sm,
    },
    pressed: {
        opacity: 0.9,
    },
    thumbnail: {
        width: 64,
        height: 64,
        borderRadius: SoftRadius.md,
        backgroundColor: SoftColors.bgSecondary,
        overflow: 'hidden',
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
        fontSize: 24,
    },
    info: {
        flex: 1,
        marginLeft: SoftSpacing.md,
        gap: SoftSpacing.xs,
    },
    name: {
        ...SoftTypography.body,
        fontWeight: '600',
        color: SoftColors.textPrimary,
    },
    description: {
        ...SoftTypography.caption,
        color: SoftColors.textMuted,
    },
    quantityWrapper: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: SoftColors.bgSecondary,
        borderRadius: SoftRadius.full,
        padding: SoftSpacing.xs,
    },
    qtyButton: {
        width: 32,
        height: 32,
        borderRadius: SoftRadius.full,
        backgroundColor: SoftColors.bgCard,
        justifyContent: 'center',
        alignItems: 'center',
        ...SoftShadows.sm,
    },
    qtyButtonText: {
        ...SoftTypography.h3,
        color: SoftColors.accent,
    },
    qtyText: {
        ...SoftTypography.label,
        color: SoftColors.textPrimary,
        paddingHorizontal: SoftSpacing.md,
        minWidth: 32,
        textAlign: 'center',
    },
});
