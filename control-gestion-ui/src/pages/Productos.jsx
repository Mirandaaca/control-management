import { useCallback, useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { Link } from 'react-router-dom';
import { LoaderCircle, Package, PackageX, Pencil, Plus } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, BarraDeFiltros, BotonCancelar, Btn, Buscador, Confirmar, ErrorDeCarga, EstadoVacio, Input, Modal, PageHeader,
  Paginacion, Selector,
} from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { useAuth } from '../context/auth';
import { PANTALLAS, PERMISOS } from '../context/permisos';
import { useDemorado } from '../hooks/useDemorado';
import { useFiltrosEnLaUrl } from '../hooks/useFiltrosEnLaUrl';
import { useRecarga } from '../hooks/useRecarga';
import { usePeticionVigente } from '../hooks/usePeticionVigente';
import { useSinGuardar } from '../hooks/useSinGuardar';
import { mostrarPrimerProblema } from '../utils/foco';
import { formatearCantidad, formatearMonto } from '../utils/formato';

const { parametros } = PERMISOS;
const SIN_FILTROS = { texto: '', categoria: '' };
const FORMULARIO_VACIO = { sku: '', descripcion: '', categoriaId: '', precioCompra: '', precio: '', stockMinimo: '0' };

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = { sku_repetido: 'sku', categoria_no_existe: 'categoriaId' };

export default function Productos() {
  const { tiene } = useAuth();
  const puedeRegistrar = tiene(parametros.registrarProducto);
  const puedeModificar = tiene(parametros.modificarProducto);
  const puedeEliminar = tiene(parametros.eliminarProducto);

  const [pagina, setPagina] = useState(null);
  const { filtros, setFiltros, numeroPagina, setNumeroPagina, tamano, setTamano } = useFiltrosEnLaUrl(SIN_FILTROS);
  const busqueda = useDemorado(filtros.texto.trim(), 300);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');

  // Las categorías (tarea 29), para el filtro y el formulario: null mientras llegan.
  const [categorias, setCategorias] = useState(null);
  const [errorCategorias, setErrorCategorias] = useState('');

  const [formulario, setFormulario] = useState(null);
  const sinGuardar = useSinGuardar(formulario);
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
      params: {
        pagina: numeroPagina, tamano, buscar: busqueda || undefined, categoriaId: filtros.categoria || undefined,
      },
    })
      .then(siVigente(({ data }) => { setPagina(data); setErrorCarga(''); }))
      .catch(siVigente((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de productos.'))))
      .finally(siVigente(() => { setCargando(false); setReintentando(false); }));
  }, [numeroPagina, tamano, busqueda, filtros.categoria, nuevaPeticion]);

  const { recargando, terminar } = useRecarga(cargar);
  useEffect(() => { cargar().finally(() => terminar(cargar)); }, [cargar, terminar]);

  const cargarCategorias = useCallback(() => api.get('/categorias/opciones')
    .then(({ data }) => { setCategorias(data); setErrorCategorias(''); })
    .catch((err) => setErrorCategorias(mensajeDeError(err, 'No se pudieron cargar las categorías.'))), []);
  useEffect(() => { cargarCategorias(); }, [cargarCategorias]);

  const filtrar = (cambios) => { setFiltros((f) => ({ ...f, ...cambios })); setNumeroPagina(1); };

  const abrirFormulario = (producto) => {
    setErroresCampo({});
    setErrorFormulario('');
    // Se vuelven a pedir: así aparece una categoría que se acaba de crear en Categorías.
    cargarCategorias();
    setFormulario(producto
      ? {
        id: producto.id,
        sku: producto.sku,
        descripcion: producto.descripcion,
        categoriaId: String(producto.categoria.id),
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
    const { id, categoriaId, precioCompra, precio, stockMinimo, ...resto } = formulario;
    const datos = {
      ...resto,
      categoriaId: Number(categoriaId) || 0,
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
      // La categoría elegida se eliminó mientras el formulario estaba abierto: la lista vuelve sin ella.
      if (codigo === 'categoria_no_existe') cargarCategorias();
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

  const hayFiltros = filtros.texto !== '' || filtros.categoria !== '';
  const productos = pagina?.items ?? [];
  const opcionesDeCategoria = (categorias ?? []).map((c) => ({ valor: String(c.id), texto: c.nombre }));

  // Sin categorías no se puede registrar un producto: la ayuda dice dónde se crean.
  let ayudaDeCategoria;
  if (categorias === null && !errorCategorias) ayudaDeCategoria = 'Cargando las categorías…';
  else if (categorias?.length === 0) ayudaDeCategoria = tiene(...PANTALLAS.categorias)
    ? <>Primero registra una categoría en <Link to="/categorias" className="font-medium text-marca-700 underline">Categorías</Link>.</>
    : 'Todavía no hay categorías: pídele al administrador que registre una.';

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
            onLimpiar={() => { setFiltros(SIN_FILTROS); setNumeroPagina(1); }}
          >
            <Buscador label="Buscar" placeholder="Código, descripción o categoría…" value={filtros.texto}
              onChange={(texto) => filtrar({ texto })} />
            <div className="w-full sm:w-48">
              <Selector label="Categoría" todos="Todas" value={filtros.categoria} opciones={opcionesDeCategoria}
                onChange={(categoria) => filtrar({ categoria })} />
            </div>
          </BarraDeFiltros>
        )}
        pie={pagina && <Paginacion pagina={pagina.numeroPagina} totalPaginas={pagina.totalPaginas} onCambiar={setNumeroPagina}
            total={pagina.total} tamano={pagina.tamano} onCambiarTamano={setTamano} deshabilitada={recargando} />}
      >
        {productos.map((p) => (
          <Tr key={p.id}>
            <Td>
              <p className="font-medium">{p.descripcion}</p>
              <p className="text-xs text-slate-600 font-mono">{p.sku}</p>
            </Td>
            <Td>{p.categoria.nombre}</Td>
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
        sinGuardar={sinGuardar}
        titulo={formulario?.id ? 'Editar producto' : 'Nuevo producto'}
        descripcion={formulario?.id ? undefined : 'El stock empieza en cero: sube con los ingresos de mercadería, en Inventario.'}
        pie={(
          <>
            <BotonCancelar disabled={guardando} />
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
              <Selector label="Categoría" value={formulario.categoriaId} opciones={opcionesDeCategoria}
                error={erroresCampo.categoriaId ?? (errorCategorias || undefined)} ayuda={ayudaDeCategoria}
                disabled={categorias === null && !errorCategorias}
                onChange={(categoriaId) => setFormulario({ ...formulario, categoriaId })} />
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
