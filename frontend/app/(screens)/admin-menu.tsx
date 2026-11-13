import { MaterialIcons } from '@expo/vector-icons';
import { useFocusEffect } from 'expo-router';
import React, { useCallback, useMemo, useState } from 'react';
import {
  ActivityIndicator,
  Alert,
  ScrollView,
  StyleSheet,
  Text,
  TouchableOpacity,
  View,
} from 'react-native';

import { type AdminTarget, type AdminTargetContext } from '@/constants/adminRoutes';
import { useAuth } from '@/contexts/AuthContext';
import { getCurrentTenantSlug } from '@/services/tenant/tenantStorage';
import { useAdminPermissions } from '@/utils/adminPermissions';
import { openAdmin } from '@/utils/openAdmin';

type AdminAction = {
  id: string;
  title: string;
  description: string;
  target: AdminTarget;
  context?: AdminTargetContext;
  requiresSuperAdmin?: boolean;
  requiresTenant?: boolean;
  requiredRoles?: string[];
  requiredPermissions?: string[];
  icon: keyof typeof MaterialIcons.glyphMap;
};

const ADMIN_ACTIONS: AdminAction[] = [
  {
    id: 'license',
    title: 'Lizenzverwaltung',
    description: 'Lizenzstatus prüfen und verlängern',
    target: 'licenseOverview',
    icon: 'vpn-key',
    requiresSuperAdmin: false,
    requiresTenant: false,
  },
  {
    id: 'cashRegisters',
    title: 'Kassenverwaltung',
    description: 'Kassen bearbeiten oder stilllegen',
    target: 'cashRegisters',
    icon: 'point-of-sale',
    requiresSuperAdmin: false,
    requiresTenant: true,
    requiredRoles: ['Manager', 'SuperAdmin'],
    requiredPermissions: ['settings.manage'],
  },
  {
    id: 'rksv',
    title: 'RKSV Sonderbelege',
    description: 'Startbeleg, Monatsbeleg und Jahresbeleg verwalten',
    target: 'rksvSonderbelege',
    icon: 'description',
    requiresSuperAdmin: false,
    requiresTenant: true,
    requiredRoles: ['Manager', 'SuperAdmin'],
    requiredPermissions: ['admin_rksv', 'report.view'],
  },
  {
    id: 'users',
    title: 'Benutzerverwaltung',
    description: 'Mitarbeiter und Rollen verwalten',
    target: 'userManagement',
    icon: 'people',
    requiresSuperAdmin: false,
    requiresTenant: true,
    requiredRoles: ['SuperAdmin'],
    requiredPermissions: ['user.view', 'user.manage'],
  },
  {
    id: 'reports',
    title: 'Berichte',
    description: 'Tages-, Monats- und Jahresberichte öffnen',
    target: 'tagesbericht',
    icon: 'bar-chart',
    requiresSuperAdmin: false,
    requiresTenant: true,
    requiredRoles: ['Manager', 'SuperAdmin'],
    requiredPermissions: ['report.view'],
  },
  {
    id: 'tenants',
    title: 'Mandantenverwaltung',
    description: 'Mandanten anlegen und bearbeiten',
    target: 'tenantManagement',
    icon: 'domain',
    requiresSuperAdmin: true,
    requiresTenant: false,
    requiredRoles: ['SuperAdmin'],
  },
];

