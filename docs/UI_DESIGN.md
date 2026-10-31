# SiteFlow interface

The frontend uses Tailwind CSS 4 through the official Vite plugin. Shared tokens
and base typography live in `frontend/src/index.css`; reusable component styles
live in `frontend/src/App.css` using Tailwind's components layer. Tailwind utilities
can be used directly in JSX. Setup follows the [Tailwind Vite guide](https://tailwindcss.com/docs/installation/using-vite).

The design uses a forest sidebar, light neutral work surfaces, sage accents, and
amber or terracotta status treatments. Shared forms, tables, feedback, and buttons
use the same styles across the existing modules. Icons and the construction
illustration are local SVG, and fonts use the system stack with no external requests.

The sidebar becomes a drawer below 1024 px. It supports Escape, keyboard focus
containment, backdrop dismissal, and focus restoration. Closed mobile navigation
is hidden from keyboard interaction. Tables scroll inside their containers while
forms become one column on smaller screens. A skip link provides direct content access.

Page entrances, hover effects, floating workflow icons, and loading placeholders
provide motion feedback. `prefers-reduced-motion` disables decorative motion.
Print styles remove navigation and interactive controls.

This refresh changes presentation and navigation layout. Existing authentication,
API requests, role restrictions, forms, validation, and ERP transaction logic are
retained. Dashboard figures continue to come from the existing dashboard endpoint;
the interface contains no hard-coded business metrics.

Run `npm run build` and `npm run lint` from `frontend` to check changes. Restart an
already running Vite server after adding the Tailwind plugin.
