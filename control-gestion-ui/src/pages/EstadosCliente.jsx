import { useCallback, useEffect, useRef, useState } from 'react';
import { flushSync } from 'react-dom';
import { Check, CircleAlert, LoaderCircle, Lock, Pencil, Plus, Tags, Trash2 } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import EstadoCliente from '../components/EstadoCliente';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import { PALETA } from '../components/paletaDeEstados';
import {
  Alert, AreaDeTexto, Badge, BarraDeFiltros, BotonCancelar, Btn, Buscador, Confirmar, ErrorDeCarga, EstadoVacio, Input,
  Modal, PageHeader,
} from '../components/ui';
import { avisar } from '../components/ui/avisar';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { mostrarPrimerProblema } from '../utils/foco';
import { coincide } from '../utils/texto';
import { useSinGuardar } from '../hooks/useSinGuardar';

const { parametros } = PERMISOS;
const INACTIVO = 4;

// Errores de la API que pertenecen a un campo del formulario: se muestran junto a él.
const CAMPO_DEL_ERROR = { estado_repetido: 'nombre' };

/** Por qué un estado protegido no cambia de nombre ni se elimina, como lo explica la API. */
function porQueEstaProtegido(estado) {
  return estado.esDelSistema
    ? 'El sistema lo asigna según las visitas (rojo o verde en el mapa).'
    : 'El sistema lo usa para bloquear las ventas y los pedidos del cliente.';
}

/** Las muestras de la paleta: un grupo de opciones que se recorre con las flechas. */
function ElegirColor({ valor, onCambiar, error }) {
  return (
    // El error es del grupo: aria-invalid no vale en cada opción (role radio), sí en el grupo.
    <fieldset role="radiogroup" aria-invalid={error ? true : undefined} aria-describedby={error ? 'color-error' : undefined}>
      <legend className="block text-sm font-medium text-slate-700 mb-1">Color</legend>
      <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
        {PALETA.map((c) => (
          <label key={c.color}
            className="flex min-w-0 cursor-pointer items-center gap-2 rounded-lg border border-borde-control bg-white px-3 py-2 text-sm text-slate-800 transition-colors hover:bg-slate-50 has-[:checked]:border-marca-600 has-[:checked]:bg-marca-50 has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-marca-500">
            <input type="radio" name="color" value={c.color} checked={valor === c.color} onChange={() => onCambiar(c.color)}
              className="sr-only" />
            <span className="grid h-5 w-5 shrink-0 place-items-center rounded-full border border-white shadow-[0_0_0_1px_#7B8798]"
              style={{ backgroundColor: c.color, color: c.texto }} aria-hidden="true">
              {valor === c.color && <Check size={12} strokeWidth={3} />}
            </span>
            <span className="truncate">{c.nombre}</span>
          </label>
        ))}
      </div>
      {error && (
        <p id="color-error" className="mt-1 flex items-start gap-1 text-sm text-red-700">
          <CircleAlert size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
          {error}
        </p>
      )}
    </fieldset>
  );
}

/**
 * Estados del cliente (7.4). Activo y Disponible para re-visita los asigna el sistema: solo cambia su descripción.
 * Inactivo cambia su color y su descripción, pero no su nombre. Los demás son de quien administra: se asignan a mano y
 * sacan al cliente del ciclo de visitas.
 */
