import { useCallback, useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { Link } from 'react-router-dom';
import { Layers, LoaderCircle, Map as Mapa, Pencil, Plus, Trash2, TriangleAlert } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, Badge, BarraDeFiltros, Btn, Buscador, Confirmar, ErrorDeCarga, EstadoVacio, Input, Modal, PageHeader,
  Selector,
} from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { useAuth } from '../context/auth';
import { PANTALLAS, PERMISOS } from '../context/permisos';
import { mostrarPrimerProblema } from '../utils/foco';
import { coincide } from '../utils/texto';

const { parametros } = PERMISOS;

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = { zona_codigo_repetido: 'codigo', zona_nombre_repetido: 'nombre' };

const clientes = (n) => (n === 1 ? '1 cliente' : `${n} clientes`);
const cuantasZonas = (n) => (n === 1 ? '1 zona' : `${n} zonas`);
// Lo que se elimina con la zona: " con su límite y sus 2 puntos de referencia", o nada.
function loQueSeVaConElla(zona) {
  const n = zona?.puntosDeReferencia ?? 0;
  const partes = [zona?.limite && 'su límite', n > 0 && (n === 1 ? 'su punto de referencia' : `sus ${n} puntos de referencia`)];
  const quedan = partes.filter(Boolean);
  return quedan.length > 0 ? ` con ${quedan.join(' y ')}` : '';
}

// Como el botón fantasma de una fila (Btn variant="ghost"), pero navega.
const enlaceDeFila = 'inline-flex h-8 shrink-0 items-center whitespace-nowrap justify-center gap-1.5 rounded-lg px-3 text-xs font-medium text-slate-700 transition-colors hover:bg-slate-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 focus-visible:ring-offset-2';

/**
 * Zonas de venta (7.2). Cada zona tiene un promotor, que es el de todos sus clientes: tiene que estar activo y
 * recorrer rutas. Una zona cuyo promotor dejó de cumplir (por ejemplo, se desactivó) se marca para reasignarla. El
 * código no se cambia, y una zona con clientes, ventas, pedidos o rutas no se elimina.
 */
