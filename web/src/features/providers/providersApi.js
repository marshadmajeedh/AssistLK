import apiClient from '../../shared/api/apiClient';

export const getVerificationQueue = async () => {
  const response = await apiClient.get('/api/providers/verification-queue');
  return response.data;
};

export const verifyProvider = async (providerId, status) => {
  const response = await apiClient.put(`/api/providers/${providerId}/verify`, { status });
  return response.data;
};

export const getCertificateUrl = (fileName) => {
  const baseUrl = (import.meta.env.VITE_API_BASE_URL || 'http://localhost:5012').replace(/\/+$/, '');
  const prefix = baseUrl.endsWith('/api') ? '' : '/api';
  return `${baseUrl}${prefix}/providers/certificates/${fileName}`;
};
