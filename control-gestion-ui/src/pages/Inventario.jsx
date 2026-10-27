import { useCallback, useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { Boxes, History, LoaderCircle, PackagePlus, TriangleAlert } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, AreaDeTexto, Badge, BarraDeFiltros, Btn, Buscador, Cargando, Casilla, ErrorDeCarga, EstadoVacio, Input, Modal,
  PageHeader, Paginacion,
} from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { useDemorado } from '../hooks/useDemorado';
import { useFiltrosEnLaUrl } from '../hooks/useFiltrosEnLaUrl';
import { mostrarPrimerProblema } from '../utils/foco';
import { formatearCantidad, formatearFecha, formatearMonto } from '../utils/formato';

const { ventas } = PERMISOS;
const TAMANO_PAGINA = 25;
const TAMANO_KARDEX = 10;
const SIN_FILTROS = { texto: '', soloBajoMinimo: false };

// TipoMovimiento de la API. "Ajuste" solo aparece en movimientos viejos: hoy el stock sube con ingresos.
const MOVIMIENTOS = {
  0: { texto: 'Entrada', color: 'green' },
  1: { texto: 'Salida', color: 'blue' },
  2: { texto: 'Ajuste', color: 'yellow' },
  3: { texto: 'Devolución', color: 'purple' },
};

/** La variación con su signo: lo que el movimiento sumó o restó al stock. */
function Variacion({ valor }) {
  const suma = valor >= 0;
  return (
    <span className={`font-semibold tabular-nums whitespace-nowrap ${suma ? 'text-green-800' : 'text-red-700'}`}>
      {suma ? '+' : '−'}{formatearCantidad(Math.abs(valor))}
    </span>
  );
}