export default function AdminMenuScreen() {
  const { user, isLoading } = useAuth();
  const permissions = useAdminPermissions();
  const [tenantSlug, setTenantSlug] = useState<string | null>(null);
  const [openingActionId, setOpeningActionId] = useState<string | null>(null);

  useFocusEffect(
    useCallback(() => {
      let active = true;

      const loadTenantSlug = async () => {
        try {
          const slug = await getCurrentTenantSlug();
          if (active) {
            setTenantSlug(slug);
          }
        } catch {
          if (active) {
            setTenantSlug(null);
          }
        }
      };

      void loadTenantSlug();

      return () => {
        active = false;
      };
    }, [])
  );

  const isSuperAdmin = useMemo(
    () => user?.role === 'SuperAdmin' || user?.roles?.includes('SuperAdmin') === true,
    [user?.role, user?.roles]
  );

  const filteredActions = useMemo(() => {
    return ADMIN_ACTIONS.filter((action) => {
      switch (action.id) {
        case 'license':
          return permissions.canViewLicense;
        case 'cashRegisters':
          return permissions.canManageCashRegisters;
        case 'rksv':
          return permissions.canManageRksv;
        case 'users':
          return permissions.canManageUsers;
        case 'reports':
          return permissions.canViewReports;
        case 'tenants':
          return permissions.canManageTenants;
        default:
          return action.requiresSuperAdmin ? isSuperAdmin : true;
      }
    });
  }, [isSuperAdmin, permissions]);

  const subtitle = useMemo(() => {
    if (isSuperAdmin && (!tenantSlug || tenantSlug === 'admin')) {
      return 'Super Admin Modus';
    }

    if (tenantSlug) {
      return `Mandant: ${tenantSlug}`;
    }

    return 'Kein Mandant ausgewählt';
  }, [isSuperAdmin, tenantSlug]);

  const handlePress = useCallback(async (action: AdminAction) => {
    setOpeningActionId(action.id);
    try {
      const ok = await openAdmin(action.target, action.context, {
        fallbackToMail: action.id === 'license',
        mailtoSubject: action.id === 'license' ? 'Lizenzverlängerung' : undefined,
      });

      if (!ok) {
        Alert.alert('Hinweis', 'Der Admin-Bereich konnte nicht geöffnet werden.');
      }
    } finally {
      setOpeningActionId(null);
    }
  }, []);

  if (isLoading && !user) {
    return (
      <View style={styles.loadingContainer}>
        <ActivityIndicator color="#007AFF" />
        <Text style={styles.loadingText}>Benutzer wird geladen…</Text>
      </View>
    );
  }

  return (
    <ScrollView style={styles.container} contentContainerStyle={styles.content}>
      <View style={styles.header}>
        <Text style={styles.title}>Admin-Menü</Text>
        <Text style={styles.subtitle}>{subtitle}</Text>
      </View>

      <View style={styles.section}>
        {filteredActions.map((action) => {
          const opening = openingActionId === action.id;
          return (
            <TouchableOpacity
              key={action.id}
              style={styles.listItem}
              onPress={() => {
                handlePress(action).catch(() => undefined);
              }}
              accessibilityRole="button"
              disabled={opening}>
              <View style={styles.iconWrap}>
                <MaterialIcons name={action.icon} size={22} color="#007AFF" />
              </View>
              <View style={styles.textWrap}>
                <Text style={styles.itemTitle}>{action.title}</Text>
                <Text style={styles.itemDescription}>{action.description}</Text>
                {action.requiresTenant ? (
                  <Text style={styles.itemMeta}>
                    {tenantSlug ? `Mandant erforderlich: ${tenantSlug}` : 'Mandant erforderlich'}
                  </Text>
                ) : null}
              </View>
              {opening ? (
                <ActivityIndicator color="#007AFF" />
              ) : (
                <MaterialIcons name="chevron-right" size={22} color="#9aa0a6" />
              )}
            </TouchableOpacity>
          );
        })}
      </View>

      <View style={styles.footer}>
        <Text style={styles.footerText}>Bestimmte Aktionen werden im Admin-Browser geöffnet.</Text>
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#f5f5f5',
  },
  content: {
    paddingBottom: 24,
  },
  loadingContainer: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: '#f5f5f5',
    gap: 12,
  },
  loadingText: {
    fontSize: 14,
    color: '#666',
  },
  header: {
    paddingHorizontal: 20,
    paddingVertical: 24,
    backgroundColor: '#fff',
    alignItems: 'center',
    borderBottomWidth: 1,
    borderBottomColor: '#e5e7eb',
  },
  title: {
    fontSize: 24,
    fontWeight: '700',
    color: '#007AFF',
  },
  subtitle: {
    fontSize: 14,
    color: '#666',
    marginTop: 6,
  },
  section: {
    marginTop: 20,
    marginHorizontal: 20,
    backgroundColor: '#fff',
    borderRadius: 12,
    overflow: 'hidden',
  },
  listItem: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 16,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#e5e7eb',
    gap: 12,
  },
  iconWrap: {
    width: 36,
    alignItems: 'center',
    justifyContent: 'center',
  },
  textWrap: {
    flex: 1,
  },
  itemTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#1f2937',
  },
  itemDescription: {
    fontSize: 13,
    color: '#6b7280',
    marginTop: 2,
  },
  itemMeta: {
    fontSize: 12,
    color: '#007AFF',
    marginTop: 4,
  },
  footer: {
    paddingHorizontal: 24,
    paddingTop: 20,
    alignItems: 'center',
  },
  footerText: {
    fontSize: 12,
    color: '#999',
    textAlign: 'center',
    lineHeight: 18,
  },
});
