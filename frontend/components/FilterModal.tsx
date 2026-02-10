import { Ionicons } from '@expo/vector-icons';
import React, { useState } from 'react';
import {
  View,
  Text,
  Modal,
  TouchableOpacity,
  StyleSheet,
  ScrollView,
  Switch,
} from 'react-native';

import { useColorScheme } from '../hooks/useColorScheme';

interface FilterOption {
  id: string;
  label: string;
  value: any;
}

interface FilterModalProps {
  visible: boolean;
  onClose: () => void;
  onApply: (filters: any) => void;
  title?: string;
  filters: {
    [key: string]: any;
  };
  filterOptions?: {
    [key: string]: FilterOption[];
  };
}

export function FilterModal({
  visible,
  onClose,
  onApply,
  title = 'Filtreler',
  filters,
  filterOptions = {},
}: FilterModalProps) {
  const colorScheme = useColorScheme();
  const [localFilters, setLocalFilters] = useState(filters);

  const handleApply = () => {
    onApply(localFilters);
    onClose();
  };

  const handleReset = () => {
    const resetFilters = Object.keys(filters).reduce((acc, key) => {
      acc[key] = null;
      return acc;
    }, {} as any);
    setLocalFilters(resetFilters);
  };

  const updateFilter = (key: string, value: any) => {
    setLocalFilters(prev => ({
      ...prev,
      [key]: value,
    }));
  };

  const renderFilterOption = (key: string, options: FilterOption[]) => {
    return (
      <View key={key} style={styles.filterSection}>
        <Text style={[
          styles.filterLabel,
          { color: colorScheme === 'dark' ? '#FFFFFF' : '#000000' }
        ]}>
          {key.charAt(0).toUpperCase() + key.slice(1)}
        </Text>
        <View style={styles.optionsContainer}>
          {options.map((option) => (
            <TouchableOpacity
              key={option.id}
              style={[
                styles.optionButton,
                {
                  backgroundColor: localFilters[key] === option.value
                    ? '#007AFF'
                    : colorScheme === 'dark'
                    ? '#2C2C2E'
                    : '#F2F2F7',
                },
              ]}
              onPress={() => updateFilter(key, option.value)}
            >
              <Text style={[
                styles.optionText,
                {
                  color: localFilters[key] === option.value
                    ? '#FFFFFF'
                    : colorScheme === 'dark'
                    ? '#FFFFFF'
                    : '#000000',
                },
              ]}>
                {option.label}
              </Text>
            </TouchableOpacity>
          ))}
        </View>
      </View>
    );
  };

  const renderBooleanFilter = (key: string) => {
    return (
      <View key={key} style={styles.filterSection}>
        <View style={styles.booleanFilterRow}>
          <Text style={[
            styles.filterLabel,
            { color: colorScheme === 'dark' ? '#FFFFFF' : '#000000' }
          ]}>
            {key.charAt(0).toUpperCase() + key.slice(1)}
          </Text>
          <Switch
            value={localFilters[key] || false}
            onValueChange={(value) => updateFilter(key, value)}
            trackColor={{ false: '#767577', true: '#81b0ff' }}
            thumbColor={localFilters[key] ? '#007AFF' : '#f4f3f4'}
          />
        </View>
      </View>
    );
  };

  return (
    <Modal
      visible={visible}
      animationType="slide"
      transparent
      onRequestClose={onClose}
    >
      <View style={styles.modalOverlay}>
        <View style={[
          styles.modalContent,
          {
            backgroundColor: colorScheme === 'dark' ? '#1C1C1E' : '#FFFFFF',
          },
        ]}>
          <View style={styles.modalHeader}>
            <Text style={[
              styles.modalTitle,
              { color: colorScheme === 'dark' ? '#FFFFFF' : '#000000' }
            ]}>
              {title}
            </Text>
            <TouchableOpacity onPress={onClose} style={styles.closeButton}>
              <Ionicons
                name="close"
                size={24}
                color={colorScheme === 'dark' ? '#FFFFFF' : '#000000'}
              />
            </TouchableOpacity>
          </View>

          <ScrollView style={styles.filterContent}>
            {Object.entries(filterOptions).map(([key, options]) =>
              renderFilterOption(key, options)
            )}
            
            {Object.keys(filters).filter(key => !filterOptions[key]).map(key =>
              renderBooleanFilter(key)
            )}
          </ScrollView>

          <View style={styles.modalFooter}>
            <TouchableOpacity
              style={[styles.footerButton, styles.resetButton]}
              onPress={handleReset}
            >
              <Text style={styles.resetButtonText}>Sıfırla</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={[styles.footerButton, styles.applyButton]}
              onPress={handleApply}
            >
              <Text style={styles.applyButtonText}>Uygula</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    maxHeight: '80%',
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
  closeButton: {
    padding: 4,
  },
  filterContent: {
    padding: 20,
  },
  filterSection: {
    marginBottom: 20,
  },
  filterLabel: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 10,
  },
  optionsContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  optionButton: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
    borderWidth: 1,
    borderColor: '#E5E5EA',
  },
  optionText: {
    fontSize: 14,
  },
  booleanFilterRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  modalFooter: {
    flexDirection: 'row',
    padding: 20,
    gap: 12,
  },
  footerButton: {
    flex: 1,
    paddingVertical: 12,
    borderRadius: 8,
    alignItems: 'center',
  },
  resetButton: {
    backgroundColor: '#F2F2F7',
  },
  applyButton: {
    backgroundColor: '#007AFF',
  },
  resetButtonText: {
    color: '#007AFF',
    fontSize: 16,
    fontWeight: '600',
  },
  applyButtonText: {
    color: '#FFFFFF',
    fontSize: 16,
    fontWeight: '600',
  },
}); 