export default function EstadosCliente() {
  const { tiene } = useAuth();
  const puedeRegistrar = tiene(parametros.registrarEstadoCliente);
  const puedeModificar = tiene(parametros.modificarEstadoCliente);
  const puedeEliminar = tiene(parametros.eliminarEstadoCliente);

  const [estados, setEstados] = useState([]);
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

  const cargar = useCallback(() => api.get('/estados-cliente')
    .then(({ data }) => { setEstados(data); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de estados.')))
    .finally(() => { setCargando(false); setReintentando(false); }), []);

  useEffect(() => { cargar(); }, [cargar]);

  const abrirEditor = (estado) => {
    setErroresCampo({});
    setErrorEditor('');
    setEditor(estado
      ? { ...estado, descripcion: estado.descripcion ?? '' }
      : { nombre: '', color: PALETA[0].color, descripcion: '', esDelSistema: false, protegido: false });
  };

  const guardar = async (ev) => {
    ev.preventDefault();
    setGuardando(true);
    setErroresCampo({});
    setErrorEditor('');
    const datos = { nombre: editor.nombre, color: editor.color, descripcion: editor.descripcion };
    try {
      const { data } = editor.id ? await api.put(`/estados-cliente/${editor.id}`, datos) : await api.post('/estados-cliente', datos);
      setEstados((lista) => (editor.id ? lista.map((e) => (e.id === data.id ? data : e)) : [...lista, data]));
      avisar.exito(editor.id ? `Se guardó el estado ${data.nombre}.` : `Se registró el estado ${data.nombre}.`);
      setEditor(null);
    } catch (err) {
      const campos = erroresPorCampo(err);
      const campoDelCodigo = CAMPO_DEL_ERROR[err.response?.data?.codigo];
      if (campoDelCodigo) campos[campoDelCodigo] = err.response.data.detail;
      flushSync(() => {
        setErroresCampo(campos);
        if (Object.keys(campos).length === 0) setErrorEditor(mensajeDeError(err, 'No se pudo guardar el estado.'));
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
      await api.delete(`/estados-cliente/${aEliminar.id}`);
      setEstados((lista) => lista.filter((e) => e.id !== aEliminar.id));
      avisar.exito(`Se eliminó el estado ${aEliminar.nombre}.`);
      setAEliminar(null);
    } catch (err) {
      setErrorEliminar(mensajeDeError(err, 'No se pudo eliminar el estado.'));
    } finally {
      setEliminando(false);
    }
  };

  const visibles = estados.filter((e) => coincide(busqueda, e.nombre, e.descripcion));

  let estado = null;
  if (errorCarga) estado = <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />;
  else if (estados.length === 0) estado = <EstadoVacio titulo="Todavía no hay estados" icono={Tags} />;

  return (
    <Layout>
      <PageHeader
        title="Estados del cliente"
        subtitle="Activo y Disponible los asigna el sistema según las visitas. Los demás se asignan a mano y sacan al cliente del ciclo de visitas."
        action={puedeRegistrar && (
          <Btn size="md" onClick={() => abrirEditor(null)}>
            <Plus size={16} aria-hidden="true" />
            Nuevo estado
          </Btn>
        )}
      />

      <Table
        cargando={cargando && 'Cargando estados…'}
        headers={['Estado', 'Descripción', 'Tipo', { texto: 'Clientes', derecha: true }, { texto: 'Acciones', oculto: true }]}
        empty="Ningún estado coincide con la búsqueda."
        estado={estado}
        barra={(cargando || (!errorCarga && estados.length > 0)) && (
          <BarraDeFiltros
            resultado={cargando ? '' : (busqueda ? `${visibles.length} de ${estados.length} estados` : `${estados.length} estados`)}
            hayFiltros={busqueda !== ''}
            onLimpiar={() => setBusqueda('')}
          >
            <Buscador label="Buscar" placeholder="Nombre o descripción…" value={busqueda} onChange={setBusqueda} />
          </BarraDeFiltros>
        )}
      >
        {visibles.map((e) => (
          <Tr key={e.id}>
            <Td><EstadoCliente estado={e} /></Td>
            <Td className="min-w-48">{e.descripcion || <span className="text-slate-600">—</span>}</Td>
            <Td>
              {e.esDelSistema
                ? <Badge color="blue"><Lock size={12} aria-hidden="true" />Del sistema</Badge>
                : <span className="whitespace-nowrap">Manual{e.protegido && ', protegido'}</span>}
            </Td>
            <Td className="text-right tabular-nums">{e.clientes}</Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                {puedeModificar && (
                  <Btn variant="ghost" onClick={() => abrirEditor(e)} aria-label={`Editar el estado ${e.nombre}`}>
                    <Pencil size={14} aria-hidden="true" />
                    Editar
                  </Btn>
                )}
                {puedeEliminar && !e.protegido && (
                  <Btn
                    variant="ghost"
                    onClick={() => { setErrorEliminar(''); setAEliminar(e); }}
                    disabled={e.clientes > 0}
                    title={e.clientes > 0 ? 'Tiene clientes: asígnales otro estado antes de eliminarlo.' : undefined}
                    aria-label={`Eliminar el estado ${e.nombre}`}
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
      {puedeEliminar && estados.some((e) => e.clientes > 0 && !e.protegido) && (
        <p className="mt-3 text-sm text-slate-600">Un estado que algún cliente tiene no se puede eliminar: primero asígnale otro.</p>
      )}

      <Modal
        abierto={editor !== null}
        onCerrar={() => setEditor(null)}
        sinGuardar={sinGuardar}
        titulo={editor?.id ? `Editar el estado ${editor.nombre}` : 'Nuevo estado'}
        descripcion={editor?.id ? undefined : 'Un estado nuevo se asigna a mano y saca al cliente del ciclo de visitas.'}
        pie={(
          <>
            <BotonCancelar disabled={guardando} />
            <Btn type="submit" form="editor-estado" size="md" disabled={guardando}>
              {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardando ? 'Guardando…' : 'Guardar'}
            </Btn>
          </>
        )}
      >
        {editor && (
          <form id="editor-estado" ref={editorRef} onSubmit={guardar} className="space-y-4" noValidate>
            {errorEditor && <Alert type="error">{errorEditor}</Alert>}
            <Input
              label="Nombre"
              value={editor.nombre}
              maxLength={50}
              disabled={editor.protegido}
              error={erroresCampo.nombre}
              ayuda={editor.protegido ? `No cambia de nombre. ${porQueEstaProtegido(editor)}` : undefined}
              onChange={(e) => setEditor({ ...editor, nombre: e.target.value })}
              autoComplete="off"
            />
            {editor.esDelSistema ? (
              <div>
                <p className="block text-sm font-medium text-slate-700 mb-1">Color</p>
                <p className="flex items-start gap-1.5 text-sm text-slate-600">
                  <Lock size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
                  El rojo de Activo y el verde de Disponible tienen un significado fijo en el mapa y en las listas: no se cambian.
                </p>
              </div>
            ) : (
              <ElegirColor valor={editor.color} error={erroresCampo.color} onCambiar={(color) => setEditor({ ...editor, color })} />
            )}
            <AreaDeTexto
              label="Descripción (opcional)"
              rows={2}
              maxLength={200}
              value={editor.descripcion}
              error={erroresCampo.descripcion}
              onChange={(e) => setEditor({ ...editor, descripcion: e.target.value })}
              autoComplete="off"
            />
            <div>
              <p className="text-sm font-medium text-slate-700 mb-1">Así se ve</p>
              <EstadoCliente estado={{ id: editor.id, nombre: editor.nombre || 'Nombre del estado', color: editor.color }} />
              {editor.id === INACTIVO && (
                <p className="mt-1 text-sm text-slate-600">Un cliente Inactivo no recibe ventas ni pedidos hasta que vuelva al ciclo.</p>
              )}
            </div>
          </form>
        )}
      </Modal>

      <Confirmar
        abierto={aEliminar !== null}
        titulo={`¿Eliminar el estado ${aEliminar?.nombre ?? ''}?`}
        textoAccion="Eliminar"
        enviando={eliminando}
        error={errorEliminar}
        onConfirmar={eliminar}
        onCancelar={() => setAEliminar(null)}
      >
        <p>El estado deja de existir y ya no se puede asignar. Esto no se puede deshacer.</p>
      </Confirmar>
    </Layout>
  );
}
