import { Ionicons } from '@expo/vector-icons';
import { useRouter } from 'expo-router';
import React, { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Modal, Pressable, StyleSheet, Text, Vibration, View } from 'react-native';

import { SoftColors, SoftRadius, SoftShadows, SoftSpacing, SoftTypography } from '../constants/SoftTheme';
import { useAuth } from '../contexts/AuthContext';

/** POS UI role labels (de-DE) — aligned with AGENTS.md / Admin display names. */
const ROLE_LABELS: Record<string, string> = {
  SuperAdmin: 'Super-Administrator',
  Manager: 'Mandanten-Admin',
  Cashier: 'Kassierer',
  Waiter: 'Kellner',
  Kitchen: 'Küche',
  Accountant: 'Buchhaltung',
  ReportViewer: 'Berichte (nur Lesen)',
};

function resolveDisplayName(user: {
  username?: string;
  userName?: string;
  firstName?: string;
  lastName?: string;
  email?: string;
}): string {
  const fullName = [user.firstName?.trim(), user.lastName?.trim()].filter(Boolean).join(' ');
  if (fullName) return fullName;
  const name = user.username?.trim() || user.userName?.trim();
  if (name) return name;
  if (user.email?.trim()) return user.email.trim();
  return 'Benutzer';
}

/** Up to two initials for the avatar (name parts or username/email prefix). */
function resolveAvatarInitials(
  user: {
    username?: string;
    userName?: string;
    firstName?: string;
    lastName?: string;
    email?: string;
  },
  displayName: string,
): string {
  const first = user.firstName?.trim()?.charAt(0);
  const last = user.lastName?.trim()?.charAt(0);
  if (first || last) {
    return `${(first || '').toUpperCase()}${(last || '').toUpperCase()}`.slice(0, 2) || '?';
  }

  const parts = displayName
    .trim()
    .split(/[\s._-]+/)
    .filter(Boolean);
  if (parts.length >= 2) {
    return `${parts[0].charAt(0)}${parts[1].charAt(0)}`.toUpperCase();
  }
  if (parts.length === 1 && parts[0].length >= 2) {
    return parts[0].slice(0, 2).toUpperCase();
  }
  if (parts.length === 1) {
    return parts[0].charAt(0).toUpperCase();
  }
  return '?';
}

function resolveRoleLabel(role: string | undefined, roles?: string[]): string {
  const primary = role?.trim() || roles?.find(Boolean)?.trim();
  if (!primary) return 'Benutzer';
  return ROLE_LABELS[primary] || primary;
}

/**
 * POS header user chip: avatar initials + name/role, dropdown with settings + Abmelden.
 */
