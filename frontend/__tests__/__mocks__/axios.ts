import { jest } from '@jest/globals';

// Mock axios instance
const mockAxios = {
  create: jest.fn(() => mockAxios),
  get: jest.fn(),
  post: jest.fn(),
  put: jest.fn(),
  delete: jest.fn(),
  patch: jest.fn(),
  interceptors: {
    request: {
      use: jest.fn(),
      eject: jest.fn(),
    },
    response: {
      use: jest.fn(),
      eject: jest.fn(),
    },
  },
  defaults: {
    baseURL: 'http://localhost:5183/api',
    timeout: 10000,
    headers: {
      'Content-Type': 'application/json',
    },
  },
};

// Default mock responses
mockAxios.get.mockResolvedValue({ data: {}, status: 200 });
mockAxios.post.mockResolvedValue({ data: {}, status: 200 });
mockAxios.put.mockResolvedValue({ data: {}, status: 200 });
mockAxios.delete.mockResolvedValue({ data: {}, status: 200 });
mockAxios.patch.mockResolvedValue({ data: {}, status: 200 });

export default mockAxios;
export { mockAxios as axios };
