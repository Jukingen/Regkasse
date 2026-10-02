import React from 'react';
import { useTranslation } from 'react-i18next';
import { StyleSheet, Text, TextInput, type TextInputProps, View } from 'react-native';

import { SoftColors, SoftRadius, SoftSpacing } from '../constants/SoftTheme';
import { useVerticalFeatures } from '../contexts/VerticalProfileContext';

const KNOWN_FIELD_LABEL_KEYS: Record<string, string> = {
  name: 'fields.name',
  phone: 'fields.phone',
  email: 'fields.email',
  petName: 'fields.petName',
  petSpecies: 'fields.petSpecies',
  petBreed: 'fields.petBreed',
  petBirthDate: 'fields.petBirthDate',
  patientNotes: 'fields.patientNotes',
  imei: 'fields.imei',
  route: 'fields.route',
  appointmentDate: 'fields.appointmentDate',
  appointmentTime: 'fields.appointmentTime',
};
const KNOWN_FIELD_PLACEHOLDER_KEYS: Record<string, string> = {
  name: 'placeholders.name',
  phone: 'placeholders.phone',
  email: 'placeholders.email',
  petName: 'placeholders.petName',
  petSpecies: 'placeholders.petSpecies',
  petBreed: 'placeholders.petBreed',
  petBirthDate: 'placeholders.petBirthDate',
  patientNotes: 'placeholders.patientNotes',
  imei: 'placeholders.imei',
  route: 'placeholders.route',
  appointmentDate: 'placeholders.appointmentDate',
  appointmentTime: 'placeholders.appointmentTime',
};

export interface DynamicFieldProps
  extends Omit<TextInputProps, 'value' | 'onChangeText' | 'placeholder'> {
  name: string;
  entity: string;
  value?: string;
  onChangeText?: (value: string) => void;
  label?: string;
  placeholder?: string;
}

export function DynamicField({
  name,
  entity,
  value,
  onChangeText,
  label,
  placeholder,
  ...inputProps
}: DynamicFieldProps) {
  const { t } = useTranslation('verticalProfiles');
  const { requiredFields, optionalFields } = useVerticalFeatures();
  const required = requiredFields[entity]?.includes(name) ?? false;
  const optional = optionalFields[entity]?.includes(name) ?? false;

  if (!required && !optional) return null;

  const labelKey = KNOWN_FIELD_LABEL_KEYS[name] ?? `fields.${name}`;
  const placeholderKey = KNOWN_FIELD_PLACEHOLDER_KEYS[name] ?? `placeholders.${name}`;
  const translatedLabel = label ?? t(labelKey, { defaultValue: name });
  const translatedPlaceholder =
    placeholder ?? t(placeholderKey, { defaultValue: translatedLabel });

  return (
    <View style={styles.container} testID={`dynamic-field-${entity}-${name}`}>
      <Text style={styles.label}>
        {translatedLabel}
        {required ? ' *' : ''}
      </Text>
      <TextInput
        {...inputProps}
        accessibilityLabel={translatedLabel}
        style={[styles.input, inputProps.style]}
        value={value ?? ''}
        onChangeText={onChangeText}
        placeholder={translatedPlaceholder}
        placeholderTextColor={SoftColors.textMuted}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    gap: 6,
    marginBottom: SoftSpacing.sm,
  },
  label: {
    color: SoftColors.textSecondary,
    fontSize: 13,
    fontWeight: '600',
  },
  input: {
    minHeight: 44,
    borderWidth: 1,
    borderColor: SoftColors.border,
    borderRadius: SoftRadius.md,
    paddingHorizontal: SoftSpacing.md,
    paddingVertical: SoftSpacing.sm,
    color: SoftColors.textPrimary,
    backgroundColor: SoftColors.bgCard,
  },
});
