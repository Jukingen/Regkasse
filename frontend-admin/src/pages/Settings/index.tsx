import { Typography, Box, FormControl, InputLabel, Select, MenuItem } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';
import { useLanguage } from '../../contexts/LanguageContext';

const LANG_OPTIONS = [
  { code: 'de', label: 'Deutsch' },
  { code: 'en', label: 'English' },
  { code: 'tr', label: 'Türkçe' },
];

export default function Settings() {
  const { t } = useTranslation();
  const { user } = useAuth();
  const { language, setLanguage } = useLanguage();
  const isAdmin = user?.role === 'admin';

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.settings')}
      </Typography>
      <Box mt={4}>
        <FormControl fullWidth disabled={!isAdmin}>
          <InputLabel>{t('settings.language')}</InputLabel>
          <Select
            value={language}
            label={t('settings.language')}
            onChange={e => setLanguage(e.target.value)}
          >
            {LANG_OPTIONS.map(opt => (
              <MenuItem key={opt.code} value={opt.code}>{opt.label}</MenuItem>
            ))}
          </Select>
        </FormControl>
        {!isAdmin && (
          <Typography variant="body2" color="text.secondary" mt={2}>
            {t('settings.language')}: Deutsch (nur Admin kann ändern)
          </Typography>
        )}
      </Box>
    </Box>
  );
} 