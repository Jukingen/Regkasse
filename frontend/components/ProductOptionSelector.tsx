// Türkçe Açıklama: Ürün seçenekleri (toppings, ek malzemeler, özel istekler) için dinamik komponent. Farklı seçenek tiplerini destekler.

import React, { useState } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, TextInput, ScrollView } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';

export interface ProductOptionValue {
  id: string;
  value: string;
  priceModifier: number;
  isDefault: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface ProductOption {
  id: string;
  name: string;
  description?: string;
  optionType: 'SingleChoice' | 'MultipleChoice' | 'TextInput' | 'NumberInput';
  isRequired: boolean;
  maxSelections: number;
  sortOrder: number;
  isActive: boolean;
  optionValues: ProductOptionValue[];
}

interface ProductOptionSelectorProps {
  options: ProductOption[];
  selectedOptions: Record<string, string | string[] | number>;
  onOptionChange: (optionId: string, value: string | string[] | number) => void;
  disabled?: boolean;
}

const ProductOptionSelector: React.FC<ProductOptionSelectorProps> = ({
  options,
  selectedOptions,
  onOptionChange,
  disabled = false
}) => {
  const { t } = useTranslation();

  // Aktif seçenekleri sırala
  const activeOptions = options
    .filter(o => o.isActive)
    .sort((a, b) => a.sortOrder - b.sortOrder);

  const handleSingleChoice = (optionId: string, valueId: string) => {
    if (!disabled) {
      onOptionChange(optionId, valueId);
    }
  };

  const handleMultipleChoice = (optionId: string, valueId: string) => {
    if (!disabled) {
      const currentValues = (selectedOptions[optionId] as string[]) || [];
      const newValues = currentValues.includes(valueId)
        ? currentValues.filter(v => v !== valueId)
        : [...currentValues, valueId];
      
      onOptionChange(optionId, newValues);
    }
  };

  const handleTextInput = (optionId: string, value: string) => {
    if (!disabled) {
      onOptionChange(optionId, value);
    }
  };

  const handleNumberInput = (optionId: string, value: string) => {
    if (!disabled) {
      const numValue = parseInt(value) || 0;
      onOptionChange(optionId, numValue);
    }
  };

  const renderSingleChoice = (option: ProductOption) => {
    const selectedValue = selectedOptions[option.id] as string;
    const activeValues = option.optionValues.filter(v => v.isActive).sort((a, b) => a.sortOrder - b.sortOrder);

    return (
      <View key={option.id} style={styles.optionContainer}>
        <Text style={styles.optionTitle}>
          {option.name}
          {option.isRequired && <Text style={styles.required}> *</Text>}
        </Text>
        {option.description && (
          <Text style={styles.optionDescription}>{option.description}</Text>
        )}
        <View style={styles.valuesContainer}>
          {activeValues.map((value) => (
            <TouchableOpacity
              key={value.id}
              style={[
                styles.valueButton,
                {
                  backgroundColor: selectedValue === value.id ? '#1976d2' : '#f5f5f5',
                  borderColor: selectedValue === value.id ? '#1976d2' : '#ddd',
                }
              ]}
              onPress={() => handleSingleChoice(option.id, value.id)}
              disabled={disabled}
              activeOpacity={0.7}
            >
              <Text style={[
                styles.valueText,
                { color: selectedValue === value.id ? '#fff' : '#333' }
              ]}>
                {value.value}
              </Text>
              {value.priceModifier !== 0 && (
                <Text style={[
                  styles.priceModifier,
                  { color: selectedValue === value.id ? '#fff' : (value.priceModifier > 0 ? '#d32f2f' : '#388e3c') }
                ]}>
                  {value.priceModifier > 0 ? '+' : ''}{value.priceModifier.toFixed(2)} €
                </Text>
              )}
            </TouchableOpacity>
          ))}
        </View>
      </View>
    );
  };

  const renderMultipleChoice = (option: ProductOption) => {
    const selectedValues = (selectedOptions[option.id] as string[]) || [];
    const activeValues = option.optionValues.filter(v => v.isActive).sort((a, b) => a.sortOrder - b.sortOrder);

    return (
      <View key={option.id} style={styles.optionContainer}>
        <Text style={styles.optionTitle}>
          {option.name}
          {option.isRequired && <Text style={styles.required}> *</Text>}
        </Text>
        {option.description && (
          <Text style={styles.optionDescription}>{option.description}</Text>
        )}
        <View style={styles.valuesContainer}>
          {activeValues.map((value) => {
            const isSelected = selectedValues.includes(value.id);
            return (
              <TouchableOpacity
                key={value.id}
                style={[
                  styles.valueButton,
                  {
                    backgroundColor: isSelected ? '#1976d2' : '#f5f5f5',
                    borderColor: isSelected ? '#1976d2' : '#ddd',
                  }
                ]}
                onPress={() => handleMultipleChoice(option.id, value.id)}
                disabled={disabled}
                activeOpacity={0.7}
              >
                <Ionicons
                  name={isSelected ? 'checkbox' : 'square-outline'}
                  size={16}
                  color={isSelected ? '#fff' : '#666'}
                  style={styles.checkbox}
                />
                <Text style={[
                  styles.valueText,
                  { color: isSelected ? '#fff' : '#333' }
                ]}>
                  {value.value}
                </Text>
                {value.priceModifier !== 0 && (
                  <Text style={[
                    styles.priceModifier,
                    { color: isSelected ? '#fff' : (value.priceModifier > 0 ? '#d32f2f' : '#388e3c') }
                  ]}>
                    {value.priceModifier > 0 ? '+' : ''}{value.priceModifier.toFixed(2)} €
                  </Text>
                )}
              </TouchableOpacity>
            );
          })}
        </View>
      </View>
    );
  };

  const renderTextInput = (option: ProductOption) => {
    const currentValue = (selectedOptions[option.id] as string) || '';

    return (
      <View key={option.id} style={styles.optionContainer}>
        <Text style={styles.optionTitle}>
          {option.name}
          {option.isRequired && <Text style={styles.required}> *</Text>}
        </Text>
        {option.description && (
          <Text style={styles.optionDescription}>{option.description}</Text>
        )}
        <TextInput
          style={styles.textInput}
          value={currentValue}
          onChangeText={(text) => handleTextInput(option.id, text)}
          placeholder={t('product.enterText', 'Metin girin...')}
          editable={!disabled}
          multiline
          numberOfLines={2}
        />
      </View>
    );
  };

  const renderNumberInput = (option: ProductOption) => {
    const currentValue = (selectedOptions[option.id] as number) || 0;

    return (
      <View key={option.id} style={styles.optionContainer}>
        <Text style={styles.optionTitle}>
          {option.name}
          {option.isRequired && <Text style={styles.required}> *</Text>}
        </Text>
        {option.description && (
          <Text style={styles.optionDescription}>{option.description}</Text>
        )}
        <View style={styles.numberInputContainer}>
          <TouchableOpacity
            style={styles.numberButton}
            onPress={() => handleNumberInput(option.id, (currentValue - 1).toString())}
            disabled={disabled || currentValue <= 0}
          >
            <Ionicons name="remove" size={20} color="#666" />
          </TouchableOpacity>
          <Text style={styles.numberValue}>{currentValue}</Text>
          <TouchableOpacity
            style={styles.numberButton}
            onPress={() => handleNumberInput(option.id, (currentValue + 1).toString())}
            disabled={disabled}
          >
            <Ionicons name="add" size={20} color="#666" />
          </TouchableOpacity>
        </View>
      </View>
    );
  };

  const renderOption = (option: ProductOption) => {
    switch (option.optionType) {
      case 'SingleChoice':
        return renderSingleChoice(option);
      case 'MultipleChoice':
        return renderMultipleChoice(option);
      case 'TextInput':
        return renderTextInput(option);
      case 'NumberInput':
        return renderNumberInput(option);
      default:
        return null;
    }
  };

  if (activeOptions.length === 0) {
    return null;
  }

  return (
    <ScrollView style={styles.container} showsVerticalScrollIndicator={false}>
      {activeOptions.map(renderOption)}
    </ScrollView>
  );
};

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  optionContainer: {
    marginBottom: 16,
    paddingHorizontal: 4,
  },
  optionTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 4,
  },
  required: {
    color: '#d32f2f',
  },
  optionDescription: {
    fontSize: 14,
    color: '#666',
    marginBottom: 8,
  },
  valuesContainer: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: 8,
  },
  valueButton: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderRadius: 6,
    borderWidth: 1,
    minHeight: 40,
  },
  checkbox: {
    marginRight: 6,
  },
  valueText: {
    fontSize: 14,
    fontWeight: '500',
  },
  priceModifier: {
    fontSize: 12,
    fontWeight: '500',
    marginLeft: 4,
  },
  textInput: {
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 6,
    padding: 12,
    fontSize: 14,
    backgroundColor: '#fff',
    minHeight: 60,
  },
  numberInputContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 16,
  },
  numberButton: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: '#f5f5f5',
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
    borderColor: '#ddd',
  },
  numberValue: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
    minWidth: 30,
    textAlign: 'center',
  },
});

export default ProductOptionSelector; 