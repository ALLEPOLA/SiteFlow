import MasterDataPage from './MasterDataPage'
import type { FieldDefinition } from './MasterDataPage'

const createRoles = ['Administrator']

const fields: FieldDefinition[] = [
  {
    name: 'code',
    label: 'Project code',
    required: true,
    maxLength: 30,
  },
  {
    name: 'name',
    label: 'Project name',
    required: true,
    maxLength: 200,
  },
  {
    name: 'location',
    label: 'Location',
    required: true,
    maxLength: 200,
  },
  {
    name: 'budget',
    label: 'Budget (LKR)',
    type: 'number',
    required: true,
    min: '0',
    step: '0.01',
  },
]

export default function ProjectsPage() {
  return (
    <MasterDataPage
      title="Construction Projects"
      singular="Project"
      description="Manage your construction sites and project budgets."
      endpoint="/api/projects"
      createRoles={createRoles}
      fields={fields}
      columns={[
        { key: 'code', label: 'Code' },
        { key: 'name', label: 'Project name' },
        { key: 'location', label: 'Location' },
        { key: 'budget', label: 'Budget', format: 'money' },
        { key: 'status', label: 'Status' },
      ]}
    />
  )
}