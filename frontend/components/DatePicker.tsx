import { Ionicons } from '@expo/vector-icons';
import DateTimePicker from '@react-native-community/datetimepicker';
import React, { useState } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Modal,
  Platform,
} from 'react-native';

import { useColorScheme } from '../hooks/useColorScheme';

interface DatePickerProps {
  value: Date | null;
  onChange: (date: Date | null) => void;
  placeholder?: string;
  label?: string;
  mode?: 'date' | 'time' | 'datetime';
  minimumDate?: Date;
  maximumDate?: Date;
  style?: any;
}

export function DatePicker({
  value,
  onChange,
  placeholder = 'Tarih seçin',
  label,
  mode = 'date',
  minimumDate,
  maximumDate,
  style,
}: DatePickerProps) {
  const colorScheme = useColorScheme();
  const [showPicker, setShowPicker] = useState(false);

  const handleDateChange = (event: any, selectedDate?: Date) => {
    if (Platform.OS === 'android') {
      setShowPicker(false);
    }
    
    if (selectedDate) {
      onChange(selectedDate);
    }
  };

  const handleConfirm = (date: Date) => {
    onChange(date);
    setShowPicker(false);
  };

  const handleCancel = () => {
    setShowPicker(false);
  };

  const formatDate = (date: Date) => {
    if (mode === 'date') {
      return date.toLocaleDateString('tr-TR');
    } else if (mode === 'time') {
      return date.toLocaleTimeString('tr-TR', {
        hour: '2-digit',
        minute: '2-digit',
      });
    } else {
      return date.toLocaleString('tr-TR');
    }
  };

  const renderPicker = () => {
    if (Platform.OS === 'ios') {
      return (
        <Modal
          visible={showPicker}
          transparent
          animationType="slide"
        >
          <View style={styles.modalOverlay}>
            <View style={[
              styles.modalContent,
              {
                backgroundColor: colorScheme === 'dark' ? '#1C1C1E' : '#FFFFFF',
              },
            ]}>
              <View style={styles.modalHeader}>
                <TouchableOpacity onPress={handleCancel}>
                  <Text style={[
                    styles.modalButton,
                    { color: colorScheme === 'dark' ? '#FFFFFF' : '#007AFF' }
                  ]}>
                    İptal
                  </Text>
                </TouchableOpacity>
                <Text style={[
                  styles.modalTitle,
                  { color: colorScheme === 'dark' ? '#FFFFFF' : '#000000' }
                ]}>
                  {label || 'Tarih Seç'}
                </Text>
                <TouchableOpacity onPress={() => handleConfirm(value || new Date())}>
                  <Text style={[
                    styles.modalButton,
                    { color: colorScheme === 'dark' ? '#FFFFFF' : '#007AFF' }
                  ]}>
                    Tamam
                  </Text>
                </TouchableOpacity>
              </View>
              <DateTimePicker
                value={value || new Date()}
                mode={mode}
                display="spinner"
                onChange={handleDateChange}
                minimumDate={minimumDate}
                maximumDate={maximumDate}
                style={styles.picker}
              />
            </View>
          </View>
        </Modal>
      );
    } else {
      return showPicker ? (
        <DateTimePicker
          value={value || new Date()}
          mode={mode}
          display="default"
          onChange={handleDateChange}
          minimumDate={minimumDate}
          maximumDate={maximumDate}
        />
      ) : null;
    }
  };

  return (
    <View style={[styles.container, style]}>
      {label && (
        <Text style={[
          styles.label,
          { color: colorScheme === 'dark' ? '#FFFFFF' : '#000000' }
        ]}>
          {label}
        </Text>
      )}
      <TouchableOpacity
        style={[
          styles.input,
          {
            backgroundColor: colorScheme === 'dark' ? '#2C2C2E' : '#F2F2F7',
            borderColor: colorScheme === 'dark' ? '#3A3A3C' : '#E5E5EA',
          },
        ]}
        onPress={() => setShowPicker(true)}
      >
        <Text style={[
          styles.inputText,
          {
            color: value
              ? (colorScheme === 'dark' ? '#FFFFFF' : '#000000')
              : (colorScheme === 'dark' ? '#8E8E93' : '#8E8E93'),
          },
        ]}>
          {value ? formatDate(value) : placeholder}
        </Text>
        <Ionicons
          name="calendar-outline"
          size={20}
          color={colorScheme === 'dark' ? '#8E8E93' : '#8E8E93'}
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