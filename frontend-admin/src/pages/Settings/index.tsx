import { useEffect, useState } from 'react';
import { Typography, CircularProgress, Alert, Box, Card, CardContent, Grid, Chip, FormControl, InputLabel, Select, MenuItem } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';
import { useLanguage } from '../../contexts/LanguageContext';
import api from '../../services/api';

const LANG_OPTIONS = [
  { code: 'de', label: 'Deutsch' },
  { code: 'en', label: 'English' },
  { code: 'tr', label: 'Türkçe' },
];

export default function Settings() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { language, setLanguage } = useLanguage();
  const [settings, setSettings] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const isAdmin = user?.role === 'admin';

  useEffect(() => {
    const fetchSettings = async () => {
      try {
        setLoading(true);
        setError(null);
        
        // API çağrısı - company settings endpoint'inden veri al
        const response = await api.get('/api/company-settings');
        
        if (response.data) {
          setSettings(response.data);
        } else {
          setSettings(null);
        }
      } catch (err) {
        console.error('Settings API error:', err);
        setSettings(null);
        setError('API bağlantı hatası - Veriler yüklenemedi');
      } finally {
        setLoading(false);
      }
    };

    fetchSettings();
  }, []);

  if (loading) {
    return (
      <Box display="flex" justifyContent="center" alignItems="center" minHeight="400px">
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.settings')}
        </Typography>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      </Box>
    );
  }

  if (!settings) {
    return (
      <Box>
        <Typography variant="h4" component="h1" gutterBottom>
          {t('navigation.settings')}
        </Typography>
        <Alert severity="info" sx={{ mb: 2 }}>
          Henüz ayar verisi bulunmuyor.
        </Alert>
      </Box>
    );
  }

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.settings')}
      </Typography>

      <Grid container spacing={3}>
        {/* Company Settings */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                Company Information
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Name: {settings.companyName}
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Tax Number: {settings.taxNumber}
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Address: {settings.address}, {settings.city}
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Phone: {settings.phone}
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Email: {settings.email}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Website: {settings.website}
              </Typography>
            </CardContent>
          </Card>
        </Grid>

        {/* System Settings */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                System Settings
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Currency: {settings.defaultCurrency}
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Default Tax Rate: {settings.defaultTaxRate}%
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Industry: {settings.industry}
              </Typography>
              
              <Box mt={2}>
                <FormControl fullWidth disabled={!isAdmin}>
                  <InputLabel>Language</InputLabel>
                  <Select
                    value={language}
                    label="Language"
                    onChange={e => setLanguage(e.target.value)}
                  >
                    {LANG_OPTIONS.map(opt => (
                      <MenuItem key={opt.code} value={opt.code}>{opt.label}</MenuItem>
                    ))}
                  </Select>
                </FormControl>
                {!isAdmin && (
                  <Typography variant="body2" color="text.secondary" mt={1}>
                    Language: Deutsch (nur Admin kann ändern)
                  </Typography>
                )}
              </Box>
            </CardContent>
          </Card>
        </Grid>

        {/* FinanceOnline Settings */}
        <Grid item xs={12} md={6}>
          <Card>
            <CardContent>
              <Typography variant="h6" gutterBottom>
                FinanzOnline Configuration
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Enabled: {settings.isFinanceOnlineEnabled ? 'Yes' : 'No'}
              </Typography>
              <Typography variant="body2" color="text.secondary" gutterBottom>
                Username: {settings.financeOnlineUsername || 'Not configured'}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                Password: {settings.financeOnlinePassword ? '***' : 'Not configured'}
              </Typography>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Box>
  );
} 