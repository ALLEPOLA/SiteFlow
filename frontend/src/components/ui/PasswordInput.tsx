import { useId, useRef, useState } from 'react'
import type { ComponentPropsWithoutRef } from 'react'

type Props = Omit<ComponentPropsWithoutRef<'input'>, 'type'> & { label: string }

export default function PasswordInput({ label, id, disabled, className = '', ...props }: Props) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const input = useRef<HTMLInputElement>(null)
  const [visible, setVisible] = useState(false)

  return (
    <div className="password-field">
      <label htmlFor={inputId}>{label}</label>
      <span className="password-input-control">
        <input {...props} ref={input} id={inputId} type={visible ? 'text' : 'password'}
          disabled={disabled} className={className} spellCheck={false} autoCapitalize="none" autoCorrect="off" />
        <button type="button" className="password-visibility-button"
          disabled={disabled} aria-controls={inputId} aria-pressed={visible}
          aria-label={(visible ? 'Hide ' : 'Show ') + label.toLowerCase()}
          title={visible ? 'Hide password' : 'Show password'}
          onPointerDown={event => event.preventDefault()}
          onClick={() => { setVisible(current => !current); input.current?.focus({ preventScroll: true }) }}>
          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor"
            strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
            {visible ? <><path d="m3 3 18 18M10.6 10.6a2 2 0 0 0 2.8 2.8M9.5 5.3A10.6 10.6 0 0 1 12 5c6 0 10 7 10 7a19.9 19.9 0 0 1-3.2 4M6.3 6.3A21 21 0 0 0 2 12s4 7 10 7a11 11 0 0 0 5.7-1.7" /></>
              : <><path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12Z" /><circle cx="12" cy="12" r="3" /></>}
          </svg>
          <span>{visible ? 'Hide' : 'Show'}</span>
        </button>
      </span>
    </div>
  )
}
