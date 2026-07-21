import { Ionicons } from '@expo/vector-icons';
import DateTimePicker, {
  type DateTimePickerChangeEvent,
} from '@react-native-community/datetimepicker';
import React, { useEffect, useState, type CSSProperties } from 'react';
import { useTranslation } from 'react-i18next';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Modal,
  Platform,
  type StyleProp,
  type ViewStyle,
} from 'react-native';

import { useColorScheme } from '../hooks/useColorScheme';
import {
  formatDateForHtmlInput,
  formatUserDate,
  formatUserDateTime,
  formatUserTime,
  type DatePickerMode,
} from '../utils/dateFormatter';

/** Austrian POS default civil timezone for report / filter date selection. */
export const POS_DATE_TIME_ZONE = 'Europe/Vienna';

export type { DatePickerMode };

interface DatePickerProps {
  value: Date | null;
  onChange: (date: Date | null) => void;
  placeholder?: string;
  label?: string;
  mode?: DatePickerMode;
  minimumDate?: Date;
  maximumDate?: Date;
  /** IANA tz for the native picker (default: Europe/Vienna). */
  timeZoneName?: string;
  style?: StyleProp<ViewStyle>;
}

function htmlInputType(mode: DatePickerMode): 'date' | 'time' | 'datetime-local' {
  if (mode === 'time') return 'time';
  if (mode === 'datetime') return 'datetime-local';
  return 'date';
}

function parseHtmlInputValue(raw: string, mode: DatePickerMode): Date | null {
  if (!raw) return null;
  if (mode === 'time') {
    const [hh, mm] = raw.split(':').map((p) => Number.parseInt(p, 10));
    if (!Number.isFinite(hh) || !Number.isFinite(mm)) return null;
    const next = new Date();
    next.setHours(hh, mm, 0, 0);
    return next;
  }
  // date / datetime-local — browser gives local civil components
  const parsed = new Date(raw);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
}

