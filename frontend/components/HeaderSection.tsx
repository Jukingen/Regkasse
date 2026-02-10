import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  StyleSheet,
  TouchableOpacity,
} from 'react-native';

import { Colors, Spacing, BorderRadius, Typography } from '../constants/Colors';

interface HeaderSectionProps {
  user: any;
  selectedTable: string;
  pendingOrdersCount: number;
  onShowOrderManager: () => void;
  onShowFavoritesManager: () => void;
  onShowTableManager: () => void;
  onQuickAction: (action: string) => void;
}

const HeaderSection: React.FC<HeaderSectionProps> = ({
  user,
  selectedTable,
  pendingOrdersCount,
  onShowOrderManager,
  onShowFavoritesManager,
  onShowTableManager,
  onQuickAction,
}) => {
  const { t } = useTranslation(['checkout', 'header', 'common']);

  return (
    <View style={styles.header}>
      <View style={styles.headerInfo}>
        <Text style={styles.headerTitle}>{t('checkout:title')}</Text>
        <Text style={styles.headerSubtitle}>
          {user?.firstName} {user?.lastName}
          {selectedTable && ` • Tisch ${selectedTable}`}
        </Text>
      </View>
      <View style={styles.headerActions}>
        {/* Bekleyen Siparişler Butonu */}
        <TouchableOpacity
          style={styles.headerActionButton}
          onPress={onShowOrderManager}
        >
          <Ionicons name="time-outline" size={18} color="white" />
          <Text style={styles.headerActionText}>{t('header:orders', 'Bestellungen')}</Text>
          {pendingOrdersCount > 0 && (
            <View style={styles.headerBadge}>
              <Text style={styles.headerBadgeText}>{pendingOrdersCount}</Text>
            </View>
          )}
        </TouchableOpacity>

        {/* Favori Ürünler Butonu */}
        <TouchableOpacity
          style={styles.headerActionButton}
          onPress={onShowFavoritesManager}
        >
          <Ionicons name="heart-outline" size={18} color="white" />
          <Text style={styles.headerActionText}>{t('header:favorites', 'Favoriten')}</Text>
        </TouchableOpacity>

        {/* Ausstehende Bestellungen Butonu */}
        <TouchableOpacity
          style={styles.headerActionButton}
          onPress={() => onQuickAction('orders')}
        >
          <Ionicons name="list-outline" size={18} color="white" />
          <Text style={styles.headerActionText}>{t('header:pending', 'Ausstehende')}</Text>
          {pendingOrdersCount > 0 && (
            <View style={styles.headerBadge}>
              <Text style={styles.headerBadgeText}>{pendingOrdersCount}</Text>
            </View>
          )}
        </TouchableOpacity>

        {/* Masa Yönetimi Butonu */}
        <TouchableOpacity
          style={styles.headerActionButton}
          onPress={onShowTableManager}
        >
          <Ionicons name="grid-outline" size={18} color="white" />
          <Text style={styles.headerActionText}>{t('header:tables', 'Tische')}</Text>
        </TouchableOpacity>
      </View>
    </View>
  );
};

const styles = StyleSheet.create({
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    backgroundColor: Colors.light.primary,
  },
  headerInfo: {
    flex: 1,
  },
  headerTitle: {
    ...Typography.h2,
    color: 'white',
    fontWeight: 'bold',
  },
  headerSubtitle: {
    ...Typography.body,
    color: 'white',
    opacity: 0.9,
    marginTop: Spacing.xs,
  },
  headerActions: {
    flexDirection: 'row',
    gap: Spacing.sm,
  },
  headerActionButton: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: Colors.light.primary + '20',
    paddingHorizontal: Spacing.sm,
    paddingVertical: Spacing.xs,
    borderRadius: BorderRadius.sm,
    marginRight: Spacing.sm,
    position: 'relative',
  },
  headerActionText: {
    ...Typography.caption,
    color: 'white',
    fontWeight: '600',
    marginLeft: Spacing.xs,
    fontSize: 12,
  },
  headerBadge: {
    position: 'absolute',
    top: -5,
    right: -5,
    backgroundColor: Colors.light.error,
    borderRadius: 10,
    minWidth: 18,
    height: 18,
    justifyContent: 'center',
    alignItems: 'center',
  },
  headerBadgeText: {
    color: 'white',
    fontSize: 10,
    fontWeight: 'bold',
  },
});

export default HeaderSection; 