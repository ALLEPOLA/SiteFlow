import { useEffect, useRef, useState } from 'react'
import { apiFetch, readApiResponse } from '../../api/apiFetch'

export const money = (value: number) => new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR' }).format(value)
export const today = () => {
  const date = new Date()
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`
}
export const get = <T,>(url: string, signal?: AbortSignal) => apiFetch(url, { signal, cache: 'no-store' }).then(readApiResponse<T>)
export const send = <T,>(url: string, body: unknown, method = 'POST') => apiFetch(url, {
  method, headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body),
}).then(readApiResponse<T>)

export function useData<T>(url: string) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [version, setVersion] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError('')
      try {
        const result = await get<T>(url, controller.signal)
        if (!controller.signal.aborted) setData(result)
      } catch (failure) {
        if (!controller.signal.aborted) setError(failure instanceof Error ? failure.message : 'Unable to load data.')
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [url, version])
  return { data, error, loading, refresh: () => setVersion(v => v + 1) }
}

export function useCommand() {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const running = useRef(false)
  async function run(work: () => Promise<unknown>) {
    if (running.current) return false
    running.current = true
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const result = await work() as { message?: string } | undefined
      setMessage(result?.message ?? 'Saved successfully.')
      return true
    } catch (failure) {
      setError(failure instanceof Error ? failure.message : 'Unable to save.')
      return false
    } finally { running.current = false; setBusy(false) }
  }
  return { busy, error, message, run }
}

export type Project = { id: number; code: string; name: string; status: string }
export type Material = { id: number; code: string; name: string; isActive: boolean; reorderLevel: number }

export async function download(url: string, filename: string) {
  const response = await apiFetch(url)
  if (!response.ok) await readApiResponse(response)
  const blob = await response.blob()
  const link = document.createElement('a')
  const objectUrl = URL.createObjectURL(blob)
  link.href = objectUrl
  link.download = filename
  link.click()
  setTimeout(() => URL.revokeObjectURL(objectUrl), 1000)
}