export default function Inventario() {
  const { tiene } = useAuth();
  const puedeIngresar = tiene(ventas.registrarIngreso);
  const puedeVerKardex = tiene(ventas.consultarKardex);

  const [pagina, setPagina] = useState(null);
  const { filtros, setFiltros, numeroPagina, setNumeroPagina } = useFiltrosEnLaUrl(SIN_FILTROS);
  const busqueda = useDemorado(filtros.texto.trim(), 300);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');

  const [ingreso, setIngreso] = useState(null);
  const ingresoRef = useRef(null);
  const [erroresIngreso, setErroresIngreso] = useState({});
  const [errorIngreso, setErrorIngreso] = useState('');
  const [guardando, setGuardando] = useState(false);

  const [kardex, setKardex] = useState(null);

  const cargar = useCallback(() => api.get('/inventario', {
    params: {
      pagina: numeroPagina,
      tamano: TAMANO_PAGINA,
      buscar: busqueda || undefined,
      soloBajoMinimo: filtros.soloBajoMinimo || undefined,
    },
  })
    .then(({ data }) => { setPagina(data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar el inventario.')))
    .finally(() => { setCargando(false); setReintentando(false); }),
  [numeroPagina, busqueda, filtros.soloBajoMinimo]);

  useEffect(() => { cargar(); }, [cargar]);

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };

  const abrirIngreso = (item) => {
    setErroresIngreso({});
    setErrorIngreso('');
    setIngreso({ item, cantidad: '', observacion: '' });
  };

  const registrarIngreso = async () => {
    setGuardando(true);
    setErroresIngreso({});
    setErrorIngreso('');
    try {
      const { data } = await api.post(`/inventario/${ingreso.item.productoId}/ingresos`, {
        cantidad: Number(ingreso.cantidad) || 0,
        observacion: ingreso.observacion,
      });
      avisar.exito(`Ingresaron ${formatearCantidad(ingreso.cantidad)} de ${data.descripcion}: el stock quedó en ${formatearCantidad(data.stockActual)}.`);
      setIngreso(null);
      cargar();
    } catch (err) {
      flushSync(() => {
        const campos = erroresPorCampo(err);
        setErroresIngreso(campos);
        if (Object.keys(campos).length === 0) setErrorIngreso(mensajeDeError(err, 'No se pudo registrar el ingreso.'));
      });
      mostrarPrimerProblema(ingresoRef.current);
    } finally {
      setGuardando(false);
    }
  };

  // Mientras llega la página siguiente se sigue viendo la anterior; al abrir otro producto, empieza vacío.
  const cargarKardex = (item, numero) => {
    setKardex((k) => ({ item, pagina: k?.item === item ? k.pagina : null, numero, cargando: true, error: '' }));
    api.get(`/inventario/${item.productoId}/movimientos`, { params: { pagina: numero, tamano: TAMANO_KARDEX } })
      .then(({ data }) => setKardex((k) => k && { ...k, pagina: data, cargando: false }))
      .catch((err) => setKardex((k) => k && { ...k, cargando: false, error: mensajeDeError(err, 'No se pudo cargar el kardex.') }));
  };

  const hayFiltros = filtros.texto !== '' || filtros.soloBajoMinimo;
  const items = pagina?.items ?? [];
  const hayAcciones = puedeIngresar || puedeVerKardex;

  let estado = null;
  if (cargando) estado = <Cargando texto="Cargando inventario…" />;
  else if (errorCarga && !pagina) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (pagina?.total === 0 && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay productos" icono={Boxes}>
      El administrador del sistema los registra en Parámetros, Productos. Cada uno empieza con stock cero.
    </EstadoVacio>
  );

  const encabezados = [
    'Producto', 'Categoría', { texto: 'Precio', derecha: true }, { texto: 'Stock', derecha: true }, { texto: 'Mínimo', derecha: true }, 'Último movimiento',
    ...(hayAcciones ? [''] : []),
  ];

  let estadoKardex = null;
  if (kardex?.cargando && !kardex.pagina) estadoKardex = <Cargando texto="Cargando movimientos…" />;
  else if (kardex?.error && !kardex.pagina) estadoKardex = (
    <ErrorDeCarga mensaje={kardex.error} onReintentar={() => cargarKardex(kardex.item, kardex.numero)} reintentando={kardex.cargando} />
  );
  else if (kardex?.pagina?.total === 0) estadoKardex = (
    <EstadoVacio titulo="Sin movimientos" icono={History}>Este producto todavía no tuvo ingresos ni ventas.</EstadoVacio>
  );

  return (
    <Layout>
      <PageHeader
        title="Inventario"
        subtitle="El stock de cada producto. Los ingresos de mercadería lo suben y las ventas confirmadas lo bajan."
      />

      {errorCarga && pagina && <Alert type="error" className="mb-4">{errorCarga}</Alert>}

      <Table
        headers={encabezados}
        empty={filtros.soloBajoMinimo && filtros.texto === '' ? 'Ningún producto está bajo el mínimo.' : 'Ningún producto coincide con la búsqueda.'}
        estado={estado}
        barra={!cargando && pagina && (pagina.total > 0 || hayFiltros) && (
          <BarraDeFiltros
            resultado={`${pagina.total} ${pagina.total === 1 ? 'producto' : 'productos'}`}
            hayFiltros={hayFiltros}
            onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}
          >
            <Buscador label="Buscar" placeholder="Código, descripción o categoría…" value={filtros.texto}
              onChange={(texto) => filtrar({ texto })} />
            <div className="sm:h-10 sm:flex sm:items-center">
              <Casilla checked={filtros.soloBajoMinimo} onCheckedChange={(v) => filtrar({ soloBajoMinimo: v === true })}>
                Solo bajo el mínimo
              </Casilla>
            </div>
          </BarraDeFiltros>
        )}
        pie={pagina && <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina} />}
      >
        {items.map((item) => (
          <Tr key={item.productoId}>
            <Td>
              <p className="font-medium">{item.descripcion}</p>
              <p className="text-xs text-slate-600 font-mono">{item.sku}</p>
            </Td>
            <Td>{item.categoria ?? <span className="text-slate-600">Sin categoría</span>}</Td>
            <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(item.precioVenta)}</Td>
            <Td className="text-right">
              <span className="font-semibold tabular-nums">{formatearCantidad(item.stockActual)}</span>
              {item.bajoMinimo && (
                <span className="mt-0.5 flex justify-end">
                  <Badge color="yellow"><TriangleAlert size={12} aria-hidden="true" />Bajo el mínimo</Badge>
                </span>
              )}
            </Td>
            <Td className="text-right tabular-nums">{formatearCantidad(item.stockMinimo)}</Td>
            <Td className="whitespace-nowrap tabular-nums">{formatearFecha(item.ultimaActualizacion, { hora: true })}</Td>
            {hayAcciones && (
              <Td>
                <div className="flex items-center justify-end gap-1">
                  {puedeIngresar && (
                    <Btn variant="ghost" onClick={() => abrirIngreso(item)} aria-label={`Registrar un ingreso de ${item.descripcion}`}>
                      <PackagePlus size={14} aria-hidden="true" />
                      Ingreso
                    </Btn>
                  )}
                  {puedeVerKardex && (
                    <Btn variant="ghost" onClick={() => cargarKardex(item, 1)} aria-label={`Ver el kardex de ${item.descripcion}`}>
                      <History size={14} aria-hidden="true" />
                      Kardex
                    </Btn>
                  )}
                </div>
              </Td>
            )}
          </Tr>
        ))}
      </Table>

      <Modal
        abierto={ingreso !== null}
        onCerrar={() => setIngreso(null)}
        titulo={`Ingreso de ${ingreso?.item.descripcion ?? ''}`}
        descripcion="Suma al stock y queda en el kardex como entrada."
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={() => setIngreso(null)} disabled={guardando}>Cancelar</Btn>
            <Btn type="submit" form="formulario-ingreso" size="md" disabled={guardando}>
              {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardando ? 'Registrando…' : 'Registrar ingreso'}
            </Btn>
          </>
        )}
      >
        {ingreso && (
          <form id="formulario-ingreso" ref={ingresoRef} onSubmit={(e) => { e.preventDefault(); registrarIngreso(); }} className="space-y-4" noValidate>
            {errorIngreso && <Alert type="error">{errorIngreso}</Alert>}
            <p className="text-sm text-slate-700">
              Stock actual: <span className="font-semibold tabular-nums">{formatearCantidad(ingreso.item.stockActual)}</span>
            </p>
            <Input label="Cantidad que ingresa" type="number" inputMode="decimal" min="0" step="1" value={ingreso.cantidad}
              error={erroresIngreso.cantidad} autoComplete="off"
              onChange={(e) => setIngreso({ ...ingreso, cantidad: e.target.value })} />
            <AreaDeTexto label="Observación" value={ingreso.observacion} error={erroresIngreso.observacion} autoComplete="off"
              placeholder="De dónde viene la mercadería: proveedor, factura…"
              onChange={(e) => setIngreso({ ...ingreso, observacion: e.target.value })} />
          </form>
        )}
      </Modal>

      <Modal
        abierto={kardex !== null}
        onCerrar={() => setKardex(null)}
        ancho="lg"
        titulo={`Kardex de ${kardex?.item.descripcion ?? ''}`}
        descripcion="Cada entrada y salida del stock, de la más reciente a la más antigua."
        pie={<Btn variant="outline" size="md" onClick={() => setKardex(null)}>Cerrar</Btn>}
      >
        {kardex && (
          <div className="space-y-3">
            {kardex.error && kardex.pagina && <Alert type="error">{kardex.error}</Alert>}
            <Table
              headers={['Fecha', 'Movimiento', { texto: 'Cantidad', derecha: true }, { texto: 'Stock', derecha: true }, 'Origen', 'Usuario']}
              estado={estadoKardex}
              pie={kardex.pagina && (
                <Paginacion pagina={kardex.pagina.numeroPagina} totalPaginas={kardex.pagina.totalPaginas}
                  onCambiar={(n) => cargarKardex(kardex.item, n)} />
              )}
            >
              {(kardex.pagina?.items ?? []).map((m) => {
                const tipo = MOVIMIENTOS[m.tipoMovimiento] ?? { texto: 'Otro', color: 'gray' };
                return (
                  <Tr key={m.id}>
                    <Td className="whitespace-nowrap tabular-nums">{formatearFecha(m.fecha, { hora: true })}</Td>
                    <Td><Badge color={tipo.color}>{tipo.texto}</Badge></Td>
                    <Td className="text-right"><Variacion valor={m.variacion} /></Td>
                    <Td className="text-right tabular-nums whitespace-nowrap">
                      {formatearCantidad(m.stockAnterior)} → {formatearCantidad(m.stockResultante)}
                    </Td>
                    <Td>
                      <p className="whitespace-nowrap">{m.hojaPedido ? m.hojaPedido.numero : tipo.texto}</p>
                      {m.observacion && <p className="text-xs text-slate-600 max-w-56 wrap-break-word">{m.observacion}</p>}
                    </Td>
                    <Td className="whitespace-nowrap">{m.usuario.nombreCompleto}</Td>
                  </Tr>
                );
              })}
            </Table>
          </div>
        )}
      </Modal>
    </Layout>
  );
}
