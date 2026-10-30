import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, readApiResponse } from '../api/apiFetch'

export interface FieldDefinition {
  name: string
  label: string
  type?: 'text' | 'email' | 'tel' | 'number' | 'textarea' | 'select'
  required?: boolean
  maxLength?: number
  min?: string
  max?: string
  step?: string
  options?: { value: string; label: string }[]
}

interface DataRow {
  id: number
  [key: string]: unknown
}

interface ColumnDefinition {
  key: string
  label: string
  format?: 'money' | 'active'
}

interface Props {
  title: string
  singular: string
  description: string
  endpoint: string
  fields: FieldDefinition[]
  columns: ColumnDefinition[]
  createRoles: string[]
}

const money = new Intl.NumberFormat('en-LK', {
  style: 'currency',
  currency: 'LKR',
})

export default function MasterDataPage({
  title,
  singular,
  description,
  endpoint,
  fields,
  columns,
  createRoles,
}: Props) {
  const emptyForm = () =>
    Object.fromEntries(fields.map((field) => [field.name, '']))

  const [rows, setRows] = useState<DataRow[]>([])
  const [form, setForm] = useState<Record<string, string>>(emptyForm)
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [canCreate, setCanCreate] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    async function load() {
      try {
        const [data, user] = await Promise.all([
          apiFetch(endpoint, {
            signal: controller.signal,
          }).then((response) => readApiResponse<DataRow[]>(response)),

          apiFetch('/api/auth/me', {
            signal: controller.signal,
            cache: 'no-store',
          }).then((response) =>
            readApiResponse<{ roles: string[] }>(response),
          ),
        ])

        if (controller.signal.aborted) return

        setRows(data)
        setCanCreate(
          user.roles.some((role) => createRoles.includes(role)),
        )
      } catch (error) {
        if (controller.signal.aborted) return
        setLoadError(
          error instanceof Error ? error.message : 'Unable to load data.',
        )
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [endpoint, createRoles])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (saving) return

    setError('')
    setSuccess('')

    const payload: Record<string, string | number | null> = {}

    for (const field of fields) {
      const value = form[field.name]?.trim() ?? ''

      if (field.required && !value) {
        setError(`${field.label} is required.`)
        return
      }

      if (field.type === 'number') {
        const number = Number(value)

        if (!value || !Number.isFinite(number) || number < 0) {
          setError(`Enter a valid ${field.label.toLowerCase()}.`)
          return
        }

        payload[field.name] = number
      } else {
        payload[field.name] = value || null
      }
    }

    setSaving(true)

    try {
      const response = await apiFetch(endpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      })

      const created = await readApiResponse<DataRow>(response)

      setRows((current) =>
        [...current, created].sort((a, b) =>
          String(a.code).localeCompare(String(b.code)),
        ),
      )

      setForm(emptyForm())
      setSuccess(`${singular} ${String(created.code)} created.`)
    } catch (error) {
      setError(
        error instanceof Error ? error.message : 'Unable to save.',
      )
    } finally {
      setSaving(false)
    }
  }

  function displayValue(row: DataRow, column: ColumnDefinition) {
    const value = row[column.key]

    if (column.format === 'money') {
      return money.format(Number(value))
    }

    if (column.format === 'active') {
      return value ? 'Active' : 'Inactive'
    }

    return value === null || value === undefined || value === ''
      ? '—'
      : String(value)
  }

  if (loading) {
    return <p role="status">Loading {title.toLowerCase()}...</p>
  }

  if (loadError) {
    return <p className="error-message" role="alert">{loadError}</p>
  }

  return (
    <>
      <header className="page-header">
        <h1>{title}</h1>
        <p>{description}</p>
      </header>

      {canCreate && (
        <section className="panel">
          <h2>Create {singular}</h2>

          <form onSubmit={handleSubmit}>
            <fieldset className="form-fields" disabled={saving}>
              <div className="form-grid">
                {fields.map((field) => {
                  const id = `${singular}-${field.name}`

                  const common = {
                    id,
                    name: field.name,
                    value: form[field.name] ?? '',
                    required: field.required,
                  }

                  const update = (value: string) =>
                    setForm((current) => ({
                      ...current,
                      [field.name]: value,
                    }))

                  return (
                    <label key={field.name} htmlFor={id}>
                      {field.label}{field.required ? ' *' : ''}

                      {field.type === 'textarea' ? (
                        <textarea
                          {...common}
                          rows={3}
                          maxLength={field.maxLength}
                          onChange={(event) => update(event.target.value)}
                        />
                      ) : field.type === 'select' ? (
                        <select
                          {...common}
                          onChange={(event) => update(event.target.value)}
                        >
                          <option value="">Select {field.label.toLowerCase()}</option>
                          {field.options?.map((option) => (
                            <option key={option.value} value={option.value}>
                              {option.label}
                            </option>
                          ))}
                        </select>
                      ) : (
                        <input
                          {...common}
                          type={field.type ?? 'text'}
                          maxLength={field.maxLength}
                          min={field.min}
                          max={field.max}
                          step={field.step}
                          onChange={(event) => update(event.target.value)}
                        />
                      )}
                    </label>
                  )
                })}
              </div>

              <button className="primary-button" type="submit">
                {saving ? 'Saving...' : `Create ${singular}`}
              </button>
            </fieldset>

            {error && <p className="error-message" role="alert">{error}</p>}
            {success && <p className="success-message" role="status">{success}</p>}
          </form>
        </section>
      )}

      <section className="panel">
        <h2>{title} List</h2>

        {rows.length === 0 ? (
          <p>No records found.</p>
        ) : (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  {columns.map((column) => (
                    <th
                      key={column.key}
                      scope="col"
                      className={column.format === 'money' ? 'number-cell' : ''}
                    >
                      {column.label}
                    </th>
                  ))}
                </tr>
              </thead>

              <tbody>
                {rows.map((row) => (
                  <tr key={row.id}>
                    {columns.map((column) => (
                      <td
                        key={column.key}
                        className={column.format === 'money' ? 'number-cell' : ''}
                      >
                        {displayValue(row, column)}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}