export function DatePicker({
  value,
  onChange,
  placeholder,
  label,
  mode = 'date',
  minimumDate,
  maximumDate,
  timeZoneName = POS_DATE_TIME_ZONE,
  style,
}: DatePickerProps) {
  const { t } = useTranslation(['common']);
  const colorScheme = useColorScheme();
  const [showPicker, setShowPicker] = useState(false);
  /** iOS spinner draft — parent updates only on confirm. */
  const [draftDate, setDraftDate] = useState<Date>(() => value ?? new Date());

  useEffect(() => {
    if (showPicker) {
      setDraftDate(value ?? new Date());
    }
  }, [showPicker, value]);

  const resolvedPlaceholder = placeholder ?? t('common:selectDate', 'Datum wählen');
  const resolvedLabel = label ?? t('common:selectDate', 'Datum wählen');

  const isDark = colorScheme === 'dark';
  const fieldBg = isDark ? '#2C2C2E' : '#F2F2F7';
  const fieldBorder = isDark ? '#3A3A3C' : '#E5E5EA';
  const fieldText = isDark ? '#FFFFFF' : '#000000';
  const labelColor = isDark ? '#FFFFFF' : '#000000';

  const commitDate = (date: Date) => {
    onChange(date);
    setShowPicker(false);
  };

  const handleValueChange = (_event: DateTimePickerChangeEvent, selectedDate: Date) => {
    if (Platform.OS === 'android') {
      // Android dialog: selection is final.
      commitDate(selectedDate);
      return;
    }
    // iOS spinner: keep draft until Abbrechen / OK.
    setDraftDate(selectedDate);
  };

  const handleDismiss = () => {
    setShowPicker(false);
  };

  const handleConfirm = () => {
    commitDate(draftDate);
  };

  const formatDisplayValue = (date: Date) => {
    if (mode === 'date') {
      return formatUserDate(date);
    }
    if (mode === 'time') {
      return formatUserTime(date);
    }
    return formatUserDateTime(date);
  };

  // Web: @react-native-community/datetimepicker is unsupported — use HTML5 inputs.
  if (Platform.OS === 'web') {
    const webInputStyle: CSSProperties = {
      flex: 1,
      width: '100%',
      fontSize: 16,
      padding: '12px 16px',
      borderRadius: 8,
      border: `1px solid ${fieldBorder}`,
      backgroundColor: fieldBg,
      color: fieldText,
      boxSizing: 'border-box',
      fontFamily: 'inherit',
      outline: 'none',
    };

    return (
      <View style={[styles.container, style]}>
        {label ? <Text style={[styles.label, { color: labelColor }]}>{label}</Text> : null}
        <View style={styles.webInputRow}>
          {React.createElement('input', {
            type: htmlInputType(mode),
            value: value ? formatDateForHtmlInput(value, mode) : '',
            min: minimumDate ? formatDateForHtmlInput(minimumDate, mode) : undefined,
            max: maximumDate ? formatDateForHtmlInput(maximumDate, mode) : undefined,
            placeholder: resolvedPlaceholder,
            'aria-label': resolvedLabel,
            onChange: (e: { target: { value: string } }) => {
              const next = parseHtmlInputValue(e.target.value, mode);
              onChange(next);
            },
            style: webInputStyle,
          })}
          <Ionicons
            name={mode === 'time' ? 'time-outline' : 'calendar-outline'}
            size={20}
            color="#8E8E93"
            style={styles.webInputIcon}
          />
        </View>
      </View>
    );
  }

  const pickerValue = Platform.OS === 'ios' ? draftDate : value || new Date();
  /** Android native picker only supports date | time. */
  const androidMode = mode === 'datetime' ? 'date' : mode;

  const renderPicker = () => {
    if (Platform.OS === 'ios') {
      return (
        <Modal
          visible={showPicker}
          transparent
          animationType="slide"
          onRequestClose={handleDismiss}>
          <View style={styles.modalOverlay}>
            <View
              style={[
                styles.modalContent,
                {
                  backgroundColor: isDark ? '#1C1C1E' : '#FFFFFF',
                },
              ]}>
              <View style={styles.modalHeader}>
                <TouchableOpacity onPress={handleDismiss} accessibilityRole="button">
                  <Text style={[styles.modalButton, { color: isDark ? '#FFFFFF' : '#007AFF' }]}>
                    {t('common:cancel', 'Abbrechen')}
                  </Text>
                </TouchableOpacity>
                <Text style={[styles.modalTitle, { color: isDark ? '#FFFFFF' : '#000000' }]}>
                  {resolvedLabel}
                </Text>
                <TouchableOpacity onPress={handleConfirm} accessibilityRole="button">
                  <Text style={[styles.modalButton, { color: isDark ? '#FFFFFF' : '#007AFF' }]}>
                    {t('common:ok', 'OK')}
                  </Text>
                </TouchableOpacity>
              </View>
              <DateTimePicker
                value={pickerValue}
                mode={mode}
                display="spinner"
                onValueChange={handleValueChange}
                onDismiss={handleDismiss}
                minimumDate={minimumDate}
                maximumDate={maximumDate}
                timeZoneName={timeZoneName}
                locale="de-DE"
                is24Hour
                themeVariant={isDark ? 'dark' : 'light'}
                style={styles.picker}
              />
            </View>
          </View>
        </Modal>
      );
    }

    if (!showPicker) {
      return null;
    }

    return (
      <DateTimePicker
        value={pickerValue}
        mode={androidMode}
        display="default"
        onValueChange={handleValueChange}
        onDismiss={handleDismiss}
        minimumDate={minimumDate}
        maximumDate={maximumDate}
        timeZoneName={timeZoneName}
        is24Hour
      />
    );
  };

  return (
    <View style={[styles.container, style]}>
      {label ? <Text style={[styles.label, { color: labelColor }]}>{label}</Text> : null}
      <TouchableOpacity
        style={[
          styles.input,
          {
            backgroundColor: fieldBg,
            borderColor: fieldBorder,
          },
        ]}
        onPress={() => {
          setShowPicker(true);
        }}
        accessibilityRole="button">
        <Text
          style={[
            styles.inputText,
            {
              color: value ? fieldText : '#8E8E93',
            },
          ]}>
          {value ? formatDisplayValue(value) : resolvedPlaceholder}
        </Text>
        <Ionicons
          name={mode === 'time' ? 'time-outline' : 'calendar-outline'}
          size={20}
          color="#8E8E93"
        />
      </TouchableOpacity>
      {renderPicker()}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    marginBottom: 16,
  },
  label: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 8,
  },
  input: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderRadius: 8,
    borderWidth: 1,
  },
  inputText: {
    fontSize: 16,
    flex: 1,
  },
  webInputRow: {
    position: 'relative',
    justifyContent: 'center',
  },
  webInputIcon: {
    position: 'absolute',
    right: 14,
    pointerEvents: 'none',
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    paddingBottom: 20,
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#E5E5EA',
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: 'bold',
  },
  modalButton: {
    fontSize: 16,
    fontWeight: '600',
  },
  picker: {
    height: 200,
  },
});