export default function Zonas() {
  const { tiene } = useAuth();
  const puedeRegistrar = tiene(parametros.registrarZona);
  const puedeModificar = tiene(parametros.modificarZona);
  const puedeEliminar = tiene(parametros.eliminarZona);
  const veElMapa = tiene(...PANTALLAS.mapaZonas);

  const [zonas, setZonas] = useState([]);
  const [promotores, setPromotores] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [busqueda, setBusqueda] = useState('');

  const [editor, setEditor] = useState(null);
  const editorRef = useRef(null);
  const [erroresCampo, setErroresCampo] = useState({});
  const [errorEditor, setErrorEditor] = useState('');
  const [guardando, setGuardando] = useState(false);

  const [aEliminar, setAEliminar] = useState(null);
  const [errorEliminar, setErrorEliminar] = useState('');
  const [eliminando, setEliminando] = useState(false);

  const cargar = useCallback(() => Promise.all([
    api.get('/zonas'),
    puedeRegistrar || puedeModificar ? api.get('/usuarios/promotores') : Promise.resolve({ data: [] }),
  ])
    .then(([z, p]) => { setZonas(z.data); setPromotores(p.data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de zonas.')))
    .finally(() => { setCargando(false); setReintentando(false); }), [puedeRegistrar, puedeModificar]);

  useEffect(() => { cargar(); }, [cargar]);

  const abrirEditor = (zona) => {
    setErroresCampo({});
    setErrorEditor('');
    setEditor(zona
      ? { id: zona.id, nombre: zona.nombre, codigo: zona.codigo, promotorId: zona.promotorValido ? zona.promotor.id : '', anterior: zona }
      : { nombre: '', codigo: '', promotorId: '' });
  };

  const guardar = async (ev) => {
    ev.preventDefault();
    setGuardando(true);
    setErroresCampo({});
    setErrorEditor('');
    try {
      const { data } = editor.id
        ? await api.put(`/zonas/${editor.id}`, { nombre: editor.nombre, promotorId: editor.promotorId })
        : await api.post('/zonas', { nombre: editor.nombre, codigo: editor.codigo, promotorId: editor.promotorId });
      setZonas((lista) => (editor.id ? lista.map((z) => (z.id === data.id ? data : z)) : [...lista, data])
        .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es')));
      avisar.exito(editor.id ? `Se guardó la zona ${data.nombre}.` : `Se registró la zona ${data.nombre}.`);
      setEditor(null);
    } catch (err) {
      const campos = erroresPorCampo(err);
      const campoDelCodigo = CAMPO_DEL_ERROR[err.response?.data?.codigo];
      if (campoDelCodigo) campos[campoDelCodigo] = err.response.data.detail;
      flushSync(() => {
        setErroresCampo(campos);
        if (Object.keys(campos).length === 0) setErrorEditor(mensajeDeError(err, 'No se pudo guardar la zona.'));
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
      await api.delete(`/zonas/${aEliminar.id}`);
      setZonas((lista) => lista.filter((z) => z.id !== aEliminar.id));
      avisar.exito(`Se eliminó la zona ${aEliminar.nombre}.`);
      setAEliminar(null);
    } catch (err) {
      setErrorEliminar(mensajeDeError(err, 'No se pudo eliminar la zona.'));
    } finally {
      setEliminando(false);
    }
  };

  const visibles = zonas.filter((z) => coincide(busqueda, z.nombre, z.codigo, z.promotor?.nombreCompleto));
  const porReasignar = zonas.filter((z) => !z.promotorValido).length;

  let estado = null;
  if (errorCarga) estado = <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />;
  else if (zonas.length === 0) estado = (
    <EstadoVacio titulo="Todavía no hay zonas" icono={Mapa}
      accion={puedeRegistrar && <Btn size="md" onClick={() => abrirEditor(null)}><Plus size={16} aria-hidden="true" />Nueva zona</Btn>}>
      Cada zona agrupa a los clientes de un promotor.
    </EstadoVacio>
  );

  const anterior = editor?.anterior;

  return (
    <Layout>
      <PageHeader
        title="Zonas"
        subtitle="Cada zona tiene un promotor, que atiende a todos sus clientes."
        action={puedeRegistrar && zonas.length > 0 && (
          <Btn size="md" onClick={() => abrirEditor(null)}>
            <Plus size={16} aria-hidden="true" />
            Nueva zona
          </Btn>
        )}
      />

      {porReasignar > 0 && puedeModificar && (
        <Alert type="warning" className="mb-4">
          {porReasignar === 1 ? 'Una zona tiene' : `${porReasignar} zonas tienen`} un promotor inactivo o que ya no recorre rutas:
          edítala y elige otro.
        </Alert>
      )}

      <Table
        cargando={cargando && 'Cargando zonas…'}
        headers={['Zona', 'Promotor', { texto: 'Clientes', derecha: true }, 'Límite', { texto: 'Puntos', derecha: true },
          { texto: 'Acciones', oculto: true }]}
        empty="Ninguna zona coincide con la búsqueda."
        estado={estado}
        barra={(cargando || (!errorCarga && zonas.length > 0)) && (
          <BarraDeFiltros
            resultado={cargando ? '' : (busqueda ? `${visibles.length} de ${cuantasZonas(zonas.length)}` : cuantasZonas(zonas.length))}
            hayFiltros={busqueda !== ''}
            onLimpiar={() => setBusqueda('')}
          >
            <Buscador label="Buscar" placeholder="Nombre, código o promotor…" value={busqueda} onChange={setBusqueda} />
          </BarraDeFiltros>
        )}
      >
        {visibles.map((z) => (
          <Tr key={z.id}>
            <Td>
              <span className="block font-medium">{z.nombre}</span>
              <span translate="no" className="block whitespace-nowrap font-mono text-xs text-slate-600">{z.codigo}</span>
            </Td>
            <Td>
              <span className="block">{z.promotor?.nombreCompleto ?? 'Sin promotor'}</span>
              {!z.promotorValido && (
                <Badge color="yellow"><TriangleAlert size={12} aria-hidden="true" />Elige otro promotor</Badge>
              )}
            </Td>
            <Td className="text-right tabular-nums">{z.clientes}</Td>
            <Td className="whitespace-nowrap">{z.limite ? 'Dibujado' : <span className="text-slate-600">Sin límite</span>}</Td>
            <Td className="text-right tabular-nums">{z.puntosDeReferencia}</Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                {veElMapa && (
                  <Link to={`/zonas/mapa?zona=${z.id}`} className={enlaceDeFila} aria-label={`Ver la zona ${z.nombre} en el mapa`}>
                    <Layers size={14} aria-hidden="true" />
                    Ver en el mapa
                  </Link>
                )}
                {puedeModificar && (
                  <Btn variant="ghost" onClick={() => abrirEditor(z)} aria-label={`Editar la zona ${z.nombre}`}>
                    <Pencil size={14} aria-hidden="true" />
                    Editar
                  </Btn>
                )}
                {puedeEliminar && (
                  <Btn variant="ghost" onClick={() => { setErrorEliminar(''); setAEliminar(z); }} aria-label={`Eliminar la zona ${z.nombre}`}>
                    <Trash2 size={14} aria-hidden="true" />
                    Eliminar
                  </Btn>
                )}
              </div>
            </Td>
          </Tr>
        ))}
      </Table>

      <Modal
        abierto={editor !== null}
        onCerrar={() => setEditor(null)}
        titulo={editor?.id ? `Editar la zona ${anterior?.nombre}` : 'Nueva zona'}
        descripcion={editor?.id ? undefined : 'Sus clientes quedan en la cartera del promotor que elijas.'}
        pie={(
          <>
            <Btn variant="outline" size="md" onClick={() => setEditor(null)} disabled={guardando}>Cancelar</Btn>
            <Btn type="submit" form="editor-zona" size="md" disabled={guardando}>
              {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardando ? 'Guardando…' : 'Guardar'}
            </Btn>
          </>
        )}
      >
        {editor && (
          <form id="editor-zona" ref={editorRef} onSubmit={guardar} className="space-y-4" noValidate>
            {errorEditor && <Alert type="error">{errorEditor}</Alert>}
            <Input label="Nombre" value={editor.nombre} maxLength={100} autoComplete="off" placeholder="Zona Norte…"
              error={erroresCampo.nombre} onChange={(e) => setEditor({ ...editor, nombre: e.target.value })} />
            <Input label="Código" value={editor.codigo} maxLength={20} autoComplete="off" spellCheck={false} placeholder="ZN-01…"
              disabled={Boolean(editor.id)} error={erroresCampo.codigo}
              ayuda={editor.id ? 'El código no se cambia: lo usan los registros de la zona.' : undefined}
              onChange={(e) => setEditor({ ...editor, codigo: e.target.value })} />
            <Selector label="Promotor" placeholder="Elige el promotor" value={editor.promotorId} error={erroresCampo.promotorId}
              opciones={promotores.map((p) => ({ valor: p.id, texto: p.nombreCompleto }))}
              ayuda={anterior && !anterior.promotorValido
                ? `${anterior.promotor?.nombreCompleto ?? 'El promotor actual'} está inactivo o ya no recorre rutas: elige otro.`
                : 'Al cambiarlo, los clientes de la zona pasan al nuevo promotor.'}
              onChange={(promotorId) => setEditor({ ...editor, promotorId })} />
          </form>
        )}
      </Modal>

      <Confirmar
        abierto={aEliminar !== null}
        titulo={`¿Eliminar la zona ${aEliminar?.nombre ?? ''}?`}
        textoAccion="Eliminar"
        enviando={eliminando}
        error={errorEliminar}
        onConfirmar={eliminar}
        onCancelar={() => setAEliminar(null)}
      >
        <p>
          La zona se borra{loQueSeVaConElla(aEliminar)}.
          Si tiene clientes, ventas, pedidos o rutas, no se puede eliminar y el aviso dice qué la usa.
          {aEliminar?.clientes > 0 && ` Tiene ${clientes(aEliminar.clientes)}.`}
        </p>
      </Confirmar>
    </Layout>
  );
}
