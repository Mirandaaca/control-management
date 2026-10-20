import { useCallback, useEffect, useState } from 'react';
import { LoaderCircle, Lock, Pencil, Plus, ShieldCheck, Trash2 } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Table, Td, Tr } from '../components/Table';
import {
  Alert, Badge, BarraDeFiltros, Btn, Buscador, Cargando, Casilla, Confirmar, ErrorDeCarga, EstadoVacio, Input, Modal,
  PageHeader,
} from '../components/ui';
import { useAuth } from '../context/auth';
import { PERMISOS } from '../context/permisos';
import { coincide } from '../utils/texto';

const { seguridad } = PERMISOS;

// Errores de la API que pertenecen a un campo del editor: se muestran junto a él.
const CAMPO_DEL_ERROR = {
  rol_duplicado: 'nombre', rol_protegido: 'nombre', permisos_protegidos: 'permisos', permiso_no_existe: 'permisos',
};

/** El catálogo agrupado por módulo, en el orden de docs/permisos-por-rol.md. */
function agruparPorModulo(catalogo) {
  const grupos = new Map();
  for (const permiso of catalogo) {
    if (!grupos.has(permiso.modulo)) grupos.set(permiso.modulo, []);
    grupos.get(permiso.modulo).push(permiso);
  }
  return [...grupos.entries()].map(([modulo, permisos]) => ({ modulo, permisos }));
}

function ModuloDePermisos({ modulo, permisos, elegidos, protegidos, onCambiar }) {
  const marcados = permisos.filter((p) => elegidos.has(p.clave)).length;
  const todos = marcados === permisos.length;
  const alternarTodos = () => onCambiar(
    permisos.map((p) => p.clave).filter((c) => !protegidos.has(c)),
    !todos,
  );
  return (
    <fieldset className="rounded-lg border border-slate-200 p-4">
      <legend className="sr-only">{modulo}</legend>
      <div className="flex flex-wrap items-center justify-between gap-2 mb-3">
        <Casilla checked={todos ? true : marcados > 0 ? 'indeterminate' : false} onCheckedChange={alternarTodos}>
          <span className="font-semibold">{modulo}</span>
        </Casilla>
        <span className="text-xs text-slate-600">{marcados} de {permisos.length}</span>
      </div>
      <div className="grid gap-2.5 pl-6 sm:grid-cols-2">
        {permisos.map((p) => {
          const protegido = protegidos.has(p.clave);
          return (
            <Casilla
              key={p.clave}
              checked={elegidos.has(p.clave)}
              disabled={protegido}
              onCheckedChange={(valor) => onCambiar([p.clave], valor === true)}
            >
              {p.nombre}
              {protegido && (
                <span className="ml-1.5 inline-flex items-center gap-1 text-xs text-slate-600">
                  <Lock size={12} aria-hidden="true" />
                  siempre
                </span>
              )}
            </Casilla>
          );
        })}
      </div>
    </fieldset>
  );
}

