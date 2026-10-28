import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';
import { Boxes, ClipboardList, House, Store, TrendingUp, TriangleAlert, Wallet } from 'lucide-react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import {
  Card, ErrorDeCarga, EsqueletoDeGrafico, EsqueletoDeIndicadores, EstadoVacio, PageHeader, ZonaDeCarga,
} from '../components/ui';
import { useAuth } from '../context/auth';
import { PANTALLAS, PERMISOS } from '../context/permisos';
import { ahoraEnBolivia, formatearCantidad, formatearDia, formatearMonto } from '../utils/formato';

// Los colores de los gráficos, como en Reportes (docs/diseno.md): la barra en marca-600 y la grilla y los ejes en gris.
const COLOR_BARRA = '#2a5899';
const COLOR_EJE = '#475569';
const COLOR_GRILLA = '#e2e8f0';
const MESES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre',
  'noviembre', 'diciembre'];

const { cobranzas, reportes, seguridad } = PERMISOS;
const montoCorto = new Intl.NumberFormat('es-BO', { notation: 'compact', maximumFractionDigits: 1 });

/**
 * Inicio (7.3.31, paso 2.4): el mes en curso de Bolivia, con la cartera del usuario (GET /reportes/tablero). Cada
 * indicador se ve si el usuario puede abrir una pantalla con ese dato, y al tocarlo la abre; quien no ve ninguno (el
 * administrador del sistema) no pide el tablero.
 */
