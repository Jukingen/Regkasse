import { Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../contexts/AuthContext';

export default function Profile() {
  const { t } = useTranslation();
  const { user } = useAuth();

  return (
    <>
      <Typography variant="h4" component="h1" gutterBottom>
        {t('navigation.profile')}
      </Typography>
      <Typography variant="body1">
        {t('profile.welcome', { name: user?.name })}
      </Typography>
    </>
  );
} 