export default function Roles() {
  const { tiene, refrescar } = useAuth();
  const puedeCrear = tiene(seguridad.crearRol);
  const puedeEditar = tiene(seguridad.editarRol);
  const puedeEliminar = tiene(seguridad.eliminarRol);

  const [roles, setRoles] = useState([]);
  const [catalogo, setCatalogo] = useState([]);
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [busqueda, setBusqueda] = useState('');
  const [aviso, setAviso] = useState('');

  const [editor, setEditor] = useState(null);
  const [erroresCampo, setErroresCampo] = useState({});
  const [errorEditor, setErrorEditor] = useState('');
  const [guardando, setGuardando] = useState(false);

  const [aEliminar, setAEliminar] = useState(null);
  const [errorEliminar, setErrorEliminar] = useState('');
  const [eliminando, setEliminando] = useState(false);

  const cargar = useCallback(() => Promise.all([
    api.get('/roles'),
    puedeCrear || puedeEditar ? api.get('/roles/permisos') : Promise.resolve({ data: [] }),
  ])
    .then(([r, c]) => {
      setRoles(r.data);
      setCatalogo(c.data);
      setErrorCarga('');
    })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la lista de roles.')))
    .finally(() => { setCargando(false); setReintentando(false); }), [puedeCrear, puedeEditar]);

  useEffect(() => { cargar(); }, [cargar]);

  const abrirEditor = (rol) => {
    setErroresCampo({});
    setErrorEditor('');
    setEditor(rol
      ? { id: rol.id, nombre: rol.nombre, protegido: rol.protegido, elegidos: new Set(rol.permisos), protegidos: new Set(rol.permisosProtegidos) }
      : { nombre: '', protegido: false, elegidos: new Set(), protegidos: new Set() });
  };

  const cambiarPermisos = (claves, marcar) => setEditor((e) => {
    const elegidos = new Set(e.elegidos);
    claves.forEach((c) => (marcar ? elegidos.add(c) : elegidos.delete(c)));
    return { ...e, elegidos };
  });

  const guardar = async (ev) => {
    ev.preventDefault();
    setGuardando(true);
    setAviso('');
    setErroresCampo({});
    setErrorEditor('');
    const datos = { nombre: editor.nombre, permisos: [...editor.elegidos] };
    try {
      const { data } = editor.id ? await api.put(`/roles/${editor.id}`, datos) : await api.post('/roles', datos);
      setRoles((lista) => (editor.id ? lista.map((r) => (r.id === data.id ? data : r)) : [...lista, data])
        .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es')));
      setAviso(editor.id ? `Se guardaron los permisos de ${data.nombre}.` : `Se creó el rol ${data.nombre}.`);
      setEditor(null);
      // Si cambiaron los permisos del propio rol, el menú se actualiza enseguida.
      refrescar().catch((err) => console.warn('No se pudo actualizar la sesión.', err));
    } catch (err) {
      const campos = erroresPorCampo(err);
      const campoDelCodigo = CAMPO_DEL_ERROR[err.response?.data?.codigo];
      if (campoDelCodigo) campos[campoDelCodigo] = err.response.data.detail;
      setErroresCampo(campos);
      if (Object.keys(campos).length === 0) setErrorEditor(mensajeDeError(err, 'No se pudo guardar el rol.'));
    } finally {
      setGuardando(false);
    }
  };

  const eliminar = async () => {
    setEliminando(true);
    setErrorEliminar('');
    try {
      await api.delete(`/roles/${aEliminar.id}`);
      setRoles((lista) => lista.filter((r) => r.id !== aEliminar.id));
      setAviso(`Se eliminó el rol ${aEliminar.nombre}.`);
      setAEliminar(null);
    } catch (err) {
      setErrorEliminar(mensajeDeError(err, 'No se pudo eliminar el rol.'));
    } finally {
      setEliminando(false);
    }
  };

  const visibles = roles.filter((r) => coincide(busqueda, r.nombre));
  const modulos = agruparPorModulo(catalogo);

  let estado = null;
  if (cargando) estado = <Cargando texto="Cargando roles…" />;
  else if (errorCarga) estado = <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />;
  else if (roles.length === 0) estado = <EstadoVacio titulo="Todavía no hay roles" icono={ShieldCheck} />;

  return (
    <Layout>
      <PageHeader
        title="Roles y permisos"
        subtitle="Qué puede hacer cada rol. Un cambio aplica a sus usuarios sin que vuelvan a iniciar sesión."
        action={puedeCrear && (
          <Btn size="md" onClick={() => abrirEditor(null)} disabled={catalogo.length === 0}>
            <Plus size={16} aria-hidden="true" />
            Nuevo rol
          </Btn>
        )}
      />

      {aviso && <Alert type="success" className="mb-4">{aviso}</Alert>}

      <Table
        headers={['Rol', 'Usuarios', 'Permisos', '']}
        empty="Ningún rol coincide con la búsqueda."
        estado={estado}
        barra={!cargando && !errorCarga && roles.length > 0 && (
          <BarraDeFiltros
            resultado={busqueda ? `${visibles.length} de ${roles.length} roles` : `${roles.length} roles`}
            hayFiltros={busqueda !== ''}
            onLimpiar={() => setBusqueda('')}
          >
            <Buscador label="Buscar" placeholder="Nombre del rol" value={busqueda} onChange={setBusqueda} />
          </BarraDeFiltros>
        )}
      >
        {visibles.map((r) => (
          <Tr key={r.id}>
            <Td>
              <span className="font-medium">{r.nombre}</span>
              {r.protegido && (
                <span className="ml-2"><Badge color="blue"><Lock size={12} aria-hidden="true" />Protegido</Badge></span>
              )}
            </Td>
            <Td className="tabular-nums">{r.usuarios}</Td>
            <Td className="tabular-nums whitespace-nowrap">
              {catalogo.length > 0 ? `${r.permisos.length} de ${catalogo.length}` : r.permisos.length}
            </Td>
            <Td>
              <div className="flex items-center justify-end gap-1">
                {puedeEditar && (
                  <Btn variant="ghost" onClick={() => abrirEditor(r)} disabled={catalogo.length === 0} aria-label={`Editar el rol ${r.nombre}`}>
                    <Pencil size={14} aria-hidden="true" />
                    Editar
                  </Btn>
                )}
                {puedeEliminar && !r.protegido && (
                  <Btn
                    variant="ghost"
                    onClick={() => { setErrorEliminar(''); setAEliminar(r); }}
                    disabled={r.usuarios > 0}
                    title={r.usuarios > 0 ? 'Tiene usuarios: asígnales otro rol antes de eliminarlo.' : undefined}
                    aria-label={`Eliminar el rol ${r.nombre}`}
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
      {puedeEliminar && roles.some((r) => r.usuarios > 0 && !r.protegido) && (
        <p className="mt-3 text-sm text-slate-600">Un rol con usuarios no se puede eliminar: primero asígnales otro rol.</p>
      )}

      <Modal
        abierto={editor !== null}
        onCerrar={() => setEditor(null)}
        ancho="lg"
        titulo={editor?.id ? `Editar el rol ${editor.nombre}` : 'Nuevo rol'}
        descripcion="Marca las operaciones que puede hacer. Un rol tiene al menos un permiso."
        pie={(
          <>
            <span className="mr-auto self-center text-sm text-slate-600">{editor?.elegidos.size ?? 0} permisos elegidos</span>
            <Btn variant="outline" size="md" onClick={() => setEditor(null)} disabled={guardando}>Cancelar</Btn>
            <Btn type="submit" form="editor-rol" size="md" disabled={guardando}>
              {guardando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
              {guardando ? 'Guardando…' : 'Guardar'}
            </Btn>
          </>
        )}
      >
        {editor && (
          <form id="editor-rol" onSubmit={guardar} className="space-y-4" noValidate>
            {errorEditor && <Alert type="error">{errorEditor}</Alert>}
            <Input
              label="Nombre del rol"
              value={editor.nombre}
              disabled={editor.protegido}
              error={erroresCampo.nombre}
              ayuda={editor.protegido ? 'El rol del administrador no se renombra ni se elimina.' : undefined}
              onChange={(e) => setEditor({ ...editor, nombre: e.target.value })}
              autoComplete="off"
            />
            {erroresCampo.permisos && <Alert type="error">{erroresCampo.permisos}</Alert>}
            <div className="space-y-3">
              {modulos.map((m) => (
                <ModuloDePermisos
                  key={m.modulo}
                  modulo={m.modulo}
                  permisos={m.permisos}
                  elegidos={editor.elegidos}
                  protegidos={editor.protegidos}
                  onCambiar={cambiarPermisos}
                />
              ))}
            </div>
          </form>
        )}
      </Modal>

      <Confirmar
        abierto={aEliminar !== null}
        titulo={`¿Eliminar el rol ${aEliminar?.nombre ?? ''}?`}
        textoAccion="Eliminar"
        enviando={eliminando}
        error={errorEliminar}
        onConfirmar={eliminar}
        onCancelar={() => setAEliminar(null)}
      >
        <p>El rol y su lista de permisos se borran. Esto no se puede deshacer.</p>
      </Confirmar>
    </Layout>
  );
}
