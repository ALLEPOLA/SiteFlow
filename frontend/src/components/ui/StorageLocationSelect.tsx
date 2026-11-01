import { useId } from 'react'
import { useData } from '../erp/ErpUi'

export type StorageLocationOption = { id: number; storeId: number; projectId: number; storeCode: string; storeName: string; code: string; name: string; rack: string; bin: string; isActive: boolean; storeActive: boolean }
type Props = { projectId: number | string | undefined; value: string; onChange: (value: string) => void; label?: string; optional?: boolean; disabled?: boolean }
export default function StorageLocationSelect({ projectId, value, onChange, label = 'Storage location', optional = false, disabled = false }: Props) {
  const id = useId()
  const enabled = Number(projectId) > 0
  const locations = useData<StorageLocationOption[]>('/api/stores/locations?projectId=' + projectId, enabled)
  return <label htmlFor={id}>{label}<select id={id} value={value} onChange={e => onChange(e.target.value)}
    required={!optional} disabled={disabled || !enabled || locations.loading || !!locations.error}>
    <option value="">{!enabled ? 'Choose a project first' : locations.loading ? 'Loading locations...' : optional ? 'All locations in this project' : 'Choose a store / location'}</option>
    {!locations.loading && locations.data?.filter(l => l.projectId === Number(projectId)).map(l => <option key={l.id} value={l.id}>{l.storeCode} — {l.storeName} / {l.code} — {l.name}{l.rack ? ' / Rack ' + l.rack : ''}{l.bin ? ' / Bin ' + l.bin : ''}</option>)}
  </select>{locations.error && <small role="alert">{locations.error}</small>}{enabled && !locations.loading && !locations.error && locations.data?.length === 0 && <small>No active locations. Ask a storekeeper or administrator to configure one.</small>}</label>
}
