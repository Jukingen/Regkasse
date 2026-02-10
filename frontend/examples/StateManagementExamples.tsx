import React from 'react';
import { View, Text, TouchableOpacity, TextInput, ScrollView, StyleSheet } from 'react-native';

import { ErrorBoundary } from '../components/ui/ErrorBoundary';
import { LoadingOverlay } from '../components/ui/LoadingOverlay';
import { NotificationToast } from '../components/ui/NotificationToast';
import { useAppState } from '../contexts/AppStateContext';
import { useAsyncState } from '../hooks/useAsyncState';
import { useFormState } from '../hooks/useFormState';
import { useProductOperationsOptimized } from '../hooks/useProductOperationsOptimized'; // ✅ YENİ: Optimize edilmiş versiyon

// 1. Basit Async State Kullanımı
export const SimpleAsyncExample: React.FC = () => {
  const [state, actions] = useAsyncState(
    async () => {
      // Simüle edilmiş API çağrısı
      await new Promise(resolve => setTimeout(resolve, 2000));
      return { message: 'Data loaded successfully!' };
    },
    {
      showErrorAlert: true,
      showSuccessAlert: true,
      successMessage: 'Operation completed successfully!',
      errorMessage: 'Operation failed!'
    }
  );

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Simple Async State Example</Text>
      
      {state.loading && <Text>Loading...</Text>}
      {state.error && <Text style={styles.error}>Error: {state.error}</Text>}
      {state.data && <Text>Data: {state.data.message}</Text>}
      
      <TouchableOpacity 
        style={styles.button}
        onPress={() => actions.execute()}
        disabled={state.loading}
      >
        <Text style={styles.buttonText}>
          {state.loading ? 'Loading...' : 'Load Data'}
        </Text>
      </TouchableOpacity>
      
      <TouchableOpacity 
        style={styles.button}
        onPress={actions.reset}
      >
        <Text style={styles.buttonText}>Reset</Text>
      </TouchableOpacity>
    </View>
  );
};

// 2. Form State Kullanımı
export const FormStateExample: React.FC = () => {
  const validationSchema = (values: any) => {
    const errors: any = {};
    if (!values.name) errors.name = 'Name is required';
    if (!values.email) errors.email = 'Email is required';
    if (values.email && !/\S+@\S+\.\S+/.test(values.email)) {
      errors.email = 'Email is invalid';
    }
    return errors;
  };

  const [formState, formActions] = useFormState(
    { name: '', email: '', message: '' },
    validationSchema,
    {
      showErrorAlert: true,
      showSuccessAlert: true,
      successMessage: 'Form submitted successfully!'
    }
  );

  const handleSubmit = async (values: any) => {
    // Simüle edilmiş form gönderimi
    await new Promise(resolve => setTimeout(resolve, 1000));
    console.log('Form values:', values);
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Form State Example</Text>
      
      <TextInput
        style={[
          styles.input,
          formState.touched.name && formState.errors.name && styles.inputError
        ]}
        placeholder="Name"
        value={formState.values.name}
        onChangeText={(text) => formActions.setValue('name', text)}
        onBlur={() => formActions.setTouched('name', true)}
      />
      {formState.touched.name && formState.errors.name && (
        <Text style={styles.errorText}>{formState.errors.name}</Text>
      )}
      
      <TextInput
        style={[
          styles.input,
          formState.touched.email && formState.errors.email && styles.inputError
        ]}
        placeholder="Email"
        value={formState.values.email}
        onChangeText={(text) => formActions.setValue('email', text)}
        onBlur={() => formActions.setTouched('email', true)}
        keyboardType="email-address"
      />
      {formState.touched.email && formState.errors.email && (
        <Text style={styles.errorText}>{formState.errors.email}</Text>
      )}
      
      <TextInput
        style={styles.input}
        placeholder="Message (optional)"
        value={formState.values.message}
        onChangeText={(text) => formActions.setValue('message', text)}
        multiline
      />
      
      <TouchableOpacity 
        style={[
          styles.button,
          (!formState.isValid || formState.isSubmitting) && styles.buttonDisabled
        ]}
        onPress={() => formActions.submit(handleSubmit)}
        disabled={!formState.isValid || formState.isSubmitting}
      >
        <Text style={styles.buttonText}>
          {formState.isSubmitting ? 'Submitting...' : 'Submit'}
        </Text>
      </TouchableOpacity>
      
      <TouchableOpacity 
        style={styles.button}
        onPress={formActions.reset}
      >
        <Text style={styles.buttonText}>Reset Form</Text>
      </TouchableOpacity>
    </View>
  );
};

