import { Ionicons } from '@expo/vector-icons';
import React from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Modal,
  TouchableWithoutFeedback,
} from 'react-native';

import { useColorScheme } from '../hooks/useColorScheme';

interface ActionOption {
  id: string;
  title: string;
  icon?: string;
  color?: string;
  destructive?: boolean;
}

interface ActionSheetProps {
  visible: boolean;
  onClose: () => void;
  onSelect: (option: ActionOption) => void;
  title?: string;
  options: ActionOption[];
  cancelText?: string;
}

export function ActionSheet({
  visible,
  onClose,
  onSelect,
  title,
  options,
  cancelText = 'İptal',
}: ActionSheetProps) {
  const colorScheme = useColorScheme();

  const handleSelect = (option: ActionOption) => {
    onSelect(option);
    onClose();
  };

  return (
    <Modal
      visible={visible}
      transparent
      animationType="fade"
      onRequestClose={onClose}
    >
      <TouchableWithoutFeedback onPress={onClose}>
        <View style={styles.overlay}>
          <TouchableWithoutFeedback>
            <View style={[
              styles.container,
              {
                backgroundColor: colorScheme === 'dark' ? '#1C1C1E' : '#FFFFFF',
              },
            ]}>
              {title && (
                <View style={styles.titleContainer}>
                  <Text style={[
                    styles.title,
                    { color: colorScheme === 'dark' ? '#FFFFFF' : '#000000' }
                  ]}>
                    {title}
                  </Text>
                </View>
              )}
              {/* Sadece options render edilecek, cancel butonu tamamen kaldırıldı */}
              {options.map((option, index) => (
                <TouchableOpacity
                  key={option.id}
                  style={[
                    styles.option,
                    index === 0 && styles.firstOption,
                    index === options.length - 1 && styles.lastOption,
                  ]}
                  onPress={() => handleSelect(option)}
                >
                  {option.icon && (
                    <Ionicons
                      name={option.icon as any}
                      size={20}
                      color={option.destructive ? '#FF3B30' : option.color || '#007AFF'}
                      style={styles.optionIcon}
                    />
                  )}
                  <Text style={[
                    styles.optionText,
                    {
                      color: option.destructive
                        ? '#FF3B30'
                        : option.color || (colorScheme === 'dark' ? '#FFFFFF' : '#000000'),
                    },
                  ]}>
                    {option.title}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
          </TouchableWithoutFeedback>
        </View>
      </TouchableWithoutFeedback>
    </Modal>
  );
}

const styles = StyleSheet.create({
  overlay: {
    flex: 1,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  container: {
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    paddingBottom: 20,
  },
  titleContainer: {
    paddingVertical: 16,
    paddingHorizontal: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#E5E5EA',
  },
  title: {
    fontSize: 16,
    fontWeight: '600',
    textAlign: 'center',
  },
  optionsContainer: {
    paddingVertical: 8,
  },
  option: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingVertical: 16,
    paddingHorizontal: 20,
    borderBottomWidth: 1,
    borderBottomColor: '#E5E5EA',
  },
  firstOption: {
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
  },
  lastOption: {
    borderBottomWidth: 0,
  },
  optionIcon: {
    marginRight: 12,
  },
  optionText: {
    fontSize: 16,
    flex: 1,
  },
  cancelButton: {
    marginTop: 8,
    marginHorizontal: 20,
    paddingVertical: 16,
    borderRadius: 12,
    backgroundColor: '#F2F2F7',
    alignItems: 'center',
  },
  cancelText: {
    fontSize: 16,
    fontWeight: '600',
  },
}); 