import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'

interface Supplier {
  id: number
  code: string
  name: string
  contactPerson: string
  phone: string
  email: string
  address: string
  isActive: boolean
}

interface ApiError {
  message?: string
  title?: string
  errors?: Record<string, string[]>
}

const emptyForm = {
  code: '',
  name: '',
  contactPerson: '',
  phone: '',
  email: '',
  address: '',
}

export default function SuppliersPage() {
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')

  const [form, setForm] = useState({ ...emptyForm })
  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState('')
  const [success, setSuccess] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    async function loadSuppliers() {
      try {
        const response = await fetch('/api/suppliers', {
          signal: controller.signal,
        })

        if (!response.ok) {
          throw new Error(
            `Unable to load suppliers (${response.status}).`,
          )
        }

        const data: Supplier[] = await response.json()

        if (!controller.signal.aborted) {
          setSuppliers(data)
        }
      } catch (error) {
        if (controller.signal.aborted) return

        setLoadError(
          error instanceof Error
            ? error.message
            : 'Unable to load suppliers.',
        )
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadSuppliers()

    return () => controller.abort()
  }, [])

  function updateField(
    field: keyof typeof emptyForm,
    value: string,
  ) {
    setForm((current) => ({
      ...current,
      [field]: value,
    }))
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (saving) return

    setFormError('')
    setSuccess('')

    if (!form.code.trim() || !form.name.trim()) {
      setFormError('Supplier code and name are required.')
      return
    }

    setSaving(true)

    try {
      const response = await fetch('/api/suppliers', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          code: form.code.trim(),
          name: form.name.trim(),
          contactPerson: form.contactPerson.trim() || null,
          phone: form.phone.trim() || null,
          email: form.email.trim() || null,
          address: form.address.trim() || null,
        }),
      })

      if (!response.ok) {
        const problem: ApiError | null = await response
          .json()
          .catch(() => null)

        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : ''

        throw new Error(
          validationMessages ||
            problem?.message ||
            problem?.title ||
            `Unable to create supplier (${response.status}).`,
        )
      }

      const supplier: Supplier = await response.json()

      setSuppliers((current) =>
        [...current, supplier].sort((a, b) =>
          a.code.localeCompare(b.code),
        ),
      )

      setForm({ ...emptyForm })
      setSuccess(`Supplier ${supplier.code} created successfully.`)
    } catch (error) {
      setFormError(
        error instanceof Error
          ? error.message
          : 'Unable to create supplier.',
      )
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <header className="page-header">
        <h1>Suppliers</h1>
        <p>Manage suppliers and their contact information.</p>
      </header>

      <section className="panel" aria-labelledby="create-supplier-heading">
        <h2 id="create-supplier-heading">Create Supplier</h2>

        <p className="section-description">
          Supplier code and name are required. Other fields are optional.
        </p>

        <form onSubmit={handleSubmit}>
          <fieldset className="form-fields" disabled={saving}>
            <div className="form-grid">
              <label htmlFor="supplier-code">
                Supplier code *
                <input
                  id="supplier-code"
                  value={form.code}
                  onChange={(event) =>
                    updateField('code', event.target.value)
                  }
                  placeholder="SUP-002"
                  maxLength={30}
                  required
                />
              </label>

              <label htmlFor="supplier-name">
                Supplier name *
                <input
                  id="supplier-name"
                  value={form.name}
                  onChange={(event) =>
                    updateField('name', event.target.value)
                  }
                  placeholder="Demo Steel Supplies"
                  maxLength={200}
                  required
                />
              </label>

              <label htmlFor="supplier-contact">
                Contact person
                <input
                  id="supplier-contact"
                  value={form.contactPerson}
                  onChange={(event) =>
                    updateField('contactPerson', event.target.value)
                  }
                  placeholder="Contact name"
                  maxLength={150}
                />
              </label>

              <label htmlFor="supplier-phone">
                Phone
                <input
                  id="supplier-phone"
                  type="tel"
                  value={form.phone}
                  onChange={(event) =>
                    updateField('phone', event.target.value)
                  }
                  placeholder="Phone number"
                  maxLength={30}
                />
              </label>

              <label htmlFor="supplier-email">
                Email
                <input
                  id="supplier-email"
                  type="email"
                  value={form.email}
                  onChange={(event) =>
                    updateField('email', event.target.value)
                  }
                  placeholder="sales@example.com"
                  maxLength={254}
                />
              </label>

              <label htmlFor="supplier-address">
                Address
                <textarea
                  id="supplier-address"
                  value={form.address}
                  onChange={(event) =>
                    updateField('address', event.target.value)
                  }
                  placeholder="Supplier address"
                  maxLength={500}
                  rows={3}
                />
              </label>
            </div>

            <button className="primary-button" type="submit">
              {saving ? 'Saving...' : 'Create Supplier'}
            </button>
          </fieldset>

          {formError && (
            <p className="error-message" role="alert">
              {formError}
            </p>
          )}

          {success && (
            <p className="success-message" role="status">
              {success}
            </p>
          )}
        </form>
      </section>

      <section className="panel" aria-labelledby="suppliers-heading">
        <h2 id="suppliers-heading">Supplier List</h2>

        {loading && <p role="status">Loading suppliers...</p>}

        {loadError && (
          <p className="error-message" role="alert">
            {loadError}
          </p>
        )}

        {!loading && !loadError && suppliers.length === 0 && (
          <p>No suppliers yet. Create your first supplier above.</p>
        )}

        {!loading && !loadError && suppliers.length > 0 && (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th scope="col">Code</th>
                  <th scope="col">Name</th>
                  <th scope="col">Contact person</th>
                  <th scope="col">Phone</th>
                  <th scope="col">Email</th>
                  <th scope="col">Address</th>
                  <th scope="col">Status</th>
                </tr>
              </thead>

              <tbody>
                {suppliers.map((supplier) => (
                  <tr key={supplier.id}>
                    <td className="project-code">{supplier.code}</td>
                    <td>{supplier.name}</td>
                    <td>{supplier.contactPerson || '—'}</td>
                    <td>{supplier.phone || '—'}</td>
                    <td>{supplier.email || '—'}</td>
                    <td>{supplier.address || '—'}</td>
                    <td>
                      <span className="status-badge">
                        {supplier.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
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