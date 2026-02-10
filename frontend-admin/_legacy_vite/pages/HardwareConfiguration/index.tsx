import React, { useEffect, useState } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Button,
  TextField,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Alert,
  CircularProgress,
  Switch,
  FormControlLabel,
  Divider,
  Grid,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import api from '../../services/api';

interface HardwareConfig {
  tseDeviceId: string;
  tseSerialNumber: string;
  printerName: string;
  printerPort: string;
  autoConnect: boolean;
  connectionTimeout: number;
  retryAttempts: number;
}

interface HardwareStatus {
  tseConnected: boolean;
  printerConnected: boolean;
  tseSerialNumber: string;
  printerName: string;
  lastConnectionTime: string;
}

const HardwareConfiguration: React.FC = () => {
  const { t } = useTranslation();
  const [config, setConfig] = useState<HardwareConfig>({
    tseDeviceId: '',
    tseSerialNumber: '',
    printerName: '',
    printerPort: '',
    autoConnect: false,
    connectionTimeout: 30,
    retryAttempts: 3,
  });
  const [status, setStatus] = useState<HardwareStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [availablePrinters, setAvailablePrinters] = useState<string[]>([]);

  const fetchConfig = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await api.get('/api/hardware/config');
      setConfig(response.data);
    } catch (err) {
      setError(t('errors.config_load_failed'));
      console.error('Konfigürasyon yükleme hatası:', err);
    } finally {
      setLoading(false);
    }
  };

  const fetchStatus = async () => {
    try {
      const response = await api.get('/api/hardware/status');
      setStatus(response.data);
    } catch (err) {
      console.error('Donanım durumu alma hatası:', err);
    }
  };

  const fetchAvailablePrinters = async () => {
    try {
      const response = await api.get('/api/hardware/printers');
      setAvailablePrinters(response.data);
    } catch (err) {
      console.error('Yazıcı listesi alma hatası:', err);
    }
  };

  const saveConfig = async () => {
    try {
      setSaving(true);
      setError(null);
      setSuccess(null);

      const response = await api.post('/api/hardware/config', config);
      
      if (response.status === 200) {
        setSuccess(t('hardware.config_saved'));
        await fetchStatus();
      }
    } catch (err) {
      setError(t('errors.config_save_failed'));
      console.error('Konfigürasyon kaydetme hatası:', err);
    } finally {
      setSaving(false);
    }
  };

  const testConnection = async () => {
    try {
      setError(null);
      const response = await api.post('/api/hardware/test-connection');
      
      if (response.status === 200) {
        setSuccess(t('hardware.connection_test_success'));
        await fetchStatus();
      }
    } catch (err) {
      setError(t('errors.connection_test_failed'));
      console.error('Bağlantı testi hatası:', err);
    }
  };

  const connectHardware = async () => {
    try {
      setError(null);
      const response = await api.post('/api/hardware/connect');
      
      if (response.status === 200) {
        setSuccess(t('hardware.connection_success'));
        await fetchStatus();
      }
    } catch (err) {
      setError(t('errors.connection_failed'));
      console.error('Donanım bağlantı hatası:', err);
    }
  };

  const disconnectHardware = async () => {
    try {
      setError(null);
      const response = await api.post('/api/hardware/disconnect');
      
      if (response.status === 200) {
        setSuccess(t('hardware.disconnection_success'));
        await fetchStatus();
      }
    } catch (err) {
      setError(t('errors.disconnection_failed'));
      console.error('Donanım bağlantısını kesme hatası:', err);
    }
  };

  useEffect(() => {
    fetchConfig();
    fetchStatus();
    fetchAvailablePrinters();
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
        {t('hardware.configuration_title')}
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
        {/* TSE Konfigürasyonu */}
        <Box sx={{ flex: 1 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('hardware.tse_configuration')}
              </Typography>
              
              <TextField
                fullWidth
                label={t('hardware.tse_device_id')}
                value={config.tseDeviceId}
                onChange={(e) => setConfig({ ...config, tseDeviceId: e.target.value })}
                margin="normal"
                helperText={t('hardware.tse_device_id_help')}
              />

              <TextField
                fullWidth
                label={t('hardware.tse_serial_number')}
                value={config.tseSerialNumber}
                onChange={(e) => setConfig({ ...config, tseSerialNumber: e.target.value })}
                margin="normal"
                helperText={t('hardware.tse_serial_number_help')}
              />

              <FormControlLabel
                control={
                  <Switch
                    checked={config.autoConnect}
                    onChange={(e) => setConfig({ ...config, autoConnect: e.target.checked })}
                  />
                }
                label={t('hardware.auto_connect')}
                sx={{ mt: 2 }}
              />

              <TextField
                fullWidth
                type="number"
                label={t('hardware.connection_timeout')}
                value={config.connectionTimeout}
                onChange={(e) => setConfig({ ...config, connectionTimeout: parseInt(e.target.value) })}
                margin="normal"
                helperText={t('hardware.connection_timeout_help')}
              />

              <TextField
                fullWidth
                type="number"
                label={t('hardware.retry_attempts')}
                value={config.retryAttempts}
                onChange={(e) => setConfig({ ...config, retryAttempts: parseInt(e.target.value) })}
                margin="normal"
                helperText={t('hardware.retry_attempts_help')}
              />
            </CardContent>
          </Card>
        </Box>

        {/* Yazıcı Konfigürasyonu */}
        <Box sx={{ flex: 1 }}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('hardware.printer_configuration')}
              </Typography>

              <FormControl fullWidth margin="normal">
                <InputLabel>{t('hardware.printer_name')}</InputLabel>
                <Select
                  value={config.printerName}
                  onChange={(e) => setConfig({ ...config, printerName: e.target.value })}
                  label={t('hardware.printer_name')}
                >
                  {availablePrinters.map((printer) => (
                    <MenuItem key={printer} value={printer}>
                      {printer}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <TextField
                fullWidth
                label={t('hardware.printer_port')}
                value={config.printerPort}
                onChange={(e) => setConfig({ ...config, printerPort: e.target.value })}
                margin="normal"
                helperText={t('hardware.printer_port_help')}
              />
            </CardContent>
          </Card>
        </Box>
      </Box>

      {/* Donanım Durumu */}
      {status && (
        <Card sx={{ mt: 3 }}>
          <CardContent>
            <Typography variant="h6" gutterBottom>
              {t('hardware.status')}
            </Typography>
            
            <Box sx={{ display: 'flex', flexDirection: { xs: 'column', md: 'row' }, gap: 2 }}>
              <Box sx={{ flex: 1 }}>
                <Typography variant="subtitle2" color="textSecondary">
                  {t('hardware.tse_status')}
                </Typography>
                <Alert severity={status.tseConnected ? 'success' : 'error'} sx={{ mt: 1 }}>
                  {status.tseConnected ? t('hardware.tse_connected') : t('hardware.tse_disconnected')}
                </Alert>
                {status.tseSerialNumber && (
                  <Typography variant="body2" sx={{ mt: 1 }}>
                    {t('hardware.serial_number')}: {status.tseSerialNumber}
                  </Typography>
                )}
              </Box>

              <Box sx={{ flex: 1 }}>
                <Typography variant="subtitle2" color="textSecondary">
                  {t('hardware.printer_status')}
                </Typography>
                <Alert severity={status.printerConnected ? 'success' : 'error'} sx={{ mt: 1 }}>
                  {status.printerConnected ? t('hardware.printer_connected') : t('hardware.printer_disconnected')}
                </Alert>
                {status.printerName && (
                  <Typography variant="body2" sx={{ mt: 1 }}>
                    {t('hardware.printer_name')}: {status.printerName}
                  </Typography>
                )}
              </Box>
            </Box>

            {status.lastConnectionTime && (
              <Typography variant="body2" sx={{ mt: 2 }}>
                {t('hardware.last_connection')}: {new Date(status.lastConnectionTime).toLocaleString()}
              </Typography>
            )}
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
          {saving ? <CircularProgress size={24} /> : t('hardware.save_config')}
        </Button>

        <Button
          variant="outlined"
          onClick={testConnection}
        >
          {t('hardware.test_connection')}
        </Button>

        <Button
          variant="outlined"
          color="success"
          onClick={connectHardware}
        >
          {t('hardware.connect')}
        </Button>

        <Button
          variant="outlined"
          color="warning"
          onClick={disconnectHardware}
        >
          {t('hardware.disconnect')}
        </Button>
      </Box>
    </Box>
  );
};

export default HardwareConfiguration; 