import { Ionicons } from '@expo/vector-icons';
import React, { useState } from 'react';
import {
  View,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  Platform,
} from 'react-native';

import { useColorScheme } from '../hooks/useColorScheme';

interface SearchBarProps {
  placeholder?: string;
  value: string;
  onChangeText: (text: string) => void;
  onSearch?: () => void;
  onClear?: () => void;
  style?: any;
}

export function SearchBar({
  placeholder = 'Ara...',
  value,
  onChangeText,
  onSearch,
  onClear,
  style,
}: SearchBarProps) {
  const colorScheme = useColorScheme();
  const [isFocused, setIsFocused] = useState(false);

  const handleClear = () => {
    onChangeText('');
    onClear?.();
  };

  const handleSearch = () => {
    onSearch?.();
  };

  return (
    <View style={[styles.container, style]}>
      <View
        style={[
          styles.searchContainer,
          {
            backgroundColor: colorScheme === 'dark' ? '#2C2C2E' : '#F2F2F7',
            borderColor: isFocused
              ? '#007AFF'
              : colorScheme === 'dark'
              ? '#3A3A3C'
              : '#E5E5EA',
          },
        ]}
      >
        <Ionicons
          name="search"
          size={20}
          color={colorScheme === 'dark' ? '#8E8E93' : '#8E8E93'}
          style={styles.searchIcon}
        />
        <TextInput
          style={[
            styles.input,
            {
              color: colorScheme === 'dark' ? '#FFFFFF' : '#000000',
            },
          ]}
          placeholder={placeholder}
          placeholderTextColor={colorScheme === 'dark' ? '#8E8E93' : '#8E8E93'}
          value={value}
          onChangeText={onChangeText}
          onFocus={() => setIsFocused(true)}
          onBlur={() => setIsFocused(false)}
          onSubmitEditing={handleSearch}
          returnKeyType="search"
          autoCapitalize="none"
          autoCorrect={false}
        />
        {value.length > 0 && (
          <TouchableOpacity onPress={handleClear} style={styles.clearButton}>
            <Ionicons
              name="close-circle"
              size={20}
              color={colorScheme === 'dark' ? '#8E8E93' : '#8E8E93'}
            />
          </TouchableOpacity>
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    paddingHorizontal: 16,
    paddingVertical: 8,
  },
  searchContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    borderRadius: 10,
    borderWidth: 1,
    paddingHorizontal: 12,
    paddingVertical: 8,
    ...Platform.select({
      ios: {
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 1 },
        shadowOpacity: 0.1,
        shadowRadius: 2,
      },
      android: {
        elevation: 2,
      },
    }),
  },
  searchIcon: {
    marginRight: 8,
  },
  input: {
    flex: 1,
    fontSize: 16,
    paddingVertical: 4,
  },
  clearButton: {
    marginLeft: 8,
    padding: 2,
  },
}); 