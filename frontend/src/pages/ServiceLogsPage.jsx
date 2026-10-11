import { useCallback, useEffect, useRef, useState } from 'react'
import { App, Button, Card, DatePicker, Empty, Input, InputNumber, Select, Space, Switch, Tag, Typography } from 'antd'
import { ArrowLeftOutlined, DownloadOutlined, ReloadOutlined } from '@ant-design/icons'
import { Link, useParams } from 'react-router-dom'
import { servicesApi, errorMessage } from '../api/client'
import { activeState, formatDateTime, PRIORITIES, priorityInfo } from '../utils/format'

const LIVE_INTERVAL_MS = 3000
const MAX_ENTRIES = 5000

const LINE_COLORS = {
  0: '#ff4d9a',
  1: '#ff4d9a',
  2: '#ff6b6b',
  3: '#ff6b6b',
  4: '#ffc53d',
  5: '#69b1ff',
}

export default function ServiceLogsPage() {
  const { id } = useParams()
  const { message } = App.useApp()
  const [service, setService] = useState(null)
  const [entries, setEntries] = useState([])
  const [loading, setLoading] = useState(false)
  const [lines, setLines] = useState(200)
  const [priority, setPriority] = useState(null)
  const [range, setRange] = useState(null)
  const [grep, setGrep] = useState('')
  const [live, setLive] = useState(false)
  const [autoScroll, setAutoScroll] = useState(true)
  const cursorRef = useRef(null)
  const busyRef = useRef(false)
  const boxRef = useRef(null)

  const filters = useCallback(
    () => ({
      priority: priority ?? undefined,
      grep: grep.trim() || undefined,
    }),
    [priority, grep],
  )

  const load = useCallback(async () => {
    setLoading(true)
    busyRef.current = true
    try {
      const data = await servicesApi.logs(id, {
        ...filters(),
        lines,
        since: range?.[0]?.toISOString(),
        until: range?.[1]?.toISOString(),
      })
      setEntries(data.entries)
      cursorRef.current = data.lastCursor
    } catch (e) {
      message.error(errorMessage(e))
    } finally {
      busyRef.current = false
      setLoading(false)
    }
  }, [id, lines, range, filters, message])

  useEffect(() => {
    servicesApi.get(id).then(setService).catch((e) => message.error(errorMessage(e)))
  }, [id, message])

  useEffect(() => {
    load()
    // Перезагрузка только при смене службы; фильтры применяются кнопкой.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id])

  // "Живой" режим: дочитываем новые записи после последнего курсора.
  useEffect(() => {
    if (!live) return undefined
    const timer = setInterval(async () => {
      if (busyRef.current || !cursorRef.current) return
      busyRef.current = true
      try {
        const data = await servicesApi.logs(id, { ...filters(), afterCursor: cursorRef.current })
        if (data.entries.length > 0) {
          cursorRef.current = data.lastCursor
          setEntries((prev) => {
            const next = prev.concat(data.entries)
            return next.length > MAX_ENTRIES ? next.slice(next.length - MAX_ENTRIES) : next
          })
        }
      } catch {
        // Ошибки в живом режиме не показываем каждые 3 секунды.
      } finally {
        busyRef.current = false
      }
    }, LIVE_INTERVAL_MS)
    return () => clearInterval(timer)
  }, [live, id, filters])

  useEffect(() => {
    if (autoScroll && boxRef.current) {
      boxRef.current.scrollTop = boxRef.current.scrollHeight
    }
  }, [entries, autoScroll])

  const download = () => {
    const text = entries
      .map((e) => `${formatDateTime(e.timestamp)} [${priorityInfo(e.priority).label}] ${e.identifier ?? ''}[${e.pid ?? ''}]: ${e.message}`)
      .join('\n')
    const blob = new Blob([text], { type: 'text/plain;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `${id}-${Date.now()}.log`
    a.click()
    URL.revokeObjectURL(url)
  }

  const st = service ? activeState(service.activeState) : null

  return (
    <Card
      title={
        <Space>
          <Link to="/services">
            <Button type="text" icon={<ArrowLeftOutlined />} />
          </Link>
          <span>Журнал: {service?.displayName ?? id}</span>
          {service && <Typography.Text type="secondary">{service.unit}</Typography.Text>}
          {st && <Tag color={st.color}>{st.label}</Tag>}
        </Space>
      }
    >
      <Space wrap style={{ marginBottom: 16 }}>
        <Space.Compact>
          <Button disabled>Строк</Button>
          <InputNumber min={1} max={2000} value={lines} onChange={(v) => setLines(v || 200)} style={{ width: 100 }} />
        </Space.Compact>
        <Select
          allowClear
          placeholder="Уровень (не ниже)"
          style={{ width: 180 }}
          value={priority}
          onChange={setPriority}
          options={PRIORITIES.map((p) => ({ value: p.value, label: `${p.value} - ${p.label}` }))}
        />
        <DatePicker.RangePicker showTime value={range} onChange={setRange} format="DD.MM.YYYY HH:mm" />
        <Input.Search
          placeholder="Поиск по тексту"
          allowClear
          value={grep}
          onChange={(e) => setGrep(e.target.value)}
          onSearch={load}
          style={{ width: 240 }}
        />
        <Button type="primary" icon={<ReloadOutlined />} onClick={load} loading={loading}>
          Показать
        </Button>
        <Space>
          <Switch checked={live} onChange={setLive} />
          <span>В реальном времени</span>
        </Space>
        <Space>
          <Switch checked={autoScroll} onChange={setAutoScroll} />
          <span>Автопрокрутка</span>
        </Space>
        <Button icon={<DownloadOutlined />} onClick={download} disabled={entries.length === 0}>
          Скачать
        </Button>
      </Space>

      <div
        ref={boxRef}
        style={{
          background: '#141414',
          color: '#d9d9d9',
          fontFamily: 'Consolas, "DejaVu Sans Mono", monospace',
          fontSize: 12,
          lineHeight: 1.5,
          padding: 12,
          borderRadius: 6,
          height: 'calc(100vh - 330px)',
          minHeight: 300,
          overflow: 'auto',
          whiteSpace: 'pre-wrap',
          wordBreak: 'break-all',
        }}
      >
        {entries.length === 0 ? (
          <Empty description={<span style={{ color: '#8c8c8c' }}>Записей нет</span>} />
        ) : (
          entries.map((e) => (
            <div key={e.cursor} style={{ color: LINE_COLORS[e.priority] }}>
              <span style={{ color: '#8c8c8c' }}>{formatDateTime(e.timestamp)}</span>{' '}
              <span style={{ color: '#5cdbd3' }}>
                {e.identifier}
                {e.pid ? `[${e.pid}]` : ''}
              </span>
              : {e.message}
            </div>
          ))
        )}
      </div>
      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
        Показано записей: {entries.length}
      </Typography.Text>
    </Card>
  )
}
