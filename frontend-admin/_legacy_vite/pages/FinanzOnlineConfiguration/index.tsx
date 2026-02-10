import React, { useEffect, useState } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Button,
  TextField,
  Alert,
  CircularProgress,
  Switch,
  FormControlLabel,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import api from '../../services/api';

interface FinanzOnlineConfig {
  apiUrl: string;
  username: string;
  password: string;
  autoSubmit: boolean;
  submitInterval: number;
  retryAttempts: number;
  enableValidation: boolean;
}

interface FinanzOnlineStatus {
  isConnected: boolean;
  apiVersion: string;
  lastSync: string;
  pendingInvoices: number;
  pendingReports: number;
}

interface FinanzOnlineError {
  code: string;
  message: string;
  timestamp: string;
  invoiceNumber: string;
}

const FinanzOnlineConfiguration: React.FC = () => {
  const { t } = useTranslation();
  const [config, setConfig] = useState<FinanzOnlineConfig>({
    apiUrl: '',
    username: '',
    password: '',
    autoSubmit: false,
    submitInterval: 60,
    retryAttempts: 3,
    enableValidation: true,
  });
  const [status, setStatus] = useState<FinanzOnlineStatus | null>(null);
  const [errors, setErrors] = useState<FinanzOnlineError[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const fetchConfig = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await api.get('/api/finanzonline/config');
      setConfig(response.data);
    } catch (err) {
      setError(t('errors.config_load_failed'));
      console.error('FinanzOnline konfigürasyon yükleme hatası:', err);
    } finally {
      setLoading(false);
    }
  };

  const fetchStatus = async () => {
    try {
      const response = await api.get('/api/finanzonline/status');
      setStatus(response.data);
    } catch (err) {
      console.error('FinanzOnline durum alma hatası:', err);
    }
  };

  const fetchErrors = async () => {
    try {
      const response = await api.get('/api/finanzonline/errors');
      setErrors(response.data);
    } catch (err) {
      console.error('FinanzOnline hatalar alma hatası:', err);
    }
  };

  const saveConfig = async () => {
    try {
      setSaving(true);
      setError(null);
      setSuccess(null);

      const response = await api.post('/api/finanzonline/config', config);
      
      if (response.status === 200) {
        setSuccess(t('finanzonline.config_saved'));
        await fetchStatus();
      }
    } catch (err) {
      setError(t('errors.config_save_failed'));
      console.error('FinanzOnline konfigürasyon kaydetme hatası:', err);
    } finally {
      setSaving(false);
    }
  };

  const testConnection = async () => {
    try {
      setTesting(true);
      setError(null);
      setSuccess(null);

      const response = await api.post('/api/finanzonline/test-connection');
      
      if (response.status === 200) {
        setSuccess(t('finanzonline.connection_test_success'));
        await fetchStatus();
      }
    } catch (err) {
      setError(t('errors.connection_test_failed'));
      console.error('FinanzOnline bağlantı testi hatası:', err);
    } finally {
      setTesting(false);
    }
  };

  const authenticate = async () => {
    try {
      setError(null);
      setSuccess(null);

      const response = await api.post('/api/finanzonline/authenticate');
      
      if (response.status === 200) {
        setSuccess(t('finanzonline.authentication_success'));
        await fetchStatus();
      }
    } catch (err) {
      setError(t('errors.authentication_failed'));
      console.error('FinanzOnline kimlik doğrulama hatası:', err);
    }
  };

  const submitPendingData = async () => {
    try {
      setError(null);
      setSuccess(null);

      const response = await api.post('/api/finanzonline/submit-pending');
      
      if (response.status === 200) {
        setSuccess(t('finanzonline.submit_success'));
        await fetchStatus();
        await fetchErrors();
      }
    } catch (err) {
      setError(t('errors.submit_failed'));
      console.error('FinanzOnline veri gönderme hatası:', err);
    }
  };

  useEffect(() => {
    fetchConfig();
    fetchStatus();
    fetchErrors();
  }, []);

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="400px">
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box p={3}>
      <Typography variant="h4" gutterBottom>
        {t('finanzonline.configuration_title')}
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {success && (
        <Alert severity="success" sx={{ mb: 2 }}>
          {success}
        </Alert>
      )}

      <Box sx={{ display: 'flex', flexDirection: { xs: 'column', md: 'row' }, gap: 3 }}>
        {/* Konfigürasyon */}
        <Box sx={{ flex: 1 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('finanzonline.api_configuration')}
              </Typography>
              
              <TextField
                fullWidth
                label={t('finanzonline.api_url')}
                value={config.apiUrl}
                onChange={(e) => setConfig({ ...config, apiUrl: e.target.value })}
                margin="normal"
                helperText={t('finanzonline.api_url_help')}
              />

              <TextField
                fullWidth
                label={t('finanzonline.username')}
                value={config.username}
                onChange={(e) => setConfig({ ...config, username: e.target.value })}
                margin="normal"
              />

              <TextField
                fullWidth
                type="password"
                label={t('finanzonline.password')}
                value={config.password}
                onChange={(e) => setConfig({ ...config, password: e.target.value })}
                margin="normal"
              />

              <FormControlLabel
                control={
                  <Switch
                    checked={config.autoSubmit}
                    onChange={(e) => setConfig({ ...config, autoSubmit: e.target.checked })}
                  />
                }
                label={t('finanzonline.auto_submit')}
                sx={{ mt: 2 }}
              />

              <TextField
                fullWidth
                type="number"
                label={t('finanzonline.submit_interval')}
                value={config.submitInterval}
                onChange={(e) => setConfig({ ...config, submitInterval: parseInt(e.target.value) })}
                margin="normal"
                helperText={t('finanzonline.submit_interval_help')}
              />

              <TextField
                fullWidth
                type="number"
                label={t('finanzonline.retry_attempts')}
                value={config.retryAttempts}
                onChange={(e) => setConfig({ ...config, retryAttempts: parseInt(e.target.value) })}
                margin="normal"
                helperText={t('finanzonline.retry_attempts_help')}
              />

              <FormControlLabel
                control={
                  <Switch
                    checked={config.enableValidation}
                    onChange={(e) => setConfig({ ...config, enableValidation: e.target.checked })}
                  />
                }
                label={t('finanzonline.enable_validation')}
                sx={{ mt: 2 }}
              />
            </CardContent>
          </Card>
        </Box>

        {/* Durum */}
        <Box sx={{ flex: 1 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('finanzonline.status')}
              </Typography>
              
              {status && (
                <Box>
                  <Alert severity={status.isConnected ? 'success' : 'error'} sx={{ mb: 2 }}>
                    {status.isConnected ? t('finanzonline.connected') : t('finanzonline.disconnected')}
                  </Alert>

                  <Typography variant="body2" sx={{ mb: 1 }}>
                    <strong>{t('finanzonline.api_version')}:</strong> {status.apiVersion}
                  </Typography>

                  <Typography variant="body2" sx={{ mb: 1 }}>
                    <strong>{t('finanzonline.last_sync')}:</strong> {status.lastSync ? new Date(status.lastSync).toLocaleString() : t('finanzonline.never')}
                  </Typography>

                  <Typography variant="body2" sx={{ mb: 1 }}>
                    <strong>{t('finanzonline.pending_invoices')}:</strong> {status.pendingInvoices}
                  </Typography>

                  <Typography variant="body2" sx={{ mb: 1 }}>
                    <strong>{t('finanzonline.pending_reports')}:</strong> {status.pendingReports}
                  </Typography>
                </Box>
              )}
            </CardContent>
          </Card>
        </Box>
      </Box>

      {/* Hata Listesi */}
      {errors.length > 0 && (
        <Card sx={{ mt: 3 }}>
          <CardContent>
            <Typography variant="h6" gutterBottom>
              {t('finanzonline.errors')} ({errors.length})
            </Typography>
            
            <TableContainer component={Paper}>
              <Table>
                <TableHead>
                  <TableRow>
                    <TableCell>{t('finanzonline.error_code')}</TableCell>
                    <TableCell>{t('finanzonline.error_message')}</TableCell>
                    <TableCell>{t('finanzonline.timestamp')}</TableCell>
                    <TableCell>{t('finanzonline.invoice_number')}</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {errors.map((error, index) => (
                    <TableRow key={index}>
                      <TableCell>
                        <Chip label={error.code} color="error" size="small" />
                      </TableCell>
                      <TableCell>{error.message}</TableCell>
                      <TableCell>{new Date(error.timestamp).toLocaleString()}</TableCell>
                      <TableCell>{error.invoiceNumber}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          </CardContent>
        </Card>
      )}

      {/* Aksiyon Butonları */}
      <Box sx={{ mt: 3, display: 'flex', gap: 2, flexWrap: 'wrap' }}>
        <Button
          variant="contained"
          color="primary"
          onClick={saveConfig}
          disabled={saving}
        >
          {saving ? <CircularProgress size={24} /> : t('finanzonline.save_config')}
        </Button>

        <Button
          variant="outlined"
          onClick={testConnection}
          disabled={testing}
        >
          {testing ? <CircularProgress size={24} /> : t('finanzonline.test_connection')}
        </Button>

        <Button
          variant="outlined"
          color="success"
          onClick={authenticate}
        >
          {t('finanzonline.authenticate')}
        </Button>

        <Button
          variant="outlined"
          color="warning"
          onClick={submitPendingData}
        >
          {t('finanzonline.submit_pending')}
        </Button>
      </Box>
    </Box>
  );
};

export default FinanzOnlineConfiguration; 