type Props = { name: string; className?: string }

const paths: Record<string, string> = {
  stores: 'M3 10h18v11H3z M2 10l3-7h14l3 7 M7 14h4v7H7z M15 14h3 M15 18h3 M7 6h10',
  reservations: 'M4 10h16v11H4z M3 10l3-6h12l3 6 M9 10V7a3 3 0 0 1 6 0v3 M9 15l2 2 4-4',
  planning: 'M4 4h16v17H4z M8 2v4 M16 2v4 M4 9h16 M8 13h3 M8 17h3 M14 15l2 2 4-5',
  dashboard: 'M3 3h7v7H3z M14 3h7v4h-7z M14 11h7v10h-7z M3 14h7v7H3z',
  projects: 'M3 21V7l9-4 9 4v14 M8 21v-5h8v5 M8 8v2 M16 8v2 M8 12v1 M16 12v1',
  costs: 'M4 20h16 M7 16v-5 M12 16V5 M17 16V8',
  materials: 'm12 3 9 5-9 5-9-5 9-5z M3 8v9l9 5 9-5V8 M12 13v9 M7 5.8l9 5',
  suppliers: 'M3 21V9l9-6 9 6v12 M3 12h18 M7 12v9 M17 12v9 M10 21v-5h4v5',
  requests: 'M8 4H5v17h14V4h-3 M8 3h8v4H8z M8 12h8 M8 16h5',
  orders: 'M7 3H4v18h16V3h-3 M8 3h8v4H8z M8 12h8 M8 16h8',
  receipts: 'M3 7h11v11H3z M14 11h4l3 4v3h-7 M6 7V4h8 M5 18v2 M18 18v2 M7 11v4 M5 13h4',
  inventory: 'M3 10h18v11H3z M2 10l3-7h14l3 7 M8 21v-7h8v7 M8 6h8',
  issues: 'M14 3H4v18h10 M10 12h11 M17 8l4 4-4 4',
  corrections: 'M4 11a8 8 0 1 1 2 7 M4 4v7h7',
  'stock-operations': 'M3 7h17 M16 3l4 4-4 4 M21 17H4 M8 13l-4 4 4 4',
  accounts: 'M3 6h18v14H3z M3 10h18 M15 15h3 M6 6V3h12v3',
  documents: 'M4 4h6l2 3h8v14H4z M8 12h8 M8 16h5',
  notifications: 'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9 M9 21h6',
  reports: 'M5 3h10l4 4v14H5z M14 3v5h5 M9 16v-3 M12 16v-5 M15 16v-2',
  audit: 'M12 3 3 7v5c0 5 9 9 9 9s9-4 9-9V7l-9-4z M8 12l3 3 5-6',
  users: 'M15 21v-3a4 4 0 0 0-4-4H7a4 4 0 0 0-4 4v3 M9 10a4 4 0 1 0 0-8 4 4 0 0 0 0 8 M17 3a4 4 0 0 1 0 8 M18 14a4 4 0 0 1 3 4v3',
  profile: 'M20 21v-3a6 6 0 0 0-6-6h-4a6 6 0 0 0-6 6v3 M12 9a4 4 0 1 0 0-8 4 4 0 0 0 0 8',
  arrow: 'M5 12h14 M14 7l5 5-5 5',
  menu: 'M4 6h16 M4 12h16 M4 18h16',
  close: 'm6 6 12 12 M6 18 18 6',
  logout: 'M10 3H4v18h6 M9 12h12 M17 8l4 4-4 4',
  refresh: 'M20 8a8 8 0 0 0-14-3L3 8 M3 3v5h5 M4 16a8 8 0 0 0 14 3l3-3 M16 16h5v5',
  check: 'm5 12 4 4L19 6',
  lock: 'M5 10h14v11H5z M8 10V6a4 4 0 0 1 8 0v4 M12 14v3',
}

export default function Icon({ name, className = '' }: Props) {
  return <svg className={`ui-icon ${className}`} width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.65" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name] ?? paths.materials} /></svg>
}
