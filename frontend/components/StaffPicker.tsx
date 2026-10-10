import React, { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { useVerticalFeatures } from '../contexts/VerticalProfileContext';
import { listPosStaff, type PosStaffMember } from '../services/api/staffService';
import { staffInitials } from '../utils/staffInitials';

export interface StaffPickerProps {
  selectedId?: string | null;
  onSelect: (staff: PosStaffMember) => void;
}

export function StaffPicker({ selectedId, onSelect }: StaffPickerProps) {
  const { t } = useTranslation('verticalProfiles');
  const { posFeatures } = useVerticalFeatures();
  const appointmentEnabled = posFeatures.appointment === true;
  const [staff, setStaff] = useState<PosStaffMember[]>([]);

  useEffect(() => {
    if (!appointmentEnabled) return;
    let cancelled = false;
    listPosStaff()
      .then((members) => {
        if (!cancelled) setStaff(members);
      })
      .catch(() => {
        if (!cancelled) setStaff([]);
      });
    return () => {
      cancelled = true;
    };
  }, [appointmentEnabled]);

  if (!appointmentEnabled) return null;

  if (staff.length === 0) {
    return <Text style={styles.empty}>{t('screens.appointments.noStaff')}</Text>;
  }

  return (
    <View style={styles.list}>
      {staff.map((member) => {
        const selected = member.id === selectedId;
        const roleLabel = t(`screens.appointments.roles.${member.role}`, {
          defaultValue: member.role,
        });
        return (
          <Pressable
            key={member.id}
            accessibilityRole="button"
            accessibilityLabel={`${member.name}, ${roleLabel}`}
            onPress={() => onSelect(member)}
            style={[styles.chip, selected && styles.chipSelected]}
          >
            <View style={[styles.avatar, selected && styles.avatarSelected]}>
              <Text style={[styles.initials, selected && styles.initialsSelected]}>
                {staffInitials(member.name)}
              </Text>
            </View>
            <View style={styles.meta}>
              <Text
                style={[styles.name, selected && styles.nameSelected]}
                numberOfLines={1}
              >
                {member.name}
              </Text>
              <Text
                style={[styles.role, selected && styles.roleSelected]}
                numberOfLines={1}
              >
                {roleLabel}
              </Text>
            </View>
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  list: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: SoftSpacing.sm,
  },
  empty: {
    color: SoftColors.textMuted,
  },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: SoftSpacing.sm,
    maxWidth: 220,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.sm,
    paddingVertical: SoftSpacing.sm,
    backgroundColor: SoftColors.bgCard,
  },
  chipSelected: {
    borderColor: SoftColors.accent,
    backgroundColor: SoftColors.accent,
  },
  avatar: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: SoftColors.accentLight,
  },
  avatarSelected: {
    backgroundColor: SoftColors.accentDark,
  },
  initials: {
    color: SoftColors.textPrimary,
    fontWeight: '700',
    fontSize: 12,
  },
  initialsSelected: {
    color: SoftColors.textInverse,
  },
  meta: {
    flexShrink: 1,
  },
  name: {
    color: SoftColors.textPrimary,
    fontWeight: '600',
    fontSize: 13,
  },
  nameSelected: {
    color: SoftColors.textInverse,
  },
  role: {
    color: SoftColors.textSecondary,
    fontSize: 11,
  },
  roleSelected: {
    color: SoftColors.textInverse,
  },
});
