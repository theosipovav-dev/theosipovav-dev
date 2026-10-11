import { Layout, Menu, Dropdown, Space, Typography, Avatar } from 'antd'
import {
  AppstoreOutlined,
  TeamOutlined,
  AuditOutlined,
  UserOutlined,
  LogoutOutlined,
  KeyOutlined,
} from '@ant-design/icons'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { ROLE_LABELS } from '../auth/roles'

const { Header, Content, Sider } = Layout

export default function AppLayout() {
  const { user, isAdmin, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const items = [
    { key: '/services', icon: <AppstoreOutlined />, label: 'Службы' },
    ...(isAdmin
      ? [
          { key: '/users', icon: <TeamOutlined />, label: 'Пользователи' },
          { key: '/audit', icon: <AuditOutlined />, label: 'Журнал действий' },
        ]
      : []),
  ]

  const selected = items.find((i) => location.pathname.startsWith(i.key))?.key

  const userMenu = {
    items: [
      { key: 'profile', icon: <KeyOutlined />, label: 'Сменить пароль' },
      { type: 'divider' },
      { key: 'logout', icon: <LogoutOutlined />, label: 'Выйти', danger: true },
    ],
    onClick: ({ key }) => {
      if (key === 'logout') {
        logout()
        navigate('/login')
      } else if (key === 'profile') {
        navigate('/profile')
      }
    },
  }

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider breakpoint="lg" collapsedWidth={0}>
        <div style={{ color: '#fff', padding: '16px 24px', fontWeight: 600, fontSize: 16 }}>
          T-FLEX DOCs Control
        </div>
        <Menu theme="dark" mode="inline" selectedKeys={selected ? [selected] : []} items={items} onClick={({ key }) => navigate(key)} />
      </Sider>
      <Layout>
        <Header
          style={{
            background: '#fff',
            padding: '0 24px',
            display: 'flex',
            justifyContent: 'flex-end',
            alignItems: 'center',
          }}
        >
          <Dropdown menu={userMenu} trigger={['click']}>
            <Space style={{ cursor: 'pointer' }}>
              <Avatar icon={<UserOutlined />} />
              <span>{user?.displayName || user?.username}</span>
              <Typography.Text type="secondary">{ROLE_LABELS[user?.role]}</Typography.Text>
            </Space>
          </Dropdown>
        </Header>
        <Content style={{ margin: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  )
}
