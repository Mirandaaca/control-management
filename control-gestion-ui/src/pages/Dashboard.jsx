import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Boxes, ClipboardList, HandCoins, House, Store, TrendingUp, TriangleAlert, UserPlus, Wallet } from 'lucide-react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import {
  AntiguedadDeSaldos, Grafico, TendenciaDeVentas, Variacion, VisitasPorPromotor,
} from '../components/inicio/Graficos';
import {
  Card, ErrorDeCarga, EsqueletoDeGrafico, EsqueletoDeIndicadores, EstadoVacio, PageHeader, ZonaDeCarga,
} from '../components/ui';
import { useAuth } from '../context/auth';
import { PANTALLAS, PERMISOS } from '../context/permisos';
import { MESES, ahoraEnBolivia, formatearCantidad, formatearDia, formatearMonto } from '../utils/formato';

const { cobranzas, reportes, seguridad, territorial } = PERMISOS;

/**
 * Inicio (7.3.31, paso 2.4): el mes en curso de Bolivia, con la cartera del usuario (GET /reportes/tablero), y su
 * análisis (GET /reportes/tablero/analisis, tarea 24): la variación contra el mes anterior, la tendencia de las ventas,
 * lo que se debe por antigüedad, los mejores clientes y las visitas. Cada indicador se ve si el usuario puede abrir una
 * pantalla con ese dato, y al tocarlo la abre; cada bloque del análisis, si la API lo manda (el permiso de su reporte).
 * Quien no ve ningún indicador (el administrador del sistema) no pide el tablero.
 */
