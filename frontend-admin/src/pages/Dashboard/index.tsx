import { Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';

export default function Dashboard() {
  const { t } = useTranslation();

  return (
    <Typography variant="h4" component="h1" gutterBottom>
      {t('navigation.dashboard')}
    </Typography>
  );
} 