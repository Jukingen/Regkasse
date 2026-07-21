import { customInstance } from '@/lib/axios';

export type ForgotUsernameRequest = {
  email: string;
  clientApp: 'admin';
};

export type ForgotUsernameResponse = {
  message: string;
};

export async function requestForgotUsername(email: string): Promise<ForgotUsernameResponse> {
  return customInstance<ForgotUsernameResponse>({
    url: '/api/Auth/forgot-username',
    method: 'POST',
    data: {
      email: email.trim(),
      clientApp: 'admin',
    },
  });
}