export default function Dashboard() {
  const { tiene } = useAuth();
  const [tablero, setTablero] = useState(null);
  const [error, setError] = useState('');
  const [reintentando, setReintentando] = useState(false);

  // La primera pantalla de la lista que el usuario puede abrir, o undefined si no puede abrir ninguna.
  const destino = (...opciones) => opciones.find(([, permisos]) => tiene(...permisos))?.[0];
  // Reportes, en la pestaña de ese dato; si no, la lista.
  const ventas = destino(['/reportes', [reportes.ventasPorZona]], ['/reportes?pestana=promotores', [reportes.ventasPorPromotor]],
    ['/hojas-pedido', PANTALLAS.hojasPedido]);
  const indicadores = [
    { etiqueta: 'Clientes activos', icono: Store, valor: (t) => formatearCantidad(t.clientesActivos),
      a: destino(['/clientes', PANTALLAS.clientes], ['/clientes/mapa', PANTALLAS.mapaClientes]) },
    { etiqueta: 'Hojas de pedido del mes', icono: ClipboardList, valor: (t) => formatearCantidad(t.hojasDelMes), a: ventas },
    { etiqueta: 'Ventas del mes', icono: TrendingUp, valor: (t) => formatearMonto(t.ventasDelMes), a: ventas },
    { etiqueta: 'Por cobrar', icono: Wallet, valor: (t) => formatearMonto(t.porCobrar),
      a: destino(['/cobranzas', PANTALLAS.cobranzas], ['/reportes?pestana=cobranzas', [cobranzas.generarReporte]]) },
    { etiqueta: 'Eventos pendientes', icono: TriangleAlert, valor: (t) => formatearCantidad(t.eventosPendientes),
      a: destino(['/eventos', PANTALLAS.eventos], ['/eventos/mapa', PANTALLAS.mapaEventos],
        ['/reportes/eventos', PANTALLAS.reporteEventos]) },
    { etiqueta: 'Productos bajo el mínimo', icono: Boxes, valor: (t) => formatearCantidad(t.stockBajo),
      a: destino(['/inventario', PANTALLAS.inventario], ['/reportes?pestana=inventario', [reportes.inventario]]) },
  ].filter((i) => i.a);
  const hayIndicadores = indicadores.length > 0;
  const veCarteraCompleta = tiene(seguridad.verCarteraCompleta);

  const pedir = useCallback(() => api.get('/reportes/tablero')
    .then(({ data }) => { setTablero(data); setError(''); })
    .catch((err) => setError(mensajeDeError(err, 'No se pudo cargar el resumen del mes.')))
    .finally(() => setReintentando(false)), []);

  useEffect(() => { if (hayIndicadores) pedir(); }, [hayIndicadores, pedir]);

  const [anio, mes] = ahoraEnBolivia().split('-').map(Number);
  const subtitulo = `Las ventas y los pendientes de ${MESES[mes - 1]} de ${anio}${veCarteraCompleta ? '' : ', de tu cartera'}.`;

  if (!hayIndicadores) return (
    <Layout>
      <PageHeader title="Inicio" />
      <Card><EstadoVacio titulo="Elige una opción del menú para empezar." icono={House} /></Card>
    </Layout>
  );

  let contenido;
  if (!tablero && error) contenido = (
    <Card className="p-0!">
      <ErrorDeCarga mensaje={error} reintentando={reintentando} onReintentar={() => { setReintentando(true); pedir(); }} />
    </Card>
  );
  else if (!tablero) contenido = (
    <ZonaDeCarga texto="Cargando el resumen…">
      <EsqueletoDeIndicadores cantidad={indicadores.length} />
      {ventas && (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          {Array.from({ length: veCarteraCompleta ? 3 : 2 }, (_, i) => <EsqueletoDeGrafico key={i} />)}
        </div>
      )}
    </ZonaDeCarga>
  );
  else contenido = (
    <>
      <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3 mb-6">
        {indicadores.map(({ etiqueta, icono: Icono, valor, a }) => (
          <li key={etiqueta}>
            <Link to={a}
              className="flex items-center gap-4 rounded-xl border border-slate-200 bg-white p-5 shadow-tarjeta transition-colors hover:border-marca-300 hover:bg-marca-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 focus-visible:ring-offset-2">
              <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-marca-50 text-marca-600">
                <Icono size={20} aria-hidden="true" />
              </span>
              <span className="min-w-0">
                <span className="block text-sm text-slate-600">{etiqueta}</span>
                <span className="block text-2xl font-semibold text-slate-900 tabular-nums">{valor(tablero)}</span>
              </span>
            </Link>
          </li>
        ))}
      </ul>
      {ventas && (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          {veCarteraCompleta && (
            <Grafico titulo="Ventas por promotor" datos={tablero.porPromotor} nombre="promotor" horizontal />
          )}
          <Grafico titulo="Productos más vendidos" datos={tablero.porProducto} nombre="producto" horizontal />
          <Grafico titulo="Ventas por día" datos={tablero.porDia.map((d) => ({ ...d, dia: formatearDia(d.fecha).slice(0, 5) }))}
            nombre="dia" />
        </div>
      )}
    </>
  );

  return (
    <Layout>
      <PageHeader title="Inicio" subtitle={subtitulo} />
      {contenido}
    </Layout>
  );
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
function Grafico({ titulo, datos, nombre, horizontal = false }) {
  const eje = { tick: { fontSize: 12, fill: COLOR_EJE }, axisLine: false, tickLine: false };
  const monto = { tickFormatter: (v) => `Bs ${montoCorto.format(v)}` };
  return (
    <Card>
      <h3 className="text-sm font-semibold text-slate-900 mb-4">{titulo}</h3>
      {datos.length === 0 ? (
        <p className="py-16 text-center text-sm text-slate-600">Todavía no hay ventas este mes.</p>
      ) : (
        <figure aria-label={titulo}>
          <div aria-hidden="true">
            <ResponsiveContainer width="100%" height={horizontal ? Math.max(220, datos.length * 40) : 240}>
              {horizontal ? (
                <BarChart data={datos} layout="vertical" margin={{ left: 8, right: 24, top: 4, bottom: 4 }} barCategoryGap={8}
                  accessibilityLayer={false}>
                  <CartesianGrid horizontal={false} stroke={COLOR_GRILLA} />
                  <XAxis type="number" {...eje} {...monto} />
                  <YAxis type="category" dataKey={nombre} width={140} {...eje} />
                  <Tooltip content={<Globo nombre={nombre} />} cursor={{ fill: '#f1f5f9' }} />
                  <Bar dataKey="total" fill={COLOR_BARRA} radius={[0, 4, 4, 0]} maxBarSize={28} />
                </BarChart>
              ) : (
                <BarChart data={datos} margin={{ left: 8, right: 8, top: 4, bottom: 4 }} accessibilityLayer={false}>
                  <CartesianGrid vertical={false} stroke={COLOR_GRILLA} />
                  <XAxis dataKey={nombre} interval="preserveStartEnd" {...eje} />
                  <YAxis width={72} {...eje} {...monto} />
                  <Tooltip content={<Globo nombre={nombre} />} cursor={{ fill: '#f1f5f9' }} />
                  <Bar dataKey="total" fill={COLOR_BARRA} radius={[4, 4, 0, 0]} maxBarSize={28} />
                </BarChart>
              )}
            </ResponsiveContainer>
          </div>
          <ul className="sr-only">
            {datos.map((d) => <li key={d[nombre]}>{d[nombre]}: {formatearMonto(d.total)}</li>)}
          </ul>
        </figure>
      )}
    </Card>
  );
}
