export default function ConstructionAnimation() {
  return (
    <div className="construction-animation" aria-hidden="true">
      <svg viewBox="0 0 480 340" fill="none" focusable="false">
        <circle cx="250" cy="140" r="110" fill="#fff" opacity=".035" />
        <path d="M28 252H452" stroke="#86d3b9" strokeWidth="2" opacity=".35" />
        <path d="M300 250V125H414V250 M310 125V105H402V125" fill="#16483f" stroke="#70b7a2" strokeWidth="2" />
        {[145, 180, 215].map(y => [319, 352, 385].map(x => (
          <rect key={x + '-' + y} x={x} y={y} width="16" height="20" rx="2" fill="#b8d99c" opacity=".55" />
        )))}
        <g stroke="#deb37d" strokeWidth="3" strokeLinejoin="round">
          <path d="M258 250V46H276V250 M258 65L276 87L258 109L276 131L258 153L276 175L258 197L276 219L258 241 M220 46H438L276 20L220 46Z M276 20V46 M244 250H290" />
        </g>
        <g className="sf-crane-load">
          <path d="M404 47V98C404 107 393 107 393 99 M404 108L385 124M404 108L423 124" stroke="#deb37d" strokeWidth="2" strokeLinecap="round" />
          <rect x="382" y="124" width="44" height="25" rx="3" fill="#bc8b59" />
          <path d="M393 124V149M415 124V149" stroke="#efd0a2" strokeWidth="3" />
        </g>
        <ellipse cx="149" cy="253" rx="55" ry="7" fill="#071f1b" opacity=".4" />
        <path d="M123 199L120 241H140L149 204L158 241H178L173 199" fill="#244c60" />
        <path d="M119 237H141V251H109C106 245 111 239 119 237Z M158 237H179L188 246V251H158V237Z" fill="#102b32" />
        <path d="M121 139L103 164L112 188" stroke="#e5a77c" strokeWidth="16" strokeLinecap="round" strokeLinejoin="round" />
        <g className="sf-worker-wave">
          <path d="M174 141L194 119L194 91" stroke="#e5a77c" strokeWidth="16" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M193 95L184 84M193 94L191 77M195 94L199 78M197 97L206 86" stroke="#efbd96" strokeWidth="6" strokeLinecap="round" />
          <circle cx="194" cy="95" r="9" fill="#efbd96" />
        </g>
        <path d="M125 132H170L181 153L174 204H121L114 153L125 132Z" fill="#f4b544" />
        <path d="M137 133L148 154L160 133" fill="#fff1cb" />
        <path d="M132 135V202M164 135V202M122 179H175" stroke="#ecf4b8" strokeWidth="7" />
        <rect x="142" y="163" width="14" height="11" rx="2" fill="#d98b26" />
        <path d="M138 120H157V139H138Z" fill="#e5a77c" />
        <rect x="124" y="76" width="49" height="53" rx="22" fill="#efbd96" />
        <circle cx="135" cy="102" r="2.5" fill="#183b36" />
        <circle cx="160" cy="102" r="2.5" fill="#183b36" />
        <path d="M139 113Q148 121 157 113" stroke="#9a563d" strokeWidth="3" strokeLinecap="round" />
        <path d="M121 86C121 45 175 45 175 86" fill="#f4b544" />
        <path d="M148 58V79" stroke="#ffe1a2" strokeWidth="8" strokeLinecap="round" />
        <rect x="115" y="81" width="66" height="10" rx="5" fill="#ffcc64" />
        <g className="sf-worker-greeting">
          <rect x="43" y="20" width="126" height="42" rx="16" fill="#dcebbb" />
          <path d="M139 59L151 73L153 58" fill="#dcebbb" />
          <text x="106" y="47" textAnchor="middle" fill="#173e35" fontSize="20" fontWeight="700" fontFamily="inherit">Hi there!</text>
        </g>
        <g stroke="#86d3b9" strokeWidth="1.5">
          <rect x="44" y="226" width="45" height="25" rx="2" fill="#285548" />
          <path d="M44 238H89M66 226V238M55 238V251M78 238V251" />
        </g>
<path d="M25 322H455" stroke="#86d3b9" opacity=".3" />
{[210, 325].map((x, i) => (
  <g key={x} transform={'translate(' + x + ' 220)'}>
    <path d="M10 70L5 98M27 70L34 98" stroke="#315569" strokeWidth="11" strokeLinecap="round" />
    <path d="M0 99H13M29 99H42" stroke="#102b32" strokeWidth="6" strokeLinecap="round" />
    <g className={i === 0 ? 'sf-material-carrier' : undefined}>
      <path d="M7 35H31L37 72H0Z" fill={i === 0 ? '#e89a55' : '#d9b549'} />
      <path d="M11 36V69M25 36V69M3 57H34" stroke="#e3edb5" strokeWidth="4" />
      <circle cx="19" cy="20" r="14" fill={i === 0 ? '#b97d59' : '#efbd96'} />
      <path d="M4 17Q4-5 19-5Q34-5 34 17Z" fill={i === 0 ? '#f4b544' : '#eeeede'} />
      <path d="M2 17H36" stroke="#ffdc88" strokeWidth="5" strokeLinecap="round" />
      <circle cx="24" cy="23" r="1.7" fill="#173d33" />
      {i === 0 ? (
        <g>
          <rect x="29" y="26" width="32" height="28" rx="7" fill="#dfd4b4" transform="rotate(-8 29 26)" />
          <path d="M38 34L53 32M38 39L49 37" stroke="#9a946d" strokeWidth="2" />
          <path d="M28 41L39 55L55 47" stroke="#b97d59" strokeWidth="7" strokeLinecap="round" strokeLinejoin="round" />
        </g>
      ) : (
        <g className="sf-bricklayer-arm">
          <path d="M29 42L44 53L59 46" stroke="#efbd96" strokeWidth="7" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M58 47L70 50" stroke="#be8952" strokeWidth="4" />
          <path d="M70 46L84 52L69 55Z" fill="#b8d1c5" />
        </g>
      )}
    </g>
  </g>
))}
<g fill="#b57855" stroke="#e7ba91" strokeWidth="1.5">
  <path d="M380 299H432V320H380Z M388 278H432V299H388Z M380 299H432 M406 299V320 M410 278V299" />
</g>
<g transform="translate(35 273)">
  <path d="M10 10H68L57 31H24Z" fill="#71a28a" stroke="#b5d0ac" strokeWidth="2" />
  <path d="M17 10L8 0H0M26 31L34 42H56M56 31L64 48" stroke="#deb37d" strokeWidth="3" strokeLinecap="round" />
  <circle cx="34" cy="42" r="8" fill="#102b32" stroke="#9cb9a5" strokeWidth="3" />
  <path d="M25 8L31 1L40 5L48 0L57 8" fill="#cfbf9e" />
</g>
{[130, 444].map(x => (
  <g key={x} transform={'translate(' + x + ' 293)'}>
    <path d="M0 26L8 0H13L22 26Z" fill="#e5a060" />
    <path d="M4 14H17" stroke="#f2ecd4" strokeWidth="5" />
    <path d="M-3 28H25" stroke="#c7d2ad" strokeWidth="4" strokeLinecap="round" />
  </g>
))}
      </svg>
    </div>
  )
}

