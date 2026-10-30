import MasterDataPage from './MasterDataPage'
import type { FieldDefinition } from './MasterDataPage'

const createRoles = ['Administrator', 'ProcurementOfficer']

const fields: FieldDefinition[] = [
  {
    name: 'code',
    label: 'Supplier code',
    required: true,
    maxLength: 30,
  },
  {
    name: 'name',
    label: 'Supplier name',
    required: true,
    maxLength: 200,
  },
  {
    name: 'contactPerson',
    label: 'Contact person',
    maxLength: 150,
  },
  {
    name: 'phone',
    label: 'Phone',
    type: 'tel',
    maxLength: 30,
  },
  {
    name: 'email',
    label: 'Email',
    type: 'email',
    maxLength: 254,
  },
  {
    name: 'address',
    label: 'Address',
    type: 'textarea',
    maxLength: 500,
  },
]

export default function SuppliersPage() {
  return (
    <MasterDataPage
      title="Suppliers"
      singular="Supplier"
      description="Manage suppliers and their contact information."
      endpoint="/api/suppliers"
      createRoles={createRoles}
      fields={fields}
      columns={[
        { key: 'code', label: 'Code' },
        { key: 'name', label: 'Name' },
        { key: 'contactPerson', label: 'Contact person' },
        { key: 'phone', label: 'Phone' },
        { key: 'email', label: 'Email' },
        { key: 'address', label: 'Address' },
        { key: 'isActive', label: 'Status', format: 'active' },
      ]}
    />
  )
}