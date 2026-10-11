import { useCallback, useEffect, useState } from 'react'
import { App, Button, Card, Form, Input, Modal, Popconfirm, Select, Space, Switch, Table, Tag } from 'antd'
import { DeleteOutlined, EditOutlined, KeyOutlined, PlusOutlined } from '@ant-design/icons'
import { usersApi, errorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { ROLE_LABELS, ROLES } from '../auth/roles'
import { formatDateTime } from '../utils/format'

const ROLE_OPTIONS = Object.values(ROLES).map((r) => ({ value: r, label: ROLE_LABELS[r] }))
const ROLE_COLORS = { Admin: 'red', Operator: 'blue', Viewer: 'default' }

const passwordRules = [
  { required: true, message: 'Введите пароль' },
  { min: 8, message: 'Не менее 8 символов' },
]

export default function UsersPage() {
  const { user: currentUser } = useAuth()
  const { message } = App.useApp()
  const [users, setUsers] = useState([])
  const [loading, setLoading] = useState(false)
  const [editing, setEditing] = useState(null) // null | {} (новый) | user
  const [resetting, setResetting] = useState(null)
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm()
  const [passwordForm] = Form.useForm()

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setUsers(await usersApi.list())
    } catch (e) {
      message.error(errorMessage(e))
    } finally {
      setLoading(false)
    }
  }, [message])

  useEffect(() => {
    load()
  }, [load])

  const openEditor = (user) => {
    setEditing(user || {})
    form.resetFields()
    form.setFieldsValue(user ? { ...user } : { role: ROLES.Viewer, isActive: true })
  }

  const save = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      if (editing.id) {
        await usersApi.update(editing.id, values)
        message.success('Пользователь сохранён')
      } else {
        await usersApi.create(values)
        message.success('Пользователь создан')
      }
      setEditing(null)
      load()
    } catch (e) {
      message.error(errorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  const resetPassword = async () => {
    const { newPassword } = await passwordForm.validateFields()
    setSaving(true)
    try {
      await usersApi.resetPassword(resetting.id, newPassword)
      message.success('Пароль изменён')
      setResetting(null)
    } catch (e) {
      message.error(errorMessage(e))
    } finally {
      setSaving(false)
    }
  }

  const remove = async (user) => {
    try {
      await usersApi.remove(user.id)
      message.success('Пользователь удалён')
      load()
    } catch (e) {
      message.error(errorMessage(e))
    }
  }

  const columns = [
    { title: 'Логин', dataIndex: 'username' },
    { title: 'Имя', dataIndex: 'displayName', render: (v) => v || '-' },
    {
      title: 'Роль',
      dataIndex: 'role',
      render: (r) => <Tag color={ROLE_COLORS[r]}>{ROLE_LABELS[r] ?? r}</Tag>,
    },
    {
      title: 'Статус',
      dataIndex: 'isActive',
      render: (v) => (v ? <Tag color="success">Активен</Tag> : <Tag>Заблокирован</Tag>),
    },
    { title: 'Создан', dataIndex: 'createdAt', render: (v) => formatDateTime(v, false) },
    { title: 'Последний вход', dataIndex: 'lastLoginAt', render: (v) => formatDateTime(v) },
    {
      title: '',
      key: 'actions',
      width: 200,
      render: (_, u) => (
        <Space>
          <Button size="small" icon={<EditOutlined />} onClick={() => openEditor(u)} />
          <Button
            size="small"
            icon={<KeyOutlined />}
            onClick={() => {
              passwordForm.resetFields()
              setResetting(u)
            }}
          />
          <Popconfirm
            title={`Удалить пользователя ${u.username}?`}
            okButtonProps={{ danger: true }}
            onConfirm={() => remove(u)}
            disabled={u.id === currentUser?.id}
          >
            <Button size="small" danger icon={<DeleteOutlined />} disabled={u.id === currentUser?.id} />
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <Card
      title="Пользователи"
      extra={
        <Button type="primary" icon={<PlusOutlined />} onClick={() => openEditor(null)}>
          Добавить
        </Button>
      }
    >
      <Table rowKey="id" columns={columns} dataSource={users} loading={loading} pagination={false} scroll={{ x: 900 }} />

      <Modal
        open={editing !== null}
        title={editing?.id ? `Пользователь ${editing.username}` : 'Новый пользователь'}
        onCancel={() => setEditing(null)}
        onOk={save}
        okText="Сохранить"
        cancelText="Отмена"
        confirmLoading={saving}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          {!editing?.id && (
            <Form.Item
              name="username"
              label="Логин"
              rules={[
                { required: true, message: 'Введите логин' },
                { pattern: /^[A-Za-z0-9._-]{3,64}$/, message: '3-64 символа: латиница, цифры, . _ -' },
              ]}
            >
              <Input autoComplete="off" />
            </Form.Item>
          )}
          <Form.Item name="displayName" label="Имя">
            <Input />
          </Form.Item>
          {!editing?.id && (
            <Form.Item name="password" label="Пароль" rules={passwordRules}>
              <Input.Password autoComplete="new-password" />
            </Form.Item>
          )}
          <Form.Item name="role" label="Роль" rules={[{ required: true }]}>
            <Select options={ROLE_OPTIONS} />
          </Form.Item>
          <Form.Item name="isActive" label="Активен" valuePropName="checked">
            <Switch />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        open={resetting !== null}
        title={`Новый пароль для ${resetting?.username}`}
        onCancel={() => setResetting(null)}
        onOk={resetPassword}
        okText="Сменить"
        cancelText="Отмена"
        confirmLoading={saving}
        destroyOnClose
      >
        <Form form={passwordForm} layout="vertical">
          <Form.Item name="newPassword" label="Пароль" rules={passwordRules}>
            <Input.Password autoComplete="new-password" />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  )
}