// 3. Ürün İşlemleri Kullanımı
export const ProductOperationsExample: React.FC = () => {
  const { products, loading, error, refreshProducts } = useProductOperationsOptimized(); // ✅ YENİ: Optimize edilmiş versiyon

  const handleCreateProduct = async () => {
    const newProduct = {
      name: 'Test Product',
      description: 'Test Description',
      price: 9.99,
      stockQuantity: 100,
      minStockLevel: 10,
      
      category: 'Test',
      unit: 'piece',
      taxRate: 20,
      isActive: true
    };

    // ❌ REMOVED: createProduct - API'den kaldırıldı
    console.log('createProduct removed - API cleaned up', newProduct);
  };

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Product Operations Example</Text>
      
      {products.loading && <Text>Loading products...</Text>}
      {products.error && <Text style={styles.error}>Error: {products.error}</Text>}
      {products.data && (
        <Text>Products loaded: {products.data.length}</Text>
      )}
      
      <TouchableOpacity 
        style={styles.button}
        onPress={refreshProducts}
        disabled={products.loading}
      >
        <Text style={styles.buttonText}>
          {products.loading ? 'Loading...' : 'Refresh Products'}
        </Text>
      </TouchableOpacity>
      
      <TouchableOpacity 
        style={[
          styles.button,
          create.loading && styles.buttonDisabled
        ]}
        onPress={handleCreateProduct}
        disabled={create.loading}
      >
        <Text style={styles.buttonText}>
          {create.loading ? 'Creating...' : 'Create Test Product'}
        </Text>
      </TouchableOpacity>
    </View>
  );
};

// 4. Global App State Kullanımı
export const GlobalAppStateExample: React.FC = () => {
  const { 
    globalLoading, 
    globalError, 
    globalSuccess,
    showGlobalLoading,
    hideGlobalLoading,
    showError,
    showSuccess,
    addNotification,
    clearError,
    clearSuccess
  } = useAppState();

  return (
    <View style={styles.container}>
      <Text style={styles.title}>Global App State Example</Text>
      
      <TouchableOpacity 
        style={styles.button}
        onPress={() => {
          showGlobalLoading();
          setTimeout(hideGlobalLoading, 2000);
        }}
      >
        <Text style={styles.buttonText}>Show Global Loading</Text>
      </TouchableOpacity>
      
      <TouchableOpacity 
        style={styles.button}
        onPress={() => showError('This is a test error message')}
      >
        <Text style={styles.buttonText}>Show Error</Text>
      </TouchableOpacity>
      
      <TouchableOpacity 
        style={styles.button}
        onPress={() => showSuccess('This is a test success message')}
      >
        <Text style={styles.buttonText}>Show Success</Text>
      </TouchableOpacity>
      
      <TouchableOpacity 
        style={styles.button}
        onPress={() => {
          addNotification({
            type: 'info',
            title: 'Test Notification',
            message: 'This is a test notification',
            duration: 3000
          });
        }}
      >
        <Text style={styles.buttonText}>Add Notification</Text>
      </TouchableOpacity>
      
      <TouchableOpacity 
        style={styles.button}
        onPress={() => {
          clearError();
          clearSuccess();
        }}
      >
        <Text style={styles.buttonText}>Clear Messages</Text>
      </TouchableOpacity>
      
      {globalLoading && <Text>Global loading is active</Text>}
      {globalError && <Text style={styles.error}>Global error: {globalError}</Text>}
      {globalSuccess && <Text style={styles.success}>Global success: {globalSuccess}</Text>}
    </View>
  );
};

// 5. Tam Entegre Örnek
export const CompleteExample: React.FC = () => {
  return (
    <ErrorBoundary>
      <ScrollView style={styles.scrollView}>
        <SimpleAsyncExample />
        <FormStateExample />
        <ProductOperationsExample />
        <GlobalAppStateExample />
      </ScrollView>
    </ErrorBoundary>
  );
};

const styles = StyleSheet.create({
  scrollView: {
    flex: 1,
  },
  container: {
    padding: 20,
    marginBottom: 20,
  },
  title: {
    fontSize: 20,
    fontWeight: 'bold',
    marginBottom: 16,
  },
  button: {
    backgroundColor: '#007AFF',
    padding: 12,
    borderRadius: 8,
    marginVertical: 8,
    alignItems: 'center',
  },
  buttonDisabled: {
    backgroundColor: '#999',
  },
  buttonText: {
    color: 'white',
    fontSize: 16,
    fontWeight: '600',
  },
  input: {
    borderWidth: 1,
    borderColor: '#ddd',
    borderRadius: 8,
    padding: 12,
    marginVertical: 8,
    fontSize: 16,
  },
  inputError: {
    borderColor: '#FF3B30',
  },
  error: {
    color: '#FF3B30',
    marginVertical: 8,
  },
  errorText: {
    color: '#FF3B30',
    fontSize: 14,
    marginBottom: 8,
  },
  success: {
    color: '#34C759',
    marginVertical: 8,
  },
}); 