import { useCallback, useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { LoaderCircle, Pencil, Plus, Shapes, Trash2 } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, BarraDeFiltros, BotonCancelar, Btn, Buscador, Confirmar, ErrorDeCarga, EstadoVacio, Input, Modal, PageHeader,
  Paginacion,
} from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { usePaginaLocal } from '../hooks/usePaginaLocal';
import { useSinGuardar } from '../hooks/useSinGuardar';
import { mostrarPrimerProblema } from '../utils/foco';
import { coincide } from '../utils/texto';

const { parametros } = PERMISOS;

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = { categoria_repetida: 'nombre' };

const enPlural = (n, singular, plural) => (n === 1 ? `1 ${singular}` : `${n} ${plural}`);

/** Por qué una categoría con productos no se elimina, como lo explica la API. */
function porQueNoSeElimina(categoria) {
  return categoria.productos > 0
    ? 'Tiene productos: pásalos a otra categoría antes de eliminarla.'
    : 'Sus productos inactivos conservan su categoría: no se puede eliminar.';
}

/**
 * Las categorías de los productos (7.3.29, tarea 29): agrupan el catálogo, el inventario y el reporte de inventario. Se
 * administran con los permisos de productos. Una categoría con productos, aunque estén inactivos, no se elimina.
 */
