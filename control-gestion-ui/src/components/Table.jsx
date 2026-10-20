export function Table({ headers, children, empty = 'Sin registros.' }) {
  return (
    <div className="bg-white rounded-xl shadow-tarjeta border border-slate-200 overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-slate-50 border-b border-slate-200">
              {headers.map((h) => (
                <th key={h} className="px-4 py-3 text-left text-xs font-semibold text-slate-600 uppercase tracking-wide whitespace-nowrap">
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200">
            {children}
          </tbody>
        </table>
      </div>
      {!children || (Array.isArray(children) && children.length === 0) ? (
        <p className="text-center text-slate-500 text-sm py-10">{empty}</p>
      ) : null}
    </div>
  );
}

export function Td({ children, mono = false }) {
  return (
    <td className={`px-4 py-2.5 text-slate-900 ${mono ? 'font-mono text-xs' : ''}`}>
      {children}
    </td>
  );
}

export function Tr({ children }) {
  return (
    <tr className="hover:bg-slate-50 transition-colors">
      {children}
    </tr>
  );
}
