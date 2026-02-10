// Türkçe Açıklama: Merkezi kategori yönetimi ve backend ile senkronize çalışan dinamik menü sistemi. Kategorileri yönetir ve ürünleri kategorilere göre filtreler.

import React, { useState, useEffect, useCallback } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, ScrollView, Modal, TextInput, Alert } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTranslation } from 'react-i18next';

export interface Category {
  id: string;
  name: string;
  description?: string;
  color?: string;
  icon?: string;
  sortOrder: number;
  isActive: boolean;
  productCount?: number;
}

interface CategoryManagerProps {
  categories: Category[];
  selectedCategoryId?: string;
  onCategorySelect: (category: Category) => void;
  onCategoryUpdate?: (category: Category) => Promise<void>;
  onCategoryCreate?: (category: Omit<Category, 'id'>) => Promise<void>;
  onCategoryDelete?: (categoryId: string) => Promise<void>;
  canManage?: boolean;
  showProductCount?: boolean;
}

const CategoryManager: React.FC<CategoryManagerProps> = ({
  categories,
  selectedCategoryId,
  onCategorySelect,
  onCategoryUpdate,
  onCategoryCreate,
  onCategoryDelete,
  canManage = false,
  showProductCount = true
}) => {
  const { t } = useTranslation();
  const [showManageModal, setShowManageModal] = useState(false);
  const [editingCategory, setEditingCategory] = useState<Category | null>(null);
  const [newCategory, setNewCategory] = useState<Omit<Category, 'id'>>({
    name: '',
    description: '',
    color: '#1976d2',
    icon: 'folder',
    sortOrder: 0,
    isActive: true
  });

  // Aktif kategorileri sırala
  const activeCategories = categories
    .filter(c => c.isActive)
    .sort((a, b) => a.sortOrder - b.sortOrder);

  // Tüm kategorileri sırala (yönetim için)
  const allCategories = categories.sort((a, b) => a.sortOrder - b.sortOrder);

  const handleCategoryPress = useCallback((category: Category) => {
    onCategorySelect(category);
  }, [onCategorySelect]);

  const handleEditCategory = useCallback((category: Category) => {
    setEditingCategory(category);
    setShowManageModal(true);
  }, []);

  const handleCreateCategory = useCallback(() => {
    setEditingCategory(null);
    setNewCategory({
      name: '',
      description: '',
      color: '#1976d2',
      icon: 'folder',
      sortOrder: allCategories.length,
      isActive: true
    });
    setShowManageModal(true);
  }, [allCategories.length]);

  const handleDeleteCategory = useCallback(async (category: Category) => {
    if (!onCategoryDelete) return;

    Alert.alert(
      t('category.deleteTitle', 'Kategoriyi Sil'),
      t('category.deleteMessage', 'Bu kategoriyi silmek istediğinizden emin misiniz?'),
      [
        { text: t('common.cancel', 'İptal'), style: 'cancel' },
        {
          text: t('common.delete', 'Sil'),
          style: 'destructive',
          onPress: async () => {
            try {
              await onCategoryDelete(category.id);
            } catch (error) {
              Alert.alert(t('common.error', 'Hata'), t('category.deleteError', 'Kategori silinemedi'));
            }
          }
        }
      ]
    );
  }, [onCategoryDelete, t]);

  const handleSaveCategory = useCallback(async () => {
    try {
      if (editingCategory && onCategoryUpdate) {
        await onCategoryUpdate(editingCategory);
      } else if (!editingCategory && onCategoryCreate) {
        await onCategoryCreate(newCategory);
      }
      setShowManageModal(false);
      setEditingCategory(null);
    } catch (error) {
      Alert.alert(t('common.error', 'Hata'), t('category.saveError', 'Kategori kaydedilemedi'));
    }
  }, [editingCategory, newCategory, onCategoryUpdate, onCategoryCreate, t]);

  const renderCategoryButton = useCallback((category: Category) => {
    const isSelected = selectedCategoryId === category.id;
    
    return (
      <TouchableOpacity
        key={category.id}
        style={[
          styles.categoryButton,
          {
            backgroundColor: isSelected ? (category.color || '#1976d2') : '#f8f9fa',
            borderColor: category.color || '#1976d2',
          }
        ]}
        onPress={() => handleCategoryPress(category)}
        activeOpacity={0.7}
      >
        {category.icon && (
          <Ionicons
            name={category.icon as any}
            size={20}
            color={isSelected ? '#fff' : (category.color || '#1976d2')}
            style={styles.categoryIcon}
          />
        )}
        <Text style={[
          styles.categoryName,
          { color: isSelected ? '#fff' : '#333' }
        ]}>
          {category.name}
        </Text>
        {showProductCount && category.productCount !== undefined && (
          <Text style={[
            styles.productCount,
            { color: isSelected ? '#fff' : '#666' }
          ]}>
            ({category.productCount})
          </Text>
        )}
      </TouchableOpacity>
    );
  }, [selectedCategoryId, handleCategoryPress, showProductCount]);

  const renderManageModal = () => (
    <Modal
      visible={showManageModal}
      animationType="slide"
      transparent
      onRequestClose={() => setShowManageModal(false)}
    >
      <View style={styles.modalOverlay}>
        <View style={styles.modalContent}>
          <View style={styles.modalHeader}>
            <Text style={styles.modalTitle}>
              {editingCategory 
                ? t('category.editTitle', 'Kategori Düzenle')
                : t('category.createTitle', 'Yeni Kategori')
              }
            </Text>
            <TouchableOpacity
              onPress={() => setShowManageModal(false)}
              style={styles.closeButton}
            >
              <Ionicons name="close" size={24} color="#666" />
            </TouchableOpacity>
          </View>

          <ScrollView style={styles.modalBody}>
            <View style={styles.inputGroup}>
              <Text style={styles.inputLabel}>{t('category.name', 'Kategori Adı')} *</Text>
              <TextInput
                style={styles.textInput}
                value={editingCategory?.name || newCategory.name}
                onChangeText={(text) => {
                  if (editingCategory) {
                    setEditingCategory({ ...editingCategory, name: text });
                  } else {
                    setNewCategory({ ...newCategory, name: text });
                  }
                }}
                placeholder={t('category.namePlaceholder', 'Kategori adını girin')}
              />
            </View>

            <View style={styles.inputGroup}>
              <Text style={styles.inputLabel}>{t('category.description', 'Açıklama')}</Text>
              <TextInput
                style={[styles.textInput, styles.textArea]}
                value={editingCategory?.description || newCategory.description}
                onChangeText={(text) => {
                  if (editingCategory) {
                    setEditingCategory({ ...editingCategory, description: text });
                  } else {
                    setNewCategory({ ...newCategory, description: text });
                  }
                }}
                placeholder={t('category.descriptionPlaceholder', 'Kategori açıklaması')}
                multiline
                numberOfLines={3}
              />
            </View>

            <View style={styles.inputGroup}>
              <Text style={styles.inputLabel}>{t('category.color', 'Renk')}</Text>
              <View style={styles.colorPicker}>
                {['#1976d2', '#d32f2f', '#388e3c', '#f57c00', '#7b1fa2', '#c2185b'].map((color) => (
                  <TouchableOpacity
                    key={color}
                    style={[
                      styles.colorOption,
                      {
                        backgroundColor: color,
                        borderColor: (editingCategory?.color || newCategory.color) === color ? '#333' : 'transparent',
                        borderWidth: (editingCategory?.color || newCategory.color) === color ? 3 : 1,
                      }
                    ]}
                    onPress={() => {
                      if (editingCategory) {
                        setEditingCategory({ ...editingCategory, color });
                      } else {
                        setNewCategory({ ...newCategory, color });
                      }
                    }}
                  />
                ))}
              </View>
            </View>

            <View style={styles.inputGroup}>
              <Text style={styles.inputLabel}>{t('category.sortOrder', 'Sıralama')}</Text>
              <TextInput
                style={styles.textInput}
                value={String(editingCategory?.sortOrder || newCategory.sortOrder)}
                onChangeText={(text) => {
                  const order = parseInt(text) || 0;
                  if (editingCategory) {
                    setEditingCategory({ ...editingCategory, sortOrder: order });
                  } else {
                    setNewCategory({ ...newCategory, sortOrder: order });
                  }
                }}
                keyboardType="numeric"
                placeholder="0"
              />
            </View>
          </ScrollView>

          <View style={styles.modalFooter}>
            <TouchableOpacity
              style={styles.cancelButton}
              onPress={() => setShowManageModal(false)}
            >
              <Text style={styles.cancelButtonText}>{t('common.cancel', 'İptal')}</Text>
            </TouchableOpacity>
            <TouchableOpacity
              style={styles.saveButton}
              onPress={handleSaveCategory}
            >
              <Text style={styles.saveButtonText}>{t('common.save', 'Kaydet')}</Text>
            </TouchableOpacity>
          </View>
        </View>
      </View>
    </Modal>
  );

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.title}>{t('category.title', 'Kategoriler')}</Text>
        {canManage && (
          <TouchableOpacity
            style={styles.manageButton}
            onPress={handleCreateCategory}
          >
            <Ionicons name="add" size={20} color="#fff" />
          </TouchableOpacity>
        )}
      </View>

      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        contentContainerStyle={styles.categoriesContainer}
      >
        {activeCategories.map(renderCategoryButton)}
      </ScrollView>

      {canManage && (
        <View style={styles.manageSection}>
          <Text style={styles.manageTitle}>{t('category.manage', 'Kategori Yönetimi')}</Text>
          <ScrollView style={styles.manageList}>
            {allCategories.map((category) => (
              <View key={category.id} style={styles.manageItem}>
                <View style={styles.manageItemInfo}>
                  <View style={[styles.categoryColor, { backgroundColor: category.color || '#1976d2' }]} />
                  <Text style={styles.manageItemName}>{category.name}</Text>
                  {showProductCount && category.productCount !== undefined && (
                    <Text style={styles.manageItemCount}>({category.productCount})</Text>
                  )}
                </View>
                <View style={styles.manageItemActions}>
                  <TouchableOpacity
                    style={styles.actionButton}
                    onPress={() => handleEditCategory(category)}
                  >
                    <Ionicons name="pencil" size={16} color="#1976d2" />
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={styles.actionButton}
                    onPress={() => handleDeleteCategory(category)}
                  >
                    <Ionicons name="trash" size={16} color="#d32f2f" />
                  </TouchableOpacity>
                </View>
              </View>
            ))}
          </ScrollView>
        </View>
      )}

      {renderManageModal()}
    </View>
  );
};

