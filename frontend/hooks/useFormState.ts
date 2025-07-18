import { useState, useCallback, useRef } from 'react';
import { Alert } from 'react-native';

export interface FormField<T = any> {
  value: T;
  error: string | null;
  touched: boolean;
  isValid: boolean;
}

export interface FormState<T extends Record<string, any>> {
  values: T;
  errors: Partial<Record<keyof T, string>>;
  touched: Partial<Record<keyof T, boolean>>;
  isValid: boolean;
  isSubmitting: boolean;
  isSubmitted: boolean;
  submitCount: number;
}

export interface FormActions<T extends Record<string, any>> {
  setValue: (field: keyof T, value: T[keyof T]) => void;
  setError: (field: keyof T, error: string) => void;
  setTouched: (field: keyof T, touched: boolean) => void;
  setValues: (values: Partial<T>) => void;
  setErrors: (errors: Partial<Record<keyof T, string>>) => void;
  reset: () => void;
  validate: () => boolean;
  submit: (onSubmit: (values: T) => Promise<void>) => Promise<void>;
  getFieldProps: (field: keyof T) => {
    value: T[keyof T];
    error: string | null;
    touched: boolean;
    onChange: (value: T[keyof T]) => void;
    onBlur: () => void;
  };
}

export function useFormState<T extends Record<string, any>>(
  initialValues: T,
  validationSchema?: (values: T) => Partial<Record<keyof T, string>>,
  options: {
    showErrorAlert?: boolean;
    showSuccessAlert?: boolean;
    successMessage?: string;
    errorMessage?: string;
  } = {}
): [FormState<T>, FormActions<T>] {
  const {
    showErrorAlert = false,
    showSuccessAlert = false,
    successMessage,
    errorMessage
  } = options;

  const [state, setState] = useState<FormState<T>>({
    values: initialValues,
    errors: {},
    touched: {},
    isValid: true,
    isSubmitting: false,
    isSubmitted: false,
    submitCount: 0
  });

  const initialValuesRef = useRef(initialValues);

  const validate = useCallback((): boolean => {
    if (!validationSchema) return true;

    const errors = validationSchema(state.values);
    const hasErrors = Object.keys(errors).length > 0;

    setState(prev => ({
      ...prev,
      errors,
      isValid: !hasErrors
    }));

    return !hasErrors;
  }, [state.values, validationSchema]);

  const setValue = useCallback((field: keyof T, value: T[keyof T]) => {
    setState(prev => {
      const newValues = { ...prev.values, [field]: value };
      const newErrors = { ...prev.errors };
      
      // Field error'ını temizle
      if (newErrors[field]) {
        delete newErrors[field];
      }

      return {
        ...prev,
        values: newValues,
        errors: newErrors,
        isValid: Object.keys(newErrors).length === 0
      };
    });
  }, []);

  const setError = useCallback((field: keyof T, error: string) => {
    setState(prev => ({
      ...prev,
      errors: { ...prev.errors, [field]: error },
      isValid: false
    }));
  }, []);

  const setTouched = useCallback((field: keyof T, touched: boolean) => {
    setState(prev => ({
      ...prev,
      touched: { ...prev.touched, [field]: touched }
    }));
  }, []);

  const setValues = useCallback((values: Partial<T>) => {
    setState(prev => ({
      ...prev,
      values: { ...prev.values, ...values }
    }));
  }, []);

  const setErrors = useCallback((errors: Partial<Record<keyof T, string>>) => {
    setState(prev => ({
      ...prev,
      errors,
      isValid: Object.keys(errors).length === 0
    }));
  }, []);

  const reset = useCallback(() => {
    setState({
      values: initialValuesRef.current,
      errors: {},
      touched: {},
      isValid: true,
      isSubmitting: false,
      isSubmitted: false,
      submitCount: 0
    });
  }, []);

  const submit = useCallback(async (onSubmit: (values: T) => Promise<void>) => {
    // Form validation
    if (!validate()) {
      if (showErrorAlert) {
        Alert.alert('Validation Error', 'Please check your input and try again.');
      }
      return;
    }

    setState(prev => ({
      ...prev,
      isSubmitting: true,
      isSubmitted: false
    }));

    try {
      await onSubmit(state.values);
      
      setState(prev => ({
        ...prev,
        isSubmitting: false,
        isSubmitted: true,
        submitCount: prev.submitCount + 1
      }));

      if (showSuccessAlert && successMessage) {
        Alert.alert('Success', successMessage);
      }

    } catch (error: any) {
      const finalErrorMessage = errorMessage || error?.message || 'Submission failed';
      
      setState(prev => ({
        ...prev,
        isSubmitting: false,
        isSubmitted: false
      }));

      if (showErrorAlert) {
        Alert.alert('Error', finalErrorMessage);
      }

      throw error;
    }
  }, [state.values, validate, showErrorAlert, showSuccessAlert, successMessage, errorMessage]);

  const getFieldProps = useCallback((field: keyof T) => ({
    value: state.values[field],
    error: state.errors[field] || null,
    touched: state.touched[field] || false,
    onChange: (value: T[keyof T]) => setValue(field, value),
    onBlur: () => setTouched(field, true)
  }), [state.values, state.errors, state.touched, setValue, setTouched]);

  return [
    state,
    {
      setValue,
      setError,
      setTouched,
      setValues,
      setErrors,
      reset,
      validate,
      submit,
      getFieldProps
    }
  ];
} 