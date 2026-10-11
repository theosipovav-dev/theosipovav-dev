import axios from 'axios'

const TOKEN_KEY = 'tfdc_token'

export const tokenStorage = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (token) => localStorage.setItem(TOKEN_KEY, token),
  clear: () => localStorage.removeItem(TOKEN_KEY),
}

// Базовый адрес API. По умолчанию тот же origin (vite proxy или backend отдаёт фронтенд сам).
const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  timeout: 90000,
})

api.interceptors.request.use((config) => {
  const token = tokenStorage.get()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && tokenStorage.get()) {
      tokenStorage.clear()
      window.dispatchEvent(new Event('auth:logout'))
    }
    return Promise.reject(error)
  },
)

/** Текст ошибки из ответа API (ProblemDetails / ValidationProblemDetails). */
export function errorMessage(error) {
  const data = error?.response?.data
  if (data?.errors) {
    return Object.values(data.errors).flat().join('; ')
  }
  if (data?.message) return data.message
  if (data?.title) return data.detail ? `${data.title}: ${data.detail}` : data.title
  if (error?.response?.status === 403) return 'Недостаточно прав'
  if (error?.response?.status === 429) return 'Слишком много попыток, попробуйте позже'
  return error?.message || 'Неизвестная ошибка'
}

export const authApi = {
  login: (username, password) => api.post('/auth/login', { username, password }).then((r) => r.data),
  me: () => api.get('/auth/me').then((r) => r.data),
  changePassword: (currentPassword, newPassword) =>
    api.post('/auth/change-password', { currentPassword, newPassword }),
}

export const servicesApi = {
  list: () => api.get('/services').then((r) => r.data),
  get: (id) => api.get(`/services/${encodeURIComponent(id)}`).then((r) => r.data),
  action: (id, action) => api.post(`/services/${encodeURIComponent(id)}/${action}`).then((r) => r.data),
  logs: (id, params) => api.get(`/services/${encodeURIComponent(id)}/logs`, { params }).then((r) => r.data),
}

export const usersApi = {
  list: () => api.get('/users').then((r) => r.data),
  create: (data) => api.post('/users', data).then((r) => r.data),
  update: (id, data) => api.put(`/users/${id}`, data).then((r) => r.data),
  resetPassword: (id, newPassword) => api.post(`/users/${id}/reset-password`, { newPassword }),
  remove: (id) => api.delete(`/users/${id}`),
}

export const auditApi = {
  list: (params) => api.get('/audit', { params }).then((r) => r.data),
}

export default api
