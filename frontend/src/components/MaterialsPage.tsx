import MasterDataPage from './MasterDataPage'
import type { FieldDefinition } from './MasterDataPage'

const createRoles = ['Administrator', 'ProcurementOfficer']

const fields: FieldDefinition[] = [
  {
    name: 'code',
    label: 'Material code',
    required: true,
    maxLength: 30,
  },
  {
    name: 'name',
    label: 'Material name',
    required: true,
    maxLength: 200,
  },
  {
    name: 'category',
    label: 'Category',
    required: true,
    maxLength: 100,
  },
  {
    name: 'unit',
    label: 'Unit',
    type: 'select',
    required: true,
    options: [
      { value: 'bag', label: 'Bag' },
      { value: 'kg', label: 'Kilogram (kg)' },
      { value: 'piece', label: 'Piece' },
      { value: 'm', label: 'Metre (m)' },
      { value: 'm2', label: 'Square metre (m²)' },
      { value: 'm3', label: 'Cubic metre (m³)' },
      { value: 'litre', label: 'Litre' },
    ],
  },
]

export default function MaterialsPage() {
  return (
    <MasterDataPage
      title="Materials"
      singular="Material"
      description="Manage materials used across your construction sites."
      endpoint="/api/materials"
      createRoles={createRoles}
      fields={fields}
      columns={[
        { key: 'code', label: 'Code' },
        { key: 'name', label: 'Name' },
        { key: 'category', label: 'Category' },
        { key: 'unit', label: 'Unit' },
        { key: 'isActive', label: 'Status', format: 'active' },
      ]}
    />
  )
}