export function UserMenu() {
  const { user, logout } = useAuth();
  const router = useRouter();
  const { t } = useTranslation(['auth', 'navigation']);
  const [showMenu, setShowMenu] = useState(false);

  const displayName = useMemo(() => (user ? resolveDisplayName(user) : ''), [user]);
  const initials = useMemo(
    () => (user ? resolveAvatarInitials(user, displayName || '?') : '?'),
    [user, displayName],
  );
  const roleLabel = useMemo(
    () => (user ? resolveRoleLabel(user.role, user.roles) : ''),
    [user],
  );

  if (!user) return null;

  const closeMenu = () => setShowMenu(false);

  const handleLogout = () => {
    Vibration.vibrate(10);
    closeMenu();
    void logout();
  };

  const handleOpenSettings = () => {
    Vibration.vibrate(10);
    closeMenu();
    router.push('/(tabs)/settings' as const);
  };

  return (
    <View style={styles.container}>
      <Pressable
        style={({ pressed }) => [styles.userButton, pressed && styles.userButtonPressed]}
        onPress={() => setShowMenu(true)}
        accessibilityRole="button"
        accessibilityLabel={`${displayName}, ${roleLabel}`}
        accessibilityHint={t('auth:logout')}
        hitSlop={6}
      >
        <View style={styles.avatar}>
          <Text style={styles.avatarText}>{initials}</Text>
        </View>
        <View style={styles.userInfo}>
          <Text style={styles.userName} numberOfLines={1}>
            {displayName}
          </Text>
          <Text style={styles.userRole} numberOfLines={1}>
            {roleLabel}
          </Text>
        </View>
        <Ionicons name="chevron-down" size={14} color={SoftColors.textSecondary} />
      </Pressable>

      <Modal
        transparent
        visible={showMenu}
        animationType="fade"
        onRequestClose={closeMenu}
      >
        <Pressable
          style={styles.overlay}
          onPress={closeMenu}
          accessibilityRole="button"
          accessibilityLabel="Menü schließen"
        >
          <Pressable style={styles.dropdown} onPress={(e) => e.stopPropagation()}>
            <View style={styles.dropdownHeader}>
              <View style={styles.dropdownAvatar}>
                <Text style={styles.dropdownAvatarText}>{initials}</Text>
              </View>
              <View style={styles.dropdownInfo}>
                <Text style={styles.dropdownName} numberOfLines={1}>
                  {displayName}
                </Text>
                {user.email ? (
                  <Text style={styles.dropdownEmail} numberOfLines={1}>
                    {user.email}
                  </Text>
                ) : null}
                <View style={styles.roleBadge}>
                  <Text style={styles.roleBadgeText} numberOfLines={1}>
                    {roleLabel}
                  </Text>
                </View>
              </View>
            </View>

            <View style={styles.divider} />

            <Pressable
              style={({ pressed }) => [styles.menuItem, pressed && styles.menuItemPressed]}
              onPress={handleOpenSettings}
              accessibilityRole="button"
              accessibilityLabel={t('navigation:settings')}
            >
              <Ionicons name="settings-outline" size={18} color={SoftColors.textPrimary} />
              <Text style={styles.menuItemText}>{t('navigation:settings')}</Text>
            </Pressable>

            <View style={styles.divider} />

            <Pressable
              style={({ pressed }) => [styles.menuItem, pressed && styles.logoutItemPressed]}
              onPress={handleLogout}
              accessibilityRole="button"
              accessibilityLabel={t('auth:logout')}
            >
              <Ionicons name="log-out-outline" size={18} color={SoftColors.error} />
              <Text style={styles.logoutText}>{t('auth:logout')}</Text>
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
    gap: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: SoftSpacing.xs,
    borderRadius: SoftRadius.md,
    maxWidth: 200,
  },
  userButtonPressed: {
    opacity: 0.85,
  },
  avatar: {
    width: 36,
    height: 36,
    borderRadius: 18,
    backgroundColor: SoftColors.accentDark,
    justifyContent: 'center',
    alignItems: 'center',
  },
  avatarText: {
    color: SoftColors.textInverse,
    fontSize: 13,
    fontWeight: '700',
  },
  userInfo: {
    flexShrink: 1,
    minWidth: 0,
  },
  userName: {
    ...SoftTypography.label,
    fontSize: 14,
    fontWeight: '600',
    color: SoftColors.textPrimary,
    maxWidth: 120,
  },
  userRole: {
    ...SoftTypography.caption,
    fontSize: 12,
    color: SoftColors.textSecondary,
    maxWidth: 120,
  },
  overlay: {
    flex: 1,
    backgroundColor: SoftColors.overlay,
    justifyContent: 'flex-start',
    alignItems: 'flex-end',
    paddingTop: SoftSpacing.xl + SoftSpacing.lg,
    paddingRight: SoftSpacing.md,
  },
  dropdown: {
    backgroundColor: SoftColors.bgCard,
    borderRadius: SoftRadius.lg,
    minWidth: 240,
    maxWidth: 320,
    paddingVertical: SoftSpacing.xs,
    ...SoftShadows.md,
  },
  dropdownHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm + 2,
  },
  dropdownAvatar: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: SoftColors.accentDark,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: SoftSpacing.sm + 2,
  },
  dropdownAvatarText: {
    color: SoftColors.textInverse,
    fontSize: 16,
    fontWeight: '700',
  },
  dropdownInfo: {
    flex: 1,
    minWidth: 0,
  },
  dropdownName: {
    ...SoftTypography.h3,
    fontSize: 16,
    fontWeight: '700',
    color: SoftColors.textPrimary,
  },
  dropdownEmail: {
    ...SoftTypography.caption,
    fontSize: 12,
    color: SoftColors.textSecondary,
    marginTop: 2,
  },
  roleBadge: {
    backgroundColor: SoftColors.bgSecondary,
    borderRadius: SoftRadius.sm,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: 2,
    marginTop: SoftSpacing.xs,
    alignSelf: 'flex-start',
  },
  roleBadgeText: {
    ...SoftTypography.caption,
    fontSize: 12,
    fontWeight: '600',
    color: SoftColors.textSecondary,
  },
  divider: {
    height: StyleSheet.hairlineWidth,
    backgroundColor: SoftColors.border,
    marginHorizontal: SoftSpacing.sm,
  },
  menuItem: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm + 2,
  },
  menuItemPressed: {
    backgroundColor: SoftColors.bgSecondary,
  },
  menuItemText: {
    ...SoftTypography.label,
    fontSize: 14,
    color: SoftColors.textPrimary,
    fontWeight: '500',
  },
  logoutItemPressed: {
    backgroundColor: SoftColors.errorBg,
  },
  logoutText: {
    ...SoftTypography.label,
    fontSize: 14,
    color: SoftColors.error,
    fontWeight: '600',
  },
});
