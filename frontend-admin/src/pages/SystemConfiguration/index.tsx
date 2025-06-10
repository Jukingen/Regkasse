import React, { useState, useEffect } from 'react';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Button,
  Grid,
  Alert,
  CircularProgress,
  FormControl,
  FormControlLabel,
  Radio,
  RadioGroup,
  Switch,
  TextField,
  Divider,
  Chip,
} from '@mui/material';
import { useTranslation } from 'react-i18next';
import api from '../../services/api';

interface SystemConfig {
  operationMode: 'online-only' | 'offline-only' | 'hybrid';
  offlineSettings: {
    enabled: boolean;
    syncInterval: number; // dakika
    maxOfflineDays: number;
    autoSync: boolean;
  };
  tseSettings: {
    required: boolean;
    offlineAllowed: boolean;
    maxOfflineTransactions: number;
  };
  printerSettings: {
    required: boolean;
    offlineQueue: boolean;
    maxQueueSize: number;
  };
}

const SystemConfiguration: React.FC = () => {
  const { t } = useTranslation();
  const [config, setConfig] = useState<SystemConfig | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  useEffect(() => {
    loadConfiguration();
  }, []);

  const loadConfiguration = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await api.get('/api/system/config');
      setConfig(response.data);
    } catch (err) {
      setError(t('errors.config_load_failed'));
      console.error('Konfigürasyon yüklenemedi:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async () => {
    if (!config) return;

    try {
      setSaving(true);
      setError(null);
      setSuccess(null);

      await api.put('/api/system/config', config);
      setSuccess(t('system.config_saved'));
    } catch (err) {
      setError(t('errors.config_save_failed'));
      console.error('Konfigürasyon kaydedilemedi:', err);
    } finally {
      setSaving(false);
    }
  };

  const handleModeChange = (mode: 'online-only' | 'offline-only' | 'hybrid') => {
    if (!config) return;

    setConfig({
      ...config,
      operationMode: mode,
      // Mod değişikliğinde otomatik ayarlar
      offlineSettings: {
        ...config.offlineSettings,
        enabled: mode !== 'online-only'
      },
      tseSettings: {
        ...config.tseSettings,
        offlineAllowed: mode !== 'online-only'
      },
      printerSettings: {
        ...config.printerSettings,
        offlineQueue: mode !== 'online-only'
      }
    });
  };

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="400px">
        <CircularProgress />
      </Box>
    );
  }

  if (!config) {
    return (
      <Alert severity="error">
        {t('errors.config_not_found')}
      </Alert>
    );
  }

  return (
    <Box p={3}>
      <Typography variant="h4" gutterBottom>
        {t('system.configuration_title')}
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

      <Grid container spacing={3}>
        {/* Operasyon Modu */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('system.operation_mode')}
              </Typography>
              
              <FormControl component="fieldset">
                <RadioGroup
                  value={config.operationMode}
                  onChange={(e) => handleModeChange(e.target.value as any)}
                >
                  <FormControlLabel
                    value="online-only"
                    control={<Radio />}
                    label={
                      <Box>
                        <Typography variant="subtitle1" fontWeight="bold">
                          {t('system.online_only')}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          {t('system.online_only_desc')}
                        </Typography>
                        <Chip 
                          label={t('system.recommended')} 
                          color="success" 
                          size="small" 
                          sx={{ mt: 1 }}
                        />
                      </Box>
                    }
                  />
                  
                  <FormControlLabel
                    value="hybrid"
                    control={<Radio />}
                    label={
                      <Box>
                        <Typography variant="subtitle1" fontWeight="bold">
                          {t('system.hybrid_mode')}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          {t('system.hybrid_mode_desc')}
                        </Typography>
                        <Chip 
                          label={t('system.advanced')} 
                          color="warning" 
                          size="small" 
                          sx={{ mt: 1 }}
                        />
                      </Box>
                    }
                  />
                  
                  <FormControlLabel
                    value="offline-only"
                    control={<Radio />}
                    label={
                      <Box>
                        <Typography variant="subtitle1" fontWeight="bold">
                          {t('system.offline_only')}
                        </Typography>
                        <Typography variant="body2" color="text.secondary">
                          {t('system.offline_only_desc')}
                        </Typography>
                        <Chip 
                          label={t('system.experimental')} 
                          color="error" 
                          size="small" 
                          sx={{ mt: 1 }}
                        />
                      </Box>
                    }
                  />
                </RadioGroup>
              </FormControl>
            </CardContent>
          </Card>
        </Grid>

        {/* Çevrimdışı Ayarları */}
        {config.operationMode !== 'online-only' && (
          <Grid item xs={12} md={6}>
            <Card>
              <CardContent>
                <Typography variant="h6" gutterBottom>
                  {t('system.offline_settings')}
                </Typography>
                
                <Box sx={{ mb: 2 }}>
                  <FormControlLabel
                    control={
                      <Switch
                        checked={config.offlineSettings.enabled}
                        onChange={(e) => setConfig({
                          ...config,
                          offlineSettings: {
                            ...config.offlineSettings,
                            enabled: e.target.checked
                          }
                        })}
                      />
                    }
                    label={t('system.enable_offline_mode')}
                  />
                </Box>

                {config.offlineSettings.enabled && (
                  <>
                    <TextField
                      fullWidth
                      label={t('system.sync_interval_minutes')}
                      type="number"
                      value={config.offlineSettings.syncInterval}
                      onChange={(e) => setConfig({
                        ...config,
                        offlineSettings: {
                          ...config.offlineSettings,
                          syncInterval: parseInt(e.target.value) || 5
                        }
                      })}
                      sx={{ mb: 2 }}
                    />

                    <TextField
                      fullWidth
                      label={t('system.max_offline_days')}
                      type="number"
                      value={config.offlineSettings.maxOfflineDays}
                      onChange={(e) => setConfig({
                        ...config,
                        offlineSettings: {
                          ...config.offlineSettings,
                          maxOfflineDays: parseInt(e.target.value) || 7
                        }
                      })}
                      sx={{ mb: 2 }}
                    />

                    <FormControlLabel
                      control={
                        <Switch
                          checked={config.offlineSettings.autoSync}
                          onChange={(e) => setConfig({
                            ...config,
                            offlineSettings: {
                              ...config.offlineSettings,
                              autoSync: e.target.checked
                            }
                          })}
                        />
                      }
                      label={t('system.auto_sync')}
                    />
                  </>
                )}
              </CardContent>
            </Card>
          </Grid>
        )}

        {/* TSE Ayarları */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('system.tse_settings')}
              </Typography>
              
              <FormControlLabel
                control={
                  <Switch
                    checked={config.tseSettings.required}
                    onChange={(e) => setConfig({
                      ...config,
                      tseSettings: {
                        ...config.tseSettings,
                        required: e.target.checked
                      }
                    })}
                  />
                }
                label={t('system.tse_required')}
              />

              {config.operationMode !== 'online-only' && (
                <>
                  <FormControlLabel
                    control={
                      <Switch
                        checked={config.tseSettings.offlineAllowed}
                        onChange={(e) => setConfig({
                          ...config,
                          tseSettings: {
                            ...config.tseSettings,
                            offlineAllowed: e.target.checked
                          }
                        })}
                      />
                    }
                    label={t('system.tse_offline_allowed')}
                  />

                  <TextField
                    fullWidth
                    label={t('system.max_offline_transactions')}
                    type="number"
                    value={config.tseSettings.maxOfflineTransactions}
                    onChange={(e) => setConfig({
                      ...config,
                      tseSettings: {
                        ...config.tseSettings,
                        maxOfflineTransactions: parseInt(e.target.value) || 100
                      }
                    })}
                    sx={{ mt: 2 }}
                  />
                </>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Yazıcı Ayarları */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                {t('system.printer_settings')}
              </Typography>
              
              <FormControlLabel
                control={
                  <Switch
                    checked={config.printerSettings.required}
                    onChange={(e) => setConfig({
                      ...config,
                      printerSettings: {
                        ...config.printerSettings,
                        required: e.target.checked
                      }
                    })}
                  />
                }
                label={t('system.printer_required')}
              />

              {config.operationMode !== 'online-only' && (
                <>
                  <FormControlLabel
                    control={
                      <Switch
                        checked={config.printerSettings.offlineQueue}
                        onChange={(e) => setConfig({
                          ...config,
                          printerSettings: {
                            ...config.printerSettings,
                            offlineQueue: e.target.checked
                          }
                        })}
                      />
                    }
                    label={t('system.offline_print_queue')}
                  />

                  <TextField
                    fullWidth
                    label={t('system.max_queue_size')}
                    type="number"
                    value={config.printerSettings.maxQueueSize}
                    onChange={(e) => setConfig({
                      ...config,
                      printerSettings: {
                        ...config.printerSettings,
                        maxQueueSize: parseInt(e.target.value) || 50
                      }
                    })}
                    sx={{ mt: 2 }}
                  />
                </>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      <Divider sx={{ my: 3 }} />

      <Box sx={{ display: 'flex', gap: 2, justifyContent: 'flex-end' }}>
        <Button
          variant="outlined"
          onClick={loadConfiguration}
          disabled={saving}
        >
          {t('common.reset')}
        </Button>
        
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={saving}
        >
          {saving ? (
            <>
              <CircularProgress size={20} sx={{ mr: 1 }} />
              {t('common.saving')}
            </>
          ) : (
            t('common.save')
          )}
        </Button>
      </Box>
    </Box>
  );
};

export default SystemConfiguration; 