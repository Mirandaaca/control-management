import { Bar, BarChart, CartesianGrid, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { ArrowDownRight, ArrowUpRight, Minus } from 'lucide-react';
import { Card } from '../ui';
import { MESES, formatearCantidad, formatearMonto } from '../../utils/formato';

// Los colores de los gráficos, como en Reportes (docs/diseno.md): las series en la escala marca y la grilla y los ejes en
// gris. Nunca rojo ni verde, que son de los estados.
const MARCA_300 = '#8bb2dd';
const MARCA_600 = '#2a5899';
const MARCA_800 = '#1c3a66';
const COLOR_EJE = '#475569';
const COLOR_GRILLA = '#e2e8f0';

const montoCorto = new Intl.NumberFormat('es-BO', { notation: 'compact', maximumFractionDigits: 1 });
const eje = { tick: { fontSize: 12, fill: COLOR_EJE }, axisLine: false, tickLine: false };
const ejeDeMonto = { tickFormatter: (v) => `Bs ${montoCorto.format(v)}` };

/** "del 1 al 28 de septiembre", o "el 1 de septiembre" si es un solo día. */
function tramo(hasta) {
  const [, mes, dia] = hasta.split('-').map(Number);
  return dia === 1 ? `el 1 de ${MESES[mes - 1]}` : `del 1 al ${dia} de ${MESES[mes - 1]}`;
}

/**
 * La variación de un indicador contra el mismo tramo del mes anterior (tarea 24): la flecha y el porcentaje, en gris
 * (el rojo y el verde son de los estados). El texto dice "más" o "menos", así no depende de la flecha.
 */
export function Variacion({ actual, anterior, hasta }) {
  if (Number(anterior) === 0)
    return <span className="block text-xs text-slate-600">Sin datos {tramo(hasta)} para comparar</span>;
  const cambio = Math.round(((actual - anterior) / anterior) * 100);
  const Icono = cambio > 0 ? ArrowUpRight : cambio < 0 ? ArrowDownRight : Minus;
  const texto = cambio === 0 ? `Igual que ${tramo(hasta)}` : `${Math.abs(cambio)} % ${cambio > 0 ? 'más' : 'menos'} que ${tramo(hasta)}`;
  return (
    <span className="mt-0.5 flex items-center gap-1 text-xs text-slate-700">
      <Icono size={14} aria-hidden="true" className="shrink-0" />
      {texto}
    </span>
  );
}

function Tarjeta({ titulo, descripcion, children }) {
  return (
    <Card>
      <h3 className="text-sm font-semibold text-slate-900">{titulo}</h3>
      {descripcion && <p className="mt-0.5 text-sm text-slate-600">{descripcion}</p>}
      <div className="mt-4">{children}</div>
    </Card>
  );
}

function Vacio({ children }) {
  return <p className="py-16 text-center text-sm text-slate-600">{children}</p>;
}

function Globo({ active, payload, nombre }) {
  if (!active || !payload?.length) return null;
  const d = payload[0].payload;
  return (
    <div className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm shadow-tarjeta">
      <p className="font-medium text-slate-900">{d[nombre]}</p>
      <p className="tabular-nums text-slate-700">{formatearMonto(d.total)}</p>
    </div>
  );
}

/**
 * Un gráfico de barras de una sola serie (el total vendido), con su lista para el lector de pantalla. El dibujo va oculto
 * para el lector, y sin la capa de teclado de Recharts: dentro de aria-hidden, nada puede recibir el foco (WCAG 4.1.2).
 */
export function Grafico({ titulo, datos, nombre, horizontal = false }) {
  return (
    <Tarjeta titulo={titulo}>
      {datos.length === 0 ? (
        <Vacio>Todavía no hay ventas este mes.</Vacio>
      ) : (
        <figure aria-label={titulo}>
          <div aria-hidden="true">
            <ResponsiveContainer width="100%" height={horizontal ? Math.max(220, datos.length * 40) : 240}>
              {horizontal ? (
                <BarChart data={datos} layout="vertical" margin={{ left: 8, right: 24, top: 4, bottom: 4 }} barCategoryGap={8}
                  accessibilityLayer={false}>
                  <CartesianGrid horizontal={false} stroke={COLOR_GRILLA} />
                  <XAxis type="number" {...eje} {...ejeDeMonto} />
                  <YAxis type="category" dataKey={nombre} width={140} {...eje} />
                  <Tooltip content={<Globo nombre={nombre} />} cursor={{ fill: '#f1f5f9' }} />
                  <Bar dataKey="total" fill={MARCA_600} radius={[0, 4, 4, 0]} maxBarSize={28} />
                </BarChart>
              ) : (
                <BarChart data={datos} margin={{ left: 8, right: 8, top: 4, bottom: 4 }} accessibilityLayer={false}>
                  <CartesianGrid vertical={false} stroke={COLOR_GRILLA} />
                  <XAxis dataKey={nombre} interval="preserveStartEnd" {...eje} />
                  <YAxis width={72} {...eje} {...ejeDeMonto} />
                  <Tooltip content={<Globo nombre={nombre} />} cursor={{ fill: '#f1f5f9' }} />
                  <Bar dataKey="total" fill={MARCA_600} radius={[4, 4, 0, 0]} maxBarSize={28} />
                </BarChart>
              )}
            </ResponsiveContainer>
          </div>
          <ul className="sr-only">
            {datos.map((d) => <li key={d[nombre]}>{d[nombre]}: {formatearMonto(d.total)}</li>)}
          </ul>
        </figure>
      )}
    </Tarjeta>
  );
}

function Leyenda({ series }) {
  return (
    <ul aria-hidden="true" className="mb-3 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-700">
      {series.map(([color, texto]) => (
        <li key={texto} className="flex items-center gap-1.5">
          <span className="size-3 rounded-sm" style={{ backgroundColor: color }} />
          {texto}
        </li>
      ))}
    </ul>
  );
}

function GloboDeTendencia({ active, payload }) {
  if (!active || !payload?.length) return null;
  const d = payload[0].payload;
  return (
    <div className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm shadow-tarjeta tabular-nums">
      <p className="font-medium text-slate-900">{d.nombre}</p>
      <p className="text-slate-700">Contado: {formatearMonto(d.contado)}</p>
      <p className="text-slate-700">Crédito: {formatearMonto(d.credito)}</p>
      <p className="font-medium text-slate-900">Total: {formatearMonto(d.contado + d.credito)}</p>
    </div>
  );
}

/** Las ventas de los últimos 6 meses de Bolivia, al contado y a crédito apilados. El mes en curso va hasta hoy. */
export function TendenciaDeVentas({ meses }) {
  const datos = meses.map((m) => {
    const [anio, mes] = m.mes.split('-').map(Number);
    return { ...m, corto: MESES[mes - 1].slice(0, 3), nombre: `${MESES[mes - 1]} de ${anio}` };
  });
  const titulo = 'Ventas de los últimos 6 meses';
  return (
    <Tarjeta titulo={titulo} descripcion="Al contado y a crédito; el mes en curso, hasta hoy.">
      {datos.every((d) => d.contado + d.credito === 0) ? (
        <Vacio>Todavía no hay ventas en estos meses.</Vacio>
      ) : (
        <figure aria-label={titulo}>
          <Leyenda series={[[MARCA_600, 'Contado'], [MARCA_300, 'Crédito']]} />
          <div aria-hidden="true">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={datos} margin={{ left: 8, right: 8, top: 4, bottom: 4 }} accessibilityLayer={false}>
                <CartesianGrid vertical={false} stroke={COLOR_GRILLA} />
                <XAxis dataKey="corto" {...eje} />
                <YAxis width={72} {...eje} {...ejeDeMonto} />
                <Tooltip content={<GloboDeTendencia />} cursor={{ fill: '#f1f5f9' }} />
                <Bar dataKey="contado" stackId="ventas" fill={MARCA_600} maxBarSize={36} />
                <Bar dataKey="credito" stackId="ventas" fill={MARCA_300} radius={[4, 4, 0, 0]} maxBarSize={36} />
              </BarChart>
            </ResponsiveContainer>
          </div>
          <ul className="sr-only">
            {datos.map((d) => (
              <li key={d.mes}>{d.nombre}: contado {formatearMonto(d.contado)}, crédito {formatearMonto(d.credito)}</li>
            ))}
          </ul>
        </figure>
      )}
    </Tarjeta>
  );
}

// El nombre, el del eje (corto: en el celular, los largos se pisan) y el color.
const TRAMOS = {
  'al-dia': ['Al día', 'Al día', MARCA_300],
  '1-30': ['1 a 30 días de mora', '1-30', '#5a8dcb'],
  '31-60': ['31 a 60 días de mora', '31-60', MARCA_600],
  '61-90': ['61 a 90 días de mora', '61-90', MARCA_800],
  'mas-de-90': ['Más de 90 días de mora', '+90', '#0e1d33'],
};

function GloboDeAntiguedad({ active, payload }) {
  if (!active || !payload?.length) return null;
  const d = payload[0].payload;
  return (
    <div className="rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm shadow-tarjeta tabular-nums">
      <p className="font-medium text-slate-900">{d.nombre}</p>
      <p className="text-slate-700">{formatearMonto(d.saldo)}</p>
      <p className="text-slate-700">{d.cantidad === 1 ? '1 cobranza' : `${formatearCantidad(d.cantidad)} cobranzas`}</p>
    </div>
  );
}

/** Lo que se debe en las cobranzas abiertas, por días de mora: cuanto más vieja la deuda, más oscura la barra. */
export function AntiguedadDeSaldos({ tramos }) {
  const datos = tramos.map((t) => ({ ...t, nombre: TRAMOS[t.tramo][0], corto: TRAMOS[t.tramo][1], color: TRAMOS[t.tramo][2] }));
  const titulo = 'Lo que se debe, por antigüedad';
  return (
    <Tarjeta titulo={titulo} descripcion="El saldo de las cobranzas abiertas, según sus días de mora (al día, de 1 a 30…).">
      {datos.every((d) => d.cantidad === 0) ? (
        <Vacio>No hay deudas por cobrar.</Vacio>
      ) : (
        <figure aria-label={titulo}>
          <div aria-hidden="true">
            <ResponsiveContainer width="100%" height={240}>
              <BarChart data={datos} margin={{ left: 8, right: 8, top: 4, bottom: 4 }} accessibilityLayer={false}>
                <CartesianGrid vertical={false} stroke={COLOR_GRILLA} />
                <XAxis dataKey="corto" interval={0} {...eje} />
                <YAxis width={72} {...eje} {...ejeDeMonto} />
                <Tooltip content={<GloboDeAntiguedad />} cursor={{ fill: '#f1f5f9' }} />
                <Bar dataKey="saldo" radius={[4, 4, 0, 0]} maxBarSize={40}>
                  {datos.map((d) => <Cell key={d.tramo} fill={d.color} />)}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
          <ul className="sr-only">
            {datos.map((d) => (
              <li key={d.tramo}>{d.nombre}: {formatearMonto(d.saldo)} en {formatearCantidad(d.cantidad)} cobranzas</li>
            ))}
          </ul>
        </figure>
      )}
    </Tarjeta>
  );
}

/**
 * Las visitas de las rutas del mes (del 1 a hoy), por promotor: cuántas visitó, en cuántas el cliente estaba ausente y
 * cuántas quedan, con su cumplimiento. Es una lista con barras, no un gráfico: se lee igual con el lector de pantalla.
 */
export function VisitasPorPromotor({ promotores }) {
  return (
    <Tarjeta titulo="Visitas del mes" descripcion="Las rutas del 1 a hoy: visitadas, con el cliente ausente y por visitar.">
      {promotores.length === 0 ? (
        <Vacio>No hay rutas planificadas este mes.</Vacio>
      ) : (
        <>
          <Leyenda series={[[MARCA_600, 'Visitadas'], ['#94a3b8', 'Ausentes'], ['#e2e8f0', 'Por visitar']]} />
          <ul className="space-y-4">
            {promotores.map((p) => {
              const porVisitar = p.planificadas - p.visitadas - p.ausentes;
              const cumplimiento = Math.round((p.visitadas / p.planificadas) * 100);
              const ancho = (n) => `${(n / p.planificadas) * 100}%`;
              return (
                <li key={p.promotorId}>
                  <div className="flex flex-wrap items-baseline justify-between gap-x-3 text-sm">
                    <span className="font-medium text-slate-900">{p.promotor}</span>
                    <span className="tabular-nums text-slate-700">
                      {p.visitadas} de {p.planificadas} visitadas ({cumplimiento} %)
                      <span className="sr-only">, {p.ausentes} ausentes y {porVisitar} por visitar</span>
                    </span>
                  </div>
                  <div aria-hidden="true" className="mt-1.5 flex h-2.5 overflow-hidden rounded-full bg-slate-200">
                    <span style={{ width: ancho(p.visitadas), backgroundColor: MARCA_600 }} />
                    <span style={{ width: ancho(p.ausentes), backgroundColor: '#94a3b8' }} />
                  </div>
                </li>
              );
            })}
          </ul>
        </>
      )}
    </Tarjeta>
  );
}