export default function Categorias() {
  const { tiene } = useAuth();
  const puedeRegistrar = tiene(parametros.registrarProducto);
  const puedeModificar = tiene(parametros.modificarProducto);
  const puedeEliminar = tiene(parametros.eliminarProducto);

  const [categorias, setCategorias] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [busqueda, setBusqueda] = useState('');

  const [editor, setEditor] = useState(null);
  const sinGuardar = useSinGuardar(editor);
  const editorRef = useRef(null);
  const [erroresCampo, setErroresCampo] = useState({});
  const [errorEditor, setErrorEditor] = useState('');
  const [guardando, setGuardando] = useState(false);

  const [aEliminar, setAEliminar] = useState(null);
  const [errorEliminar, setErrorEliminar] = useState('');
  const [eliminando, setEliminando] = useState(false);

  const cargar = useCallback(() => api.get('/categorias')
    .then(({ data }) => { setCategorias(data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de categorías.')))
    .finally(() => { setCargando(false); setReintentando(false); }), []);

  useEffect(() => { cargar(); }, [cargar]);

  // "original" es el nombre al abrir: el título de la ventana no cambia mientras se escribe.
  const abrirEditor = (categoria) => {
    setErroresCampo({});
    setErrorEditor('');
    setEditor(categoria ? { id: categoria.id, nombre: categoria.nombre, original: categoria.nombre } : { nombre: '' });
  };

  const guardar = async (ev) => {
    ev.preventDefault();
    setGuardando(true);
    setErroresCampo({});
    setErrorEditor('');
    const datos = { nombre: editor.nombre };
    try {
      const { data } = editor.id ? await api.put(`/categorias/${editor.id}`, datos) : await api.post('/categorias', datos);
      setCategorias((lista) => (editor.id ? lista.map((c) => (c.id === data.id ? data : c)) : [...lista, data])
        .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es')));
      avisar.exito(editor.id ? `Se guardó la categoría ${data.nombre}.` : `Se registró la categoría ${data.nombre}.`);
      setEditor(null);
    } catch (err) {
      const campos = erroresPorCampo(err);
      const campoDelCodigo = CAMPO_DEL_ERROR[err.response?.data?.codigo];
      if (campoDelCodigo) campos[campoDelCodigo] = err.response.data.detail;
      flushSync(() => {
        setErroresCampo(campos);
        if (Object.keys(campos).length === 0) setErrorEditor(mensajeDeError(err, 'No se pudo guardar la categoría.'));
      });
      mostrarPrimerProblema(editorRef.current);
    } finally {
      setGuardando(false);
    }
  };

  const eliminar = async () => {
    setEliminando(true);
    setErrorEliminar('');
    try {
      await api.delete(`/categorias/${aEliminar.id}`);
      setCategorias((lista) => lista.filter((c) => c.id !== aEliminar.id));
      avisar.exito(`Se eliminó la categoría ${aEliminar.nombre}.`);
      setAEliminar(null);
    } catch (err) {
      setErrorEliminar(mensajeDeError(err, 'No se pudo eliminar la categoría.'));
    } finally {
      setEliminando(false);
    }
  };

  const filtrados = categorias.filter((c) => coincide(busqueda, c.nombre));
  const { visibles, paginacion } = usePaginaLocal(filtrados, busqueda);
  const total = categorias.length;
  const enUso = (c) => c.productos + c.inactivos > 0;
  const botonNueva = puedeRegistrar && (
    <Btn size="md" onClick={() => abrirEditor(null)}>
      <Plus size={16} aria-hidden="true" />
      Nueva categoría
    </Btn>
  );

  let estado = null;
  if (errorCarga) estado = <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />;
  else if (total === 0) estado = (
    <EstadoVacio titulo="Todavía no hay categorías" icono={Shapes} accion={botonNueva}>
      Registra la primera: cada producto pertenece a una categoría.
    </EstadoVacio>
  );

  return (
    <Layout>
      <PageHeader
        title="Categorías"
        subtitle="Agrupan los productos en el catálogo, en el inventario y en el reporte de inventario."
        action={botonNueva}
      />

      <Table
        cargando={cargando && 'Cargando categorías…'}
        headers={['Categoría', { texto: 'Productos', derecha: true }, { texto: 'Acciones', oculto: true }]}
        empty="Ninguna categoría coincide con la búsqueda."
        estado={estado}
        pie={<Paginacion {...paginacion} />}
        barra={(cargando || (!errorCarga && total > 0)) && (
          <BarraDeFiltros
            resultado={cargando ? '' : (busqueda ? `${filtrados.length} de ${enPlural(total, 'categoría', 'categorías')}` : enPlural(total, 'categoría', 'categorías'))}
            hayFiltros={busqueda !== ''}
            onLimpiar={() => setBusqueda('')}
          >
            <Buscador label="Buscar" placeholder="Nombre de la categoría…" value={busqueda} onChange={setBusqueda} />
          </BarraDeFiltros>
        )}
      >
        {visibles.map((c) => (
          <Tr key={c.id}>
            <Td><span className="font-medium">{c.nombre}</span></Td>
            <Td className="text-right tabular-nums">
              {c.productos}
              {c.inactivos > 0 && (
                <span className="block text-xs text-slate-600">y {enPlural(c.inactivos, 'inactivo', 'inactivos')}</span>
              )}
            </Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                {puedeModificar && (
                  <Btn variant="ghost" onClick={() => abrirEditor(c)} aria-label={`Editar la categoría ${c.nombre}`}>
                    <Pencil size={14} aria-hidden="true" />
                    Editar
                  </Btn>
                )}
                {puedeEliminar && (
                  <Btn
                    variant="ghost"
                    onClick={() => { setErrorEliminar(''); setAEliminar(c); }}
                    disabled={enUso(c)}
                    title={enUso(c) ? porQueNoSeElimina(c) : undefined}
                    aria-label={`Eliminar la categoría ${c.nombre}`}
                  >
                    <Trash2 size={14} aria-hidden="true" />
                    Eliminar
                  </Btn>
                )}
              </div>
            </Td>
          </Tr>
        ))}
      </Table>
      {puedeEliminar && categorias.some(enUso) && (
        <p className="mt-3 text-sm text-slate-600">
          Una categoría con productos no se puede eliminar: primero pásalos a otra. Los productos inactivos conservan la suya.
        </p>
      )}

      <Modal
        abierto={editor !== null}
        onCerrar={() => setEditor(null)}
        sinGuardar={sinGuardar}
        titulo={editor?.id ? `Editar la categoría ${editor.original}` : 'Nueva categoría'}
        descripcion={editor?.id ? undefined : 'Después se elige en cada producto.'}
        pie={(
          <>
            <BotonCancelar disabled={guardando} />
            <Btn type="submit" form="editor-categoria" size="md" disabled={guardando}>
              {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardando ? 'Guardando…' : 'Guardar'}
            </Btn>
          </>
        )}
      >
        {editor && (
          <form id="editor-categoria" ref={editorRef} onSubmit={guardar} className="space-y-4" noValidate>
            {errorEditor && <Alert type="error">{errorEditor}</Alert>}
            <Input
              label="Nombre"
              value={editor.nombre}
              maxLength={80}
              error={erroresCampo.nombre}
              placeholder="Bebidas, higiene…"
              onChange={(e) => setEditor({ ...editor, nombre: e.target.value })}
              autoComplete="off"
            />
          </form>
        )}
      </Modal>

      <Confirmar
        abierto={aEliminar !== null}
        titulo={`¿Eliminar la categoría ${aEliminar?.nombre ?? ''}?`}
        textoAccion="Eliminar"
        enviando={eliminando}
        error={errorEliminar}
        onConfirmar={eliminar}
        onCancelar={() => setAEliminar(null)}
      >
        <p>La categoría deja de existir y ya no se puede elegir en los productos. Esto no se puede deshacer.</p>
      </Confirmar>
    </Layout>
  );
}