const styles = StyleSheet.create({
  container: {
    marginVertical: 8,
  },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
  },
  manageButton: {
    backgroundColor: '#1976d2',
    borderRadius: 20,
    width: 32,
    height: 32,
    alignItems: 'center',
    justifyContent: 'center',
  },
  categoriesContainer: {
    paddingHorizontal: 4,
    gap: 8,
  },
  categoryButton: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: 16,
    paddingVertical: 12,
    borderRadius: 20,
    borderWidth: 2,
    gap: 8,
    minWidth: 100,
  },
  categoryIcon: {
    marginRight: 4,
  },
  categoryName: {
    fontSize: 14,
    fontWeight: '600',
  },
  productCount: {
    fontSize: 12,
    fontWeight: '400',
  },
  manageSection: {
    marginTop: 16,
    borderTopWidth: 1,
    borderTopColor: '#eee',
    paddingTop: 16,
  },
  manageTitle: {
    fontSize: 16,
    fontWeight: '600',
    color: '#333',
    marginBottom: 8,
  },
  manageList: {
    maxHeight: 200,
  },
  manageItem: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 8,
    paddingHorizontal: 12,
    backgroundColor: '#f8f9fa',
    borderRadius: 6,
    marginBottom: 4,
  },
  manageItemInfo: {
    flexDirection: 'row',
    alignItems: 'center',
    flex: 1,
  },
  categoryColor: {
    width: 12,
    height: 12,
    borderRadius: 6,
    marginRight: 8,
  },
  manageItemName: {
    fontSize: 14,
    fontWeight: '500',
    color: '#333',
    flex: 1,
  },
  manageItemCount: {
    fontSize: 12,
    color: '#666',
  },
  manageItemActions: {
    flexDirection: 'row',
    gap: 8,
  },
  actionButton: {
    padding: 4,
  },
  modalOverlay: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.5)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  modalContent: {
    backgroundColor: '#fff',
    borderRadius: 12,
    width: '90%',
    maxHeight: '80%',
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: 16,
    borderBottomWidth: 1,
    borderBottomColor: '#eee',
  },
  modalTitle: {
    fontSize: 18,
    fontWeight: '600',
    color: '#333',
  },
  closeButton: {
    padding: 4,
  },
  modalBody: {
    padding: 16,
  },
  inputGroup: {
    marginBottom: 16,
  },
  inputLabel: {
    fontSize: 14,
    fontWeight: '500',
    color: '#333',
    marginBottom: 4,
  },
  textInput: {
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 6,
    padding: 12,
    fontSize: 14,
    backgroundColor: '#fff',
  },
  textArea: {
    minHeight: 80,
    textAlignVertical: 'top',
  },
  colorPicker: {
    flexDirection: 'row',
    gap: 8,
  },
  colorOption: {
    width: 32,
    height: 32,
    borderRadius: 16,
    borderWidth: 1,
    borderColor: '#ddd',
  },
  modalFooter: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    padding: 16,
    borderTopWidth: 1,
    borderTopColor: '#eee',
  },
  cancelButton: {
    flex: 1,
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 6,
    backgroundColor: '#f5f5f5',
    marginRight: 8,
    alignItems: 'center',
  },
  cancelButtonText: {
    fontSize: 14,
    fontWeight: '500',
    color: '#666',
  },
  saveButton: {
    flex: 1,
    paddingVertical: 12,
    paddingHorizontal: 16,
    borderRadius: 6,
    backgroundColor: '#1976d2',
    marginLeft: 8,
    alignItems: 'center',
  },
  saveButtonText: {
    fontSize: 14,
    fontWeight: '500',
    color: '#fff',
  },
});

export default CategoryManager; 