export default function Dashboard() {
  const { tiene } = useAuth();
  const [tablero, setTablero] = useState(null);
  const [analisis, setAnalisis] = useState(null);
  const [error, setError] = useState('');
  const [errorAnalisis, setErrorAnalisis] = useState('');
  const [reintentando, setReintentando] = useState(false);
  const [reintentandoAnalisis, setReintentandoAnalisis] = useState(false);

  // La primera pantalla de la lista que el usuario puede abrir, o undefined si no puede abrir ninguna.
  const destino = (...opciones) => opciones.find(([, permisos]) => tiene(...permisos))?.[0];
  // Reportes, en la pestaña de ese dato; si no, la lista.
  const ventas = destino(['/reportes', [reportes.ventasPorZona]], ['/reportes?pestana=promotores', [reportes.ventasPorPromotor]],
    ['/hojas-pedido', PANTALLAS.hojasPedido]);
  const aCobranzas = destino(['/cobranzas', PANTALLAS.cobranzas], ['/reportes?pestana=cobranzas', [cobranzas.generarReporte]]);
  const aClientes = destino(['/clientes', PANTALLAS.clientes], ['/clientes/mapa', PANTALLAS.mapaClientes]);
  // "deAnalisis": el bloque del análisis con el dato; sin él (la API no lo mandó o falló), el indicador no se muestra.
  const indicadores = [
    { etiqueta: 'Clientes activos', icono: Store, valor: (t) => formatearCantidad(t.clientesActivos), a: aClientes },
    { etiqueta: 'Clientes nuevos del mes', icono: UserPlus, deAnalisis: 'clientesNuevos', formato: formatearCantidad,
      a: destino(['/reportes?pestana=clientes', [reportes.clientesNuevosRecurrentes]], ['/clientes', PANTALLAS.clientes]) },
    { etiqueta: 'Hojas de pedido del mes', icono: ClipboardList, valor: (t) => formatearCantidad(t.hojasDelMes), a: ventas },
    { etiqueta: 'Ventas del mes', icono: TrendingUp, valor: (t) => formatearMonto(t.ventasDelMes), variacion: 'ventas', a: ventas },
    { etiqueta: 'Cobrado del mes', icono: HandCoins, deAnalisis: 'cobrado', formato: formatearMonto, a: aCobranzas },
    { etiqueta: 'Por cobrar', icono: Wallet, valor: (t) => formatearMonto(t.porCobrar), a: aCobranzas },
    { etiqueta: 'Eventos pendientes', icono: TriangleAlert, valor: (t) => formatearCantidad(t.eventosPendientes),
      a: destino(['/eventos', PANTALLAS.eventos], ['/eventos/mapa', PANTALLAS.mapaEventos],
        ['/reportes/eventos', PANTALLAS.reporteEventos]) },
    { etiqueta: 'Productos bajo el mínimo', icono: Boxes, valor: (t) => formatearCantidad(t.stockBajo),
      a: destino(['/inventario', PANTALLAS.inventario], ['/reportes?pestana=inventario', [reportes.inventario]]) },
  ].filter((i) => i.a);
  const hayIndicadores = indicadores.length > 0;
  const veCarteraCompleta = tiene(seguridad.verCarteraCompleta);
  // Para el esqueleto: los gráficos que probablemente vengan.
  const graficos = (ventas ? (veCarteraCompleta ? 5 : 4) : 0) + (tiene(cobranzas.consultarDeudas, cobranzas.generarReporte) ? 1 : 0)
    + (tiene(territorial.planificarRuta, territorial.seguirRuta) ? 1 : 0);

  const pedirAnalisis = useCallback(() => api.get('/reportes/tablero/analisis')
    .then(({ data }) => { setAnalisis(data); setErrorAnalisis(''); })
    .catch((err) => setErrorAnalisis(mensajeDeError(err, 'No se pudo cargar la comparación con el mes anterior.')))
    .finally(() => setReintentandoAnalisis(false)), []);

  // El análisis llega junto con el tablero, así la pantalla no se rearma; si falla, el tablero se ve igual.
  const pedir = useCallback(() => Promise.all([
    api.get('/reportes/tablero')
      .then(({ data }) => { setTablero(data); setError(''); })
      .catch((err) => setError(mensajeDeError(err, 'No se pudo cargar el resumen del mes.'))),
    pedirAnalisis(),
  ]).finally(() => setReintentando(false)), [pedirAnalisis]);

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
      <EsqueletoDeIndicadores cantidad={indicadores.length} grilla="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4 mb-6" />
      {graficos > 0 && (
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
          {Array.from({ length: graficos }, (_, i) => <EsqueletoDeGrafico key={i} />)}
        </div>
      )}
    </ZonaDeCarga>
  );
  else contenido = (
    <>
      <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4 mb-6">
        {indicadores.filter((i) => !i.deAnalisis || analisis?.[i.deAnalisis]).map((i) => {
          const { etiqueta, icono: Icono, a } = i;
          const bloque = analisis?.[i.deAnalisis ?? i.variacion];
          return (
            <li key={etiqueta}>
              <Link to={a}
                className="flex h-full items-center gap-4 rounded-xl border border-slate-200 bg-white p-5 shadow-tarjeta transition-colors hover:border-marca-300 hover:bg-marca-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 focus-visible:ring-offset-2">
                <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-marca-50 text-marca-600">
                  <Icono size={20} aria-hidden="true" />
                </span>
                <span className="min-w-0">
                  <span className="block text-sm text-slate-600">{etiqueta}</span>
                  <span className="block text-2xl font-semibold text-slate-900 tabular-nums">
                    {i.deAnalisis ? i.formato(bloque.actual) : i.valor(tablero)}
                  </span>
                  {bloque && <Variacion actual={bloque.actual} anterior={bloque.anterior} hasta={analisis.hastaElMesAnterior} />}
                </span>
              </Link>
            </li>
          );
        })}
      </ul>
      {errorAnalisis && (
        <Card className="p-0! mb-6">
          <ErrorDeCarga mensaje={errorAnalisis} reintentando={reintentandoAnalisis}
            onReintentar={() => { setReintentandoAnalisis(true); pedirAnalisis(); }} />
        </Card>
      )}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {analisis?.tendencia && <TendenciaDeVentas meses={analisis.tendencia} />}
        {ventas && (
          <Grafico titulo="Ventas por día" datos={tablero.porDia.map((d) => ({ ...d, dia: formatearDia(d.fecha).slice(0, 5) }))}
            nombre="dia" />
        )}
        {ventas && veCarteraCompleta && (
          <Grafico titulo="Ventas por promotor" datos={tablero.porPromotor} nombre="promotor" horizontal />
        )}
        {ventas && <Grafico titulo="Productos más vendidos" datos={tablero.porProducto} nombre="producto" horizontal />}
        {analisis?.mejoresClientes && (
          <Grafico titulo="Mejores clientes del mes" datos={analisis.mejoresClientes} nombre="cliente" horizontal />
        )}
        {analisis?.antiguedad && <AntiguedadDeSaldos tramos={analisis.antiguedad} />}
        {analisis?.visitas && <VisitasPorPromotor promotores={analisis.visitas} />}
      </div>
    </>
  );

  return (
    <Layout>
      <PageHeader title="Inicio" subtitle={subtitulo} />
      {contenido}
    </Layout>
  );
}
