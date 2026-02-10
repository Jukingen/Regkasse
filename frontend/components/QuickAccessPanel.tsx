import { Ionicons } from '@expo/vector-icons';
import React, { useState, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
  ScrollView,
  Animated,
  Vibration,
  Dimensions,
  Platform,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';



interface QuickAccessPanelProps {
  onQuickAction: (action: string) => void;
  pendingOrders?: number;
  lowStockItems?: number;
  dailySales?: number;
  isCollapsed?: boolean;
  onToggleCollapse?: () => void;
  favoriteProducts?: any[];
  onFavoriteProductPress?: (product: any) => void;
}

const { width: screenWidth } = Dimensions.get('window');

const QuickAccessPanel: React.FC<QuickAccessPanelProps> = ({
  onQuickAction,
  pendingOrders = 0,
  lowStockItems = 0,
  dailySales = 0,
  isCollapsed = false,
  onToggleCollapse,
  favoriteProducts = [],
  onFavoriteProductPress,
}) => {
  const { t } = useTranslation();
  const [activeAction, setActiveAction] = useState<string | null>(null);
  
  // Animasyon değerleri
  const collapseAnimation = useRef(new Animated.Value(isCollapsed ? 0 : 1)).current;
  const pulseAnimation = useRef(new Animated.Value(1)).current;
  const actionAnimations = useRef<{ [key: string]: Animated.Value }>({}).current;



  const handleActionPress = (actionId: string) => {
    // Haptic feedback - Kısaltıldı
    Vibration.vibrate(25); // 50ms -> 25ms
    
    // Animasyon başlat - Hızlandırıldı
    if (!actionAnimations[actionId]) {
      actionAnimations[actionId] = new Animated.Value(1);
    }
    
    Animated.sequence([
      Animated.timing(actionAnimations[actionId], {
        toValue: 0.9, // 0.8 -> 0.9 (daha az belirgin)
        duration: 50, // 100ms -> 50ms
        useNativeDriver: true,
      }),
      Animated.timing(actionAnimations[actionId], {
        toValue: 1,
        duration: 50, // 100ms -> 50ms
        useNativeDriver: true,
      }),
    ]).start();

    setActiveAction(actionId);
    onQuickAction(actionId);
    
    // 1 saniye sonra active state'i temizle - Hızlandırıldı
    setTimeout(() => setActiveAction(null), 1000); // 2000ms -> 1000ms
  };

  const handleToggleCollapse = () => {
    onToggleCollapse?.();
  };

  // Collapse animasyonu - Hızlandırıldı
  React.useEffect(() => {
    Animated.timing(collapseAnimation, {
      toValue: isCollapsed ? 0 : 1,
      duration: 150, // 300ms -> 150ms
      useNativeDriver: true,
    }).start();
  }, [isCollapsed]);

  // Pulse animasyonu (badge'ler için) - Hızlandırıldı
  React.useEffect(() => {
    if (pendingOrders > 0 || lowStockItems > 0) {
      const pulse = Animated.loop(
        Animated.sequence([
          Animated.timing(pulseAnimation, {
            toValue: 1.05, // 1.1 -> 1.05 (daha az belirgin)
            duration: 500, // 1000ms -> 500ms
            useNativeDriver: true,
          }),
          Animated.timing(pulseAnimation, {
            toValue: 1,
            duration: 500, // 1000ms -> 500ms
            useNativeDriver: true,
          }),
        ])
      );
      pulse.start();
      
      return () => pulse.stop();
    }
  }, [pendingOrders, lowStockItems]);

  if (isCollapsed) {
    return (
      <Animated.View 
        style={[
          styles.collapsedContainer,
          {
            transform: [{
              translateX: collapseAnimation.interpolate({
                inputRange: [0, 1],
                outputRange: [-100, 0],
              })
            }],
            opacity: collapseAnimation.interpolate({
              inputRange: [0, 1],
              outputRange: [0, 1],
            }),
          }
        ]}
      >
        <TouchableOpacity
          style={styles.expandButton}
          onPress={handleToggleCollapse}
        >
          <Ionicons name="chevron-forward" size={24} color={Colors.light.primary} />
        </TouchableOpacity>
        
        {/* Mini özet - Sadece siparişler */}
        <View style={styles.miniSummary}>
          <Text style={styles.miniSummaryText}>{pendingOrders}</Text>
          <Text style={styles.miniSummaryLabel}>Bestellungen</Text>
        </View>
        
        {/* Mini favori sayısı */}
        {favoriteProducts.length > 0 && (
          <View style={styles.miniFavorites}>
            <Ionicons name="heart" size={16} color={Colors.light.primary} />
            <Text style={styles.miniFavoritesText}>{favoriteProducts.length}</Text>
          </View>
        )}
      </Animated.View>
    );
  }

  return (
    <Animated.View 
      style={[
        styles.container,
        {
          transform: [{
            translateX: collapseAnimation.interpolate({
              inputRange: [0, 1],
              outputRange: [-screenWidth, 0],
            })
          }],
          opacity: collapseAnimation,
        }
      ]}
    >
        {/* Header */}
        <View style={styles.header}>
          <Text style={styles.titleSmall}>{t('quickAccess.title')}</Text>
          <TouchableOpacity
            style={styles.collapseButton}
            onPress={handleToggleCollapse}
          >
            <Ionicons name="chevron-back" size={20} color={Colors.light.textSecondary} />
          </TouchableOpacity>
        </View>
        
        {/* Günlük Özet - Sadece Siparişler */}
        <View style={styles.summaryContainer}>
          <View style={styles.summaryItem}>
            <Animated.View style={{ transform: [{ scale: pulseAnimation }] }}>
              <Ionicons name="time-outline" size={24} color={Colors.light.warning} />
            </Animated.View>
            <Text style={styles.summaryValue}>{pendingOrders}</Text>
            <Text style={styles.summaryLabel}>{t('quickAccess.pendingOrders')}</Text>
          </View>
        </View>

        {/* Favori Ürünler - Hızlı Erişim */}
        {favoriteProducts.length > 0 && (
          <View style={styles.favoritesSection}>
            <Text style={styles.favoritesTitle}>
              <Ionicons name="heart" size={16} color={Colors.light.primary} />
              {' '}{t('quickAccess.favoriteProducts')} ({favoriteProducts.length})
            </Text>
            <ScrollView horizontal showsHorizontalScrollIndicator={false}>
              <View style={styles.favoritesContainer}>
                {favoriteProducts.slice(0, 6).map((product) => (
                  <TouchableOpacity
                    key={product.id}
                    style={styles.favoriteProductCard}
                    onPress={() => onFavoriteProductPress?.(product)}
                    activeOpacity={0.8}
                  >
                    <View style={styles.favoriteProductIcon}>
                      <Ionicons name="add-circle" size={20} color={Colors.light.primary} />
                    </View>
                    <Text style={styles.favoriteProductName} numberOfLines={2}>
                      {product.name}
                    </Text>
                    <Text style={styles.favoriteProductPrice}>
                      €{product.price.toFixed(2)}
                    </Text>
                  </TouchableOpacity>
                ))}
              </View>
            </ScrollView>
          </View>
        )}


      </Animated.View>
  );
};

const styles = StyleSheet.create({
  container: {
    backgroundColor: Colors.light.background,
    padding: Spacing.md,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.md,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 2,
    },
    shadowOpacity: 0.1,
    shadowRadius: 3.84,
    elevation: 5,
  },
  collapsedContainer: {
    position: 'absolute',
    left: 0,
    top: 0,
    bottom: 0,
    width: 60,
    backgroundColor: Colors.light.background,
    borderRadius: BorderRadius.md,
    marginBottom: Spacing.md,
    shadowColor: '#000',
    shadowOffset: {
      width: 0,
      height: 2,
    },
    shadowOpacity: 0.1,
    shadowRadius: 3.84,
    elevation: 5,
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: Spacing.md,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.md,
  },
  title: {
    ...Typography.h3,
    color: Colors.light.text,
    fontSize: 16,
  },
  titleSmall: {
    ...Typography.h3,
    color: Colors.light.text,
    fontSize: 12,
    fontWeight: '500',
  },
  collapseButton: {
    padding: Spacing.xs,
  },
  expandButton: {
    padding: Spacing.xs,
    backgroundColor: Colors.light.primary + '20',
    borderRadius: BorderRadius.sm,
  },
  miniSummary: {
    alignItems: 'center',
  },
  miniSummaryText: {
    ...Typography.h3,
    color: Colors.light.success,
    fontWeight: '600',
  },
  miniSummaryLabel: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    marginTop: Spacing.xs,
  },
  miniFavorites: {
    alignItems: 'center',
    marginTop: Spacing.sm,
  },
  miniFavoritesText: {
    ...Typography.caption,
    color: Colors.light.primary,
    fontWeight: '600',
    marginTop: Spacing.xs,
  },
  summaryContainer: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: Spacing.lg,
  },
  summaryItem: {
    alignItems: 'center',
    flex: 1,
  },
  summaryValue: {
    ...Typography.h2,
    color: Colors.light.text,
    marginTop: Spacing.sm,
  },
  summaryLabel: {
    ...Typography.caption,
    color: Colors.light.textSecondary,
    textAlign: 'center',
    marginTop: Spacing.xs,
  },
  favoritesSection: {
    marginBottom: Spacing.lg,
  },
  favoritesTitle: {
    ...Typography.h3,
    color: Colors.light.text,
    marginBottom: Spacing.sm,
    flexDirection: 'row',
    alignItems: 'center',
    fontSize: 14,
  },
  favoritesContainer: {
    flexDirection: 'row',
    gap: Spacing.sm,
  },
  favoriteProductCard: {
    width: 80,
    height: 80,
    backgroundColor: Colors.light.primary + '10',
    borderRadius: BorderRadius.sm,
    justifyContent: 'center',
    alignItems: 'center',
    padding: Spacing.xs,
    borderWidth: 1,
    borderColor: Colors.light.primary + '20',
  },
  favoriteProductIcon: {
    marginBottom: Spacing.xs,
  },
  favoriteProductName: {
    ...Typography.caption,
    color: Colors.light.text,
    textAlign: 'center',
    fontWeight: '500',
    fontSize: 10,
  },
  favoriteProductPrice: {
    ...Typography.caption,
    color: Colors.light.primary,
    fontWeight: '600',
    fontSize: 10,
    marginTop: Spacing.xs,
  },

});

export default QuickAccessPanel; 