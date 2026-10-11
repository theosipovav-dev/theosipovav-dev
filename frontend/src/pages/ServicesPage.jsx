import { useCallback, useEffect, useRef, useState } from 'react'
import { App, Badge, Button, Card, Popconfirm, Space, Switch, Table, Tag, Tooltip, Typography } from 'antd'
import {
  CaretRightOutlined,
  PauseOutlined,
  ReloadOutlined,
  RedoOutlined,
  FileTextOutlined,
} from '@ant-design/icons'
import { Link } from 'react-router-dom'
import { servicesApi, errorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { activeState, formatBytes, formatDateTime, formatUptime } from '../utils/format'

const REFRESH_INTERVAL_MS = 10000

const ACTION_LABELS = {
  start: 'Запуск',
  stop: 'Остановка',
  restart: 'Перезапуск',
}

export default function ServicesPage() {
  const { canControl } = useAuth()
  const { message } = App.useApp()
  const [services, setServices] = useState([])
  const [loading, setLoading] = useState(false)
  const [autoRefresh, setAutoRefresh] = useState(true)
  const [pending, setPending] = useState({})
  const [updatedAt, setUpdatedAt] = useState(null)
  const loadingRef = useRef(false)

  const load = useCallback(
    async (silent = false) => {
      if (loadingRef.current) return
      loadingRef.current = true
      if (!silent) setLoading(true)
      try {
        setServices(await servicesApi.list())
        setUpdatedAt(new Date())
      } catch (e) {
        if (!silent) message.error(errorMessage(e))
      } finally {
        loadingRef.current = false
        setLoading(false)
      }
    },
    [message],
  )

  useEffect(() => {
    load()
  }, [load])

  useEffect(() => {
    if (!autoRefresh) return undefined
    const timer = setInterval(() => load(true), REFRESH_INTERVAL_MS)
    return () => clearInterval(timer)
  }, [autoRefresh, load])

  const runAction = async (service, action) => {
    setPending((p) => ({ ...p, [service.id]: action }))
    try {
      const result = await servicesApi.action(service.id, action)
      message.success(`${ACTION_LABELS[action]}: ${service.displayName} - ${result.message}`)
      if (result.status) {
        setServices((list) => list.map((s) => (s.id === service.id ? result.status : s)))
      }
    } catch (e) {
      message.error(`${ACTION_LABELS[action]}: ${service.displayName} - ${errorMessage(e)}`)
      const status = e?.response?.data?.status
      if (status) {
        setServices((list) => list.map((s) => (s.id === service.id ? status : s)))
      }
    } finally {
      setPending((p) => {
        const next = { ...p }
        delete next[service.id]
        return next
      })
    }
  }

  const columns = [
    {
      title: 'Служба',
      dataIndex: 'displayName',
      render: (_, s) => (
        <Space direction="vertical" size={0}>
          <Space size={4}>
            <Typography.Text strong>{s.displayName}</Typography.Text>
            {s.discovered && (
              <Tooltip title="Найдена автоматически по шаблону из конфигурации">
                <Tag color="geekblue">авто</Tag>
              </Tooltip>
            )}
          </Space>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {s.unit}
          </Typography.Text>
          {s.description && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {s.description}
            </Typography.Text>
          )}
        </Space>
      ),
    },
    {
      title: 'Статус',
      dataIndex: 'activeState',
      width: 200,
      render: (_, s) => {
        const st = activeState(s.activeState)
        return (
          <Space direction="vertical" size={0}>
            <Tag color={st.color}>{st.label}</Tag>
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {s.activeState}/{s.subState}
              {s.unitFileState ? `, ${s.unitFileState}` : ''}
            </Typography.Text>
            {s.error && (
              <Typography.Text type="danger" style={{ fontSize: 12 }}>
                {s.error}
              </Typography.Text>
            )}
          </Space>
        )
      },
    },
    {
      title: 'PID',
      dataIndex: 'mainPid',
      width: 90,
      render: (v) => v ?? '-',
    },
    {
      title: 'Работает',
      dataIndex: 'activeSince',
      width: 170,
      render: (v, s) =>
        s.activeState === 'active' ? <Tooltip title={formatDateTime(v)}>{formatUptime(v)}</Tooltip> : '-',
    },
    {
      title: 'Память',
      dataIndex: 'memoryBytes',
      width: 110,
      render: formatBytes,
    },
    {
      title: 'Действия',
      key: 'actions',
      width: 330,
      render: (_, s) => {
        const busy = pending[s.id]
        const disabled = !canControl || !s.allowControl || Boolean(busy)
        const isActive = s.activeState === 'active' || s.activeState === 'activating'
        return (
          <Space wrap>
            <Popconfirm
              title={`Запустить «${s.displayName}»?`}
              onConfirm={() => runAction(s, 'start')}
              disabled={disabled || isActive}
            >
              <Button size="small" icon={<CaretRightOutlined />} disabled={disabled || isActive} loading={busy === 'start'}>
                Запустить
              </Button>
            </Popconfirm>
            <Popconfirm
              title={`Остановить «${s.displayName}»?`}
              okButtonProps={{ danger: true }}
              onConfirm={() => runAction(s, 'stop')}
              disabled={disabled || !isActive}
            >
              <Button size="small" danger icon={<PauseOutlined />} disabled={disabled || !isActive} loading={busy === 'stop'}>
                Остановить
              </Button>
            </Popconfirm>
            <Popconfirm
              title={`Перезапустить «${s.displayName}»?`}
              onConfirm={() => runAction(s, 'restart')}
              disabled={disabled}
            >
              <Button size="small" icon={<RedoOutlined />} disabled={disabled} loading={busy === 'restart'}>
                Перезапустить
              </Button>
            </Popconfirm>
            <Link to={`/services/${encodeURIComponent(s.id)}/logs`}>
              <Button size="small" type="link" icon={<FileTextOutlined />}>
                Логи
              </Button>
            </Link>
          </Space>
        )
      },
    },
  ]

  const activeCount = services.filter((s) => s.activeState === 'active').length
  const groups = groupServices(services)
  const showGroupTitles = groups.length > 1 || groups[0]?.name !== null

  return (
    <Card
      title={
        <Space>
          <span>Службы</span>
          <Badge
            count={`${activeCount} / ${services.length}`}
            style={{ backgroundColor: activeCount === services.length ? '#52c41a' : '#faad14' }}
          />
        </Space>
      }
      extra={
        <Space>
          {updatedAt && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Обновлено: {formatDateTime(updatedAt)}
            </Typography.Text>
          )}
          <Switch checked={autoRefresh} onChange={setAutoRefresh} checkedChildren="Авто" unCheckedChildren="Авто" />
          <Button icon={<ReloadOutlined />} onClick={() => load()} loading={loading}>
            Обновить
          </Button>
        </Space>
      }
    >
      {services.length === 0 && (
        <Table rowKey="id" columns={columns} dataSource={[]} loading={loading} pagination={false} />
      )}
      {groups.map((g) => {
        const groupActive = g.items.filter((s) => s.activeState === 'active').length
        return (
          <div key={g.name ?? ''} style={{ marginBottom: 24 }}>
            {showGroupTitles && (
              <Space style={{ marginBottom: 8 }}>
                <Typography.Title level={5} style={{ margin: 0 }}>
                  {g.name ?? 'Без группы'}
                </Typography.Title>
                <Badge
                  count={`${groupActive} / ${g.items.length}`}
                  style={{ backgroundColor: groupActive === g.items.length ? '#52c41a' : '#faad14' }}
                />
              </Space>
            )}
            <Table rowKey="id" columns={columns} dataSource={g.items} pagination={false} scroll={{ x: 1000 }} />
          </div>
        )
      })}
    </Card>
  )
}

/** Группировка по полю group с сохранением порядка из конфигурации. */
function groupServices(services) {
  const map = new Map()
  for (const s of services) {
    const key = s.group || null
    if (!map.has(key)) map.set(key, [])
    map.get(key).push(s)
  }
  return Array.from(map, ([name, items]) => ({ name, items }))
}
