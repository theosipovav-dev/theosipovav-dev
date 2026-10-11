import { useCallback, useEffect, useState } from 'react'
import { App, Button, Card, Input, Space, Table, Tag } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'
import { auditApi, errorMessage } from '../api/client'
import { formatDateTime } from '../utils/format'

const ACTION_LABELS = {
  login: 'Вход',
  'change-password': 'Смена пароля',
  'service-start': 'Запуск службы',
  'service-stop': 'Остановка службы',
  'service-restart': 'Перезапуск службы',
  'user-create': 'Создание пользователя',
  'user-update': 'Изменение пользователя',
  'user-reset-password': 'Сброс пароля',
  'user-delete': 'Удаление пользователя',
}

export default function AuditPage() {
  const { message } = App.useApp()
  const [data, setData] = useState({ items: [], total: 0 })
  const [loading, setLoading] = useState(false)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(50)
  const [username, setUsername] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setData(await auditApi.list({ page, pageSize, username: username.trim() || undefined }))
    } catch (e) {
      message.error(errorMessage(e))
    } finally {
      setLoading(false)
    }
    // username применяется по кнопке поиска
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, pageSize, message])

  useEffect(() => {
    load()
  }, [load])

  const columns = [
    { title: 'Время', dataIndex: 'timestamp', width: 170, render: (v) => formatDateTime(v) },
    { title: 'Пользователь', dataIndex: 'username', width: 140 },
    { title: 'Действие', dataIndex: 'action', width: 200, render: (a) => ACTION_LABELS[a] ?? a },
    { title: 'Объект', dataIndex: 'target', render: (v) => v || '-' },
    {
      title: 'Результат',
      dataIndex: 'success',
      width: 110,
      render: (v) => (v ? <Tag color="success">Успешно</Tag> : <Tag color="error">Ошибка</Tag>),
    },
    { title: 'Подробности', dataIndex: 'details', render: (v) => v || '-' },
    { title: 'IP', dataIndex: 'remoteIp', width: 130 },
  ]

  return (
    <Card
      title="Журнал действий"
      extra={
        <Space>
          <Input.Search
            placeholder="Пользователь"
            allowClear
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            onSearch={() => (page === 1 ? load() : setPage(1))}
            style={{ width: 200 }}
          />
          <Button icon={<ReloadOutlined />} onClick={load} loading={loading} />
        </Space>
      }
    >
      <Table
        rowKey="id"
        columns={columns}
        dataSource={data.items}
        loading={loading}
        scroll={{ x: 1000 }}
        pagination={{
          current: page,
          pageSize,
          total: data.total,
          showSizeChanger: true,
          pageSizeOptions: [20, 50, 100, 200],
          onChange: (p, s) => {
            setPage(p)
            setPageSize(s)
          },
        }}
      />
    </Card>
  )
}
