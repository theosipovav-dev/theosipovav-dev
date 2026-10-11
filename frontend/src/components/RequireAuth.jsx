import { Navigate, useLocation } from 'react-router-dom'
import { Result, Spin } from 'antd'
import { useAuth } from '../auth/AuthContext'

export default function RequireAuth({ children, adminOnly = false }) {
  const { user, loading, isAdmin } = useAuth()
  const location = useLocation()

  if (loading) {
    return <Spin size="large" style={{ display: 'block', margin: '20vh auto' }} />
  }
  if (!user) {
    return <Navigate to="/login" replace state={{ from: location }} />
  }
  if (adminOnly && !isAdmin) {
    return <Result status="403" title="Нет доступа" subTitle="Раздел доступен только администраторам" />
  }
  return children
}
