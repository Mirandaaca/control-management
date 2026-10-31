import { useCallback, useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { LoaderCircle, Package, PackageX, Pencil, Plus } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, BarraDeFiltros, Btn, Buscador, Confirmar, ErrorDeCarga, EstadoVacio, Input, Modal, PageHeader, Paginacion,
} from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { useDemorado } from '../hooks/useDemorado';
import { useRecarga } from '../hooks/useRecarga';
import { usePeticionVigente } from '../hooks/usePeticionVigente';
import { mostrarPrimerProblema } from '../utils/foco';
import { formatearCantidad, formatearMonto } from '../utils/formato';

const { parametros } = PERMISOS;
const TAMANO_PAGINA = 25;
const FORMULARIO_VACIO = { sku: '', descripcion: '', categoria: '', precioCompra: '', precio: '', stockMinimo: '0' };

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = { sku_repetido: 'sku' };

export default function Productos() {
  const { tiene } = useAuth();
  const puedeRegistrar = tiene(parametros.registrarProducto);
  const puedeModificar = tiene(parametros.modificarProducto);
  const puedeEliminar = tiene(parametros.eliminarProducto);

  const [pagina, setPagina] = useState(null);
  const [numeroPagina, setNumeroPagina] = useState(1);
  const [texto, setTexto] = useState('');
  const busqueda = useDemorado(texto.trim(), 300);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');

  const [formulario, setFormulario] = useState(null);
  const formularioRef = useRef(null);
  const [erroresCampo, setErroresCampo] = useState({});
  const [errorFormulario, setErrorFormulario] = useState('');
  const [guardando, setGuardando] = useState(false);

  const [desactivando, setDesactivando] = useState(null);
  const [errorDesactivar, setErrorDesactivar] = useState('');
  const [enviandoDesactivar, setEnviandoDesactivar] = useState(false);

  const nuevaPeticion = usePeticionVigente();
  const cargar = useCallback(() => {
    const siVigente = nuevaPeticion();
    return api.get('/productos', {
      params: { pagina: numeroPagina, tamano: TAMANO_PAGINA, buscar: busqueda || undefined },
    })
      .then(siVigente(({ data }) => { setPagina(data); setErrorCarga(''); }))
      .catch(siVigente((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de productos.'))))
      .finally(siVigente(() => { setCargando(false); setReintentando(false); }));
  }, [numeroPagina, busqueda, nuevaPeticion]);

  const { recargando, terminar } = useRecarga(cargar);
  useEffect(() => { cargar().finally(() => terminar(cargar)); }, [cargar, terminar]);

  const buscar = (valor) => { setTexto(valor); setNumeroPagina(1); };

  const abrirFormulario = (producto) => {
    setErroresCampo({});
    setErrorFormulario('');
    setFormulario(producto
      ? {
        id: producto.id,
        sku: producto.sku,
        descripcion: producto.descripcion,
        categoria: producto.categoria ?? '',
        precioCompra: String(producto.precioCompra),
        precio: String(producto.precio),
        stockMinimo: String(producto.stockMinimo),
      }
      : FORMULARIO_VACIO);
  };

  const cambiar = (campo) => (e) => setFormulario({ ...formulario, [campo]: e.target.value });

  const guardar = async () => {
    setGuardando(true);
    setErroresCampo({});
    setErrorFormulario('');
    const { id, categoria, precioCompra, precio, stockMinimo, ...resto } = formulario;
    const datos = {
      ...resto,
      categoria: categoria.trim() || null,
      precioCompra: Number(precioCompra) || 0,
      precio: Number(precio) || 0,
      stockMinimo: Number(stockMinimo) || 0,
    };
    try {
      if (id) await api.put(`/productos/${id}`, datos);
      else await api.post('/productos', datos);
      avisar.exito(id ? `Se guardaron los cambios de ${datos.descripcion}.` : `Se registró ${datos.descripcion}, con stock 0.`);
      setFormulario(null);
      cargar();
    } catch (err) {
      const { codigo, detail } = err.response?.data ?? {};
      flushSync(() => {
        const campos = erroresPorCampo(err);
        if (CAMPO_DEL_ERROR[codigo]) campos[CAMPO_DEL_ERROR[codigo]] = detail;
        setErroresCampo(campos);
        if (Object.keys(campos).length === 0) setErrorFormulario(mensajeDeError(err, 'No se pudo guardar el producto.'));
      });
      mostrarPrimerProblema(formularioRef.current);
    } finally {
      setGuardando(false);
    }
  };

  const desactivar = async () => {
    setEnviandoDesactivar(true);
    setErrorDesactivar('');
    try {
      await api.delete(`/productos/${desactivando.id}`);
      avisar.exito(`${desactivando.descripcion} quedó inactivo.`);
      setDesactivando(null);
      cargar();
    } catch (err) {
      setErrorDesactivar(mensajeDeError(err, 'No se pudo desactivar el producto.'));
    } finally {
      setEnviandoDesactivar(false);
    }
  };

  const hayFiltros = texto !== '';
  const productos = pagina?.items ?? [];

  let estado = null;
  if (errorCarga && !pagina) estado = (
    <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
  );
  else if (pagina?.total === 0 && !hayFiltros) estado = (
    <EstadoVacio titulo="Todavía no hay productos" icono={Package}
      accion={puedeRegistrar && <Btn size="md" onClick={() => abrirFormulario(null)}><Plus size={16} aria-hidden="true" />Nuevo producto</Btn>}>
      Registra el primero con sus precios. Su stock empieza en cero y sube con los ingresos de mercadería.
    </EstadoVacio>
  );

  const hayAcciones = puedeModificar || puedeEliminar;
  const encabezados = [
    'Producto', 'Categoría', { texto: 'Precio de compra', derecha: true }, { texto: 'Precio de venta', derecha: true },
    { texto: 'Stock', derecha: true }, { texto: 'Stock mínimo', derecha: true }, ...(hayAcciones ? [''] : []),
  ];

  return (
    <Layout>
      <PageHeader
        title="Productos"
        subtitle="El catálogo que se vende en las hojas de pedido y los pedidos, con sus precios y su stock mínimo."
        action={puedeRegistrar && (
          <Btn size="md" onClick={() => abrirFormulario(null)}>
            <Plus size={16} aria-hidden="true" />
            Nuevo producto
          </Btn>
        )}
      />

      {errorCarga && pagina && <Alert type="error" className="mb-4">{errorCarga}</Alert>}

      <Table
        cargando={cargando && 'Cargando productos…'} recargando={recargando}
        headers={encabezados}
        empty="Ningún producto coincide con la búsqueda."
        estado={estado}
        barra={(cargando || (pagina && (pagina.total > 0 || hayFiltros))) && (
          <BarraDeFiltros
            resultado={pagina ? `${pagina.total} ${pagina.total === 1 ? 'producto' : 'productos'}` : ''}
            hayFiltros={hayFiltros}
            onLimpiar={() => buscar('')}
          >
            <Buscador label="Buscar" placeholder="Código, descripción o categoría…" value={texto} onChange={buscar} />
          </BarraDeFiltros>
        )}
        pie={pagina && <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina}
            deshabilitada={recargando} />}
      >
        {productos.map((p) => (
          <Tr key={p.id}>
            <Td>
              <p className="font-medium">{p.descripcion}</p>
              <p className="text-xs text-slate-600 font-mono">{p.sku}</p>
            </Td>
            <Td>{p.categoria ?? <span className="text-slate-600">Sin categoría</span>}</Td>
            <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(p.precioCompra)}</Td>
            <Td className="text-right tabular-nums whitespace-nowrap">{formatearMonto(p.precio)}</Td>
            <Td className="text-right tabular-nums">{formatearCantidad(p.stockActual)}</Td>
            <Td className="text-right tabular-nums">{formatearCantidad(p.stockMinimo)}</Td>
            {hayAcciones && (
              <Td>
                <div className="flex items-center justify-end gap-1">
                  {puedeModificar && (
                    <Btn variant="ghost" onClick={() => abrirFormulario(p)} aria-label={`Editar ${p.descripcion}`}>
                      <Pencil size={14} aria-hidden="true" />
                      Editar
                    </Btn>
                  )}
                  {puedeEliminar && (
                    <Btn variant="ghost" onClick={() => { setErrorDesactivar(''); setDesactivando(p); }} aria-label={`Desactivar ${p.descripcion}`}>
                      <PackageX size={14} aria-hidden="true" />
                      Desactivar
                    </Btn>
                  )}
                </div>
              </Td>
            )}
          </Tr>
        ))}
      </Table>

      <Modal
        abierto={formulario !== null}
        onCerrar={() => setFormulario(null)}
        titulo={formulario?.id ? 'Editar producto' : 'Nuevo producto'}
        descripcion={formulario?.id ? undefined : 'El stock empieza en cero: sube con los ingresos de mercadería, en Inventario.'}
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={() => setFormulario(null)} disabled={guardando}>Cancelar</Btn>
            <Btn type="submit" form="formulario-producto" size="md" disabled={guardando}>
              {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardando ? 'Guardando…' : 'Guardar'}
            </Btn>
          </>
        )}
      >
        {formulario && (
          <form id="formulario-producto" ref={formularioRef} onSubmit={(e) => { e.preventDefault(); guardar(); }} className="space-y-4" noValidate>
            {errorFormulario && <Alert type="error">{errorFormulario}</Alert>}
            <div className="grid gap-4 sm:grid-cols-2">
              <Input label="Código (SKU)" value={formulario.sku} error={erroresCampo.sku} autoComplete="off" spellCheck={false}
                placeholder="ARZ-1KG…" onChange={cambiar('sku')} />
              <Input label="Categoría (opcional)" value={formulario.categoria} error={erroresCampo.categoria} autoComplete="off"
                placeholder="Abarrotes, limpieza…" onChange={cambiar('categoria')} />
            </div>
            <Input label="Descripción" value={formulario.descripcion} error={erroresCampo.descripcion} autoComplete="off"
              placeholder="Arroz 1 kg…" onChange={cambiar('descripcion')} />
            <div className="grid gap-4 sm:grid-cols-2">
              <Input label="Precio de compra (Bs)" type="number" inputMode="decimal" step="0.01" min="0" value={formulario.precioCompra}
                error={erroresCampo.precioCompra} autoComplete="off" onChange={cambiar('precioCompra')} />
              <Input label="Precio de venta (Bs)" type="number" inputMode="decimal" step="0.01" min="0" value={formulario.precio}
                error={erroresCampo.precio} autoComplete="off" onChange={cambiar('precio')} />
              <Input label="Stock mínimo" type="number" inputMode="decimal" step="1" min="0" value={formulario.stockMinimo}
                error={erroresCampo.stockMinimo} autoComplete="off" ayuda="Con este stock o menos, sale bajo el mínimo."
                onChange={cambiar('stockMinimo')} />
            </div>
          </form>
        )}
      </Modal>

      <Confirmar
        abierto={desactivando !== null}
        titulo={`¿Desactivar “${desactivando?.descripcion ?? ''}”?`}
        textoAccion="Desactivar"
        enviando={enviandoDesactivar}
        error={errorDesactivar}
        onConfirmar={desactivar}
        onCancelar={() => setDesactivando(null)}
      >
        <p>
          Deja de aparecer en el catálogo, en el inventario y al registrar ventas y pedidos. Las hojas de pedido y el kardex
          que ya lo usan no cambian.
        </p>
      </Confirmar>
    </Layout>
  );
}
