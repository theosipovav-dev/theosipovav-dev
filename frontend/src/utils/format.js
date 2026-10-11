import dayjs from 'dayjs'

/** Цвет и подпись статуса systemd (ActiveState). */
export const ACTIVE_STATE = {
  active: { color: 'success', label: 'Работает' },
  reloading: { color: 'processing', label: 'Перезагрузка' },
  activating: { color: 'processing', label: 'Запускается' },
  deactivating: { color: 'warning', label: 'Останавливается' },
  inactive: { color: 'default', label: 'Остановлена' },
  failed: { color: 'error', label: 'Ошибка' },
  unknown: { color: 'default', label: 'Неизвестно' },
}

export function activeState(state) {
  return ACTIVE_STATE[state] || { color: 'default', label: state }
}

/** Уровни syslog для журнала. */
export const PRIORITIES = [
  { value: 0, label: 'emerg', color: 'magenta' },
  { value: 1, label: 'alert', color: 'magenta' },
  { value: 2, label: 'crit', color: 'red' },
  { value: 3, label: 'err', color: 'red' },
  { value: 4, label: 'warning', color: 'orange' },
  { value: 5, label: 'notice', color: 'blue' },
  { value: 6, label: 'info', color: 'default' },
  { value: 7, label: 'debug', color: 'default' },
]

export function priorityInfo(p) {
  return PRIORITIES.find((x) => x.value === p) || { value: p, label: p ?? '-', color: 'default' }
}

export function formatDateTime(value, withSeconds = true) {
  if (!value) return '-'
  return dayjs(value).format(withSeconds ? 'DD.MM.YYYY HH:mm:ss' : 'DD.MM.YYYY HH:mm')
}

export function formatBytes(bytes) {
  if (bytes == null) return '-'
  const units = ['Б', 'КБ', 'МБ', 'ГБ', 'ТБ']
  let value = bytes
  let i = 0
  while (value >= 1024 && i < units.length - 1) {
    value /= 1024
    i++
  }
  return `${value.toFixed(i === 0 ? 0 : 1)} ${units[i]}`
}

export function formatUptime(since) {
  if (!since) return '-'
  const seconds = Math.max(0, dayjs().diff(dayjs(since), 'second'))
  const d = Math.floor(seconds / 86400)
  const h = Math.floor((seconds % 86400) / 3600)
  const m = Math.floor((seconds % 3600) / 60)
  if (d > 0) return `${d} д ${h} ч`
  if (h > 0) return `${h} ч ${m} мин`
  return `${m} мин`
}
