import { Ionicons } from '@expo/vector-icons';
import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Modal, Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftShadows, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useAuth } from '../contexts/AuthContext';

function resolveDisplayName(user: {
  username?: string;
  email?: string;
  firstName?: string;
  lastName?: string;
}): string {
  const fullName = [user.firstName, user.lastName].filter(Boolean).join(' ').trim();
  if (fullName) return fullName;
  if (user.username?.trim()) return user.username.trim();
  if (user.email?.trim()) return user.email.trim();
  return 'Benutzer';
}

function resolveInitials(displayName: string): string {
  const parts = displayName.split(/[\s@._-]+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0] ?? ''}${parts[1][0] ?? ''}`.toUpperCase();
}

/**
 * Compact POS header user chip: avatar + name, dropdown with Abmelden.
 */
export function UserMenu() {
  const { user, logout } = useAuth();
  const { t } = useTranslation(['auth']);
  const [showMenu, setShowMenu] = useState(false);

  const displayName = useMemo(() => (user ? resolveDisplayName(user) : ''), [user]);
  const initials = useMemo(() => resolveInitials(displayName || '?'), [displayName]);

  if (!user) return null;

  const handleLogout = () => {
    setShowMenu(false);
    void logout();
  };

  return (
    <View style={styles.container}>
      <Pressable
        style={({ pressed }) => [styles.userButton, pressed && styles.userButtonPressed]}
        onPress={() => setShowMenu(true)}
        accessibilityRole="button"
        accessibilityLabel={displayName}
        accessibilityHint={t('auth:logout')}
        hitSlop={6}
      >
        <View style={styles.avatar}>
          <Text style={styles.avatarText}>{initials}</Text>
        </View>
        <Text style={styles.userName} numberOfLines={1}>
          {displayName}
        </Text>
        <Ionicons name="chevron-down" size={14} color={SoftColors.textSecondary} />
      </Pressable>

      <Modal
        transparent
        visible={showMenu}
        animationType="fade"
        onRequestClose={() => setShowMenu(false)}
      >
        <Pressable
          style={styles.overlay}
          onPress={() => setShowMenu(false)}
          accessibilityRole="button"
          accessibilityLabel="Menü schließen"
        >
          <Pressable style={styles.dropdown} onPress={(e) => e.stopPropagation()}>
            <View style={styles.dropdownHeader}>
              <Text style={styles.dropdownName} numberOfLines={1}>
                {displayName}
              </Text>
              {user.email ? (
                <Text style={styles.dropdownEmail} numberOfLines={1}>
                  {user.email}
                </Text>
              ) : null}
              {user.role ? (
                <Text style={styles.dropdownRole} numberOfLines={1}>
                  {user.role}
                </Text>
              ) : null}
            </View>
            <View style={styles.divider} />
            <Pressable
              style={({ pressed }) => [styles.dropdownItem, pressed && styles.dropdownItemPressed]}
              onPress={handleLogout}
              accessibilityRole="button"
              accessibilityLabel={t('auth:logout')}
            >
              <Ionicons name="log-out-outline" size={18} color={SoftColors.error} />
              <Text style={styles.dropdownItemText}>{t('auth:logout')}</Text>
            </Pressable>
          </Pressable>
        </Pressable>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    position: 'relative',
  },
  userButton: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.xs,
    paddingHorizontal: SoftSpacing.xs,
    paddingVertical: 4,
    borderRadius: SoftRadius.md,
    maxWidth: 140,
  },
  userButtonPressed: {
    opacity: 0.85,
  },
  avatar: {
    width: 28,
    height: 28,
    borderRadius: 14,
    backgroundColor: SoftColors.accentDark,
    justifyContent: 'center',
    alignItems: 'center',
  },
  avatarText: {
    color: SoftColors.textInverse,
    fontSize: 11,
    fontWeight: '700',
  },
  userName: {
    ...SoftTypography.label,
    fontSize: 13,
    color: SoftColors.textPrimary,
    flexShrink: 1,
    maxWidth: 72,
  },
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.3)',
    justifyContent: 'flex-start',
    alignItems: 'flex-end',
    paddingTop: SoftSpacing.xl + SoftSpacing.lg,
    paddingRight: SoftSpacing.md,
  },
  dropdown: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    minWidth: 200,
    maxWidth: 280,
    paddingVertical: SoftSpacing.xs,
    ...SoftShadows.md,
  },
  dropdownHeader: {
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
  },
  dropdownName: {
    ...SoftTypography.h3,
    fontSize: 15,
    color: SoftColors.textPrimary,
  },
  dropdownEmail: {
    ...SoftTypography.caption,
    color: SoftColors.textSecondary,
    marginTop: 2,
  },
  dropdownRole: {
    ...SoftTypography.caption,
    fontSize: 11,
    color: SoftColors.textMuted,
    marginTop: 2,
  },
  divider: {
    height: StyleSheet.hairlineWidth,
    backgroundColor: SoftColors.border,
    marginHorizontal: SoftSpacing.sm,
  },
  dropdownItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm + 2,
  },
  dropdownItemPressed: {
    backgroundColor: SoftColors.errorBg,
  },
  dropdownItemText: {
    ...SoftTypography.label,
    fontSize: 14,
    color: SoftColors.error,
    fontWeight: '600',
  },
});
