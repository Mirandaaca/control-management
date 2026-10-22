import { useCallback, useEffect, useState } from 'react';
import { CalendarClock } from 'lucide-react';
import api from '../api/axios';
import { erroresPorCampo, mensajeDeError } from '../api/errores';
import Layout from '../components/Layout';
import { Alert, Btn, Card, Cargando, Confirmar, ErrorDeCarga, Input, PageHeader } from '../components/ui';

const MENSAJE_DE_RANGO = 'Los días de re-visita van de 1 a 365.';

const cambiaron = (n, estado) => {
  if (n === 0) return `ninguno pasó a ${estado}`;
  return n === 1 ? `1 pasó a ${estado}` : `${n} pasaron a ${estado}`;
};

function resumenDelCambio({ dias, clientesRecalculados, pasaronADisponible, pasaronAActivo }) {
  if (clientesRecalculados === 0) return `Ahora son ${dias} días. Ningún cliente tiene una visita registrada: no hubo nada que recalcular.`;
  const clientes = clientesRecalculados === 1 ? '1 cliente' : `${clientesRecalculados} clientes`;
  return `Ahora son ${dias} días. Se recalculó la próxima visita de ${clientes}: `
    + `${cambiaron(pasaronADisponible, 'Disponible')} y ${cambiaron(pasaronAActivo, 'Activo')}.`;
}

/**
 * Configuración general (7.1, tesis 7.3.27). Por ahora solo los días de re-visita: cambiarlos mueve la próxima visita de
 * todos los clientes con una visita registrada y puede cambiar su estado en el momento, por eso se confirma.
 */
export default function ConfiguracionGeneral() {
  const [guardados, setGuardados] = useState(null);
  const [dias, setDias] = useState('');
  const [cargando, setCargando] = useState(true);
  const [reintentando, setReintentando] = useState(false);
  const [errorCarga, setErrorCarga] = useState('');
  const [errorCampo, setErrorCampo] = useState('');
  const [confirmando, setConfirmando] = useState(false);
  const [guardando, setGuardando] = useState(false);
  const [errorGuardar, setErrorGuardar] = useState('');
  const [aviso, setAviso] = useState('');

  const cargar = useCallback(() => api.get('/configuraciones/dias-revisita')
    .then(({ data }) => { setGuardados(data.dias); setDias(String(data.dias)); setErrorCarga(''); })
    .catch((err) => setErrorCarga(mensajeDeError(err, 'No se pudo cargar la configuración general.')))
    .finally(() => { setCargando(false); setReintentando(false); }), []);

  useEffect(() => { cargar(); }, [cargar]);

  const numero = Number(dias);

  // Un valor fuera de rango ni siquiera abre la confirmación: la API respondería lo mismo.
  const pedirConfirmacion = (e) => {
    e.preventDefault();
    setAviso('');
    setErrorGuardar('');
    if (!Number.isInteger(numero) || numero < 1 || numero > 365) {
      setErrorCampo(MENSAJE_DE_RANGO);
      return;
    }
    setErrorCampo('');
    setConfirmando(true);
  };

  const guardar = async () => {
    setGuardando(true);
    setErrorGuardar('');
    try {
      const { data } = await api.put('/configuraciones/dias-revisita', { dias: numero });
      setGuardados(data.dias);
      setDias(String(data.dias));
      setConfirmando(false);
      setAviso(resumenDelCambio(data));
    } catch (err) {
      const campo = erroresPorCampo(err).dias;
      if (campo) {
        setConfirmando(false);
        setErrorCampo(campo);
      } else {
        setErrorGuardar(mensajeDeError(err, 'No se pudieron cambiar los días de re-visita.'));
      }
    } finally {
      setGuardando(false);
    }
  };

  let contenido;
  if (cargando) contenido = <Card><Cargando texto="Cargando la configuración general…" /></Card>;
  else if (errorCarga) contenido = (
    <Card className="p-0">
      <ErrorDeCarga mensaje={errorCarga} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
    </Card>
  );
  else contenido = (
    <Card className="max-w-2xl">
      <div className="flex items-start gap-3 mb-5">
        <span className="grid place-items-center w-9 h-9 rounded-lg bg-marca-50 text-marca-700 shrink-0" aria-hidden="true">
          <CalendarClock size={18} />
        </span>
        <div className="space-y-2 text-sm text-slate-600">
          <h3 className="text-base font-semibold text-slate-900">Días de re-visita</h3>
          <p>
            Cada visita a un cliente (una venta o una visita marcada en la ruta) fija su próxima visita a estos días. Mientras
            no llega, el cliente está Activo; cuando llega, pasa a Disponible para re-visita.
          </p>
          <p>
            Al cambiar el número se mueve la próxima visita de todos los clientes con una visita registrada, y algunos pueden
            pasar a Disponible o volver a Activo en ese momento. Inactivo y los demás estados asignados a mano no cambian.
          </p>
        </div>
      </div>
      <form onSubmit={pedirConfirmacion} className="space-y-4" noValidate>
        {aviso && <Alert type="success">{aviso}</Alert>}
        <div className="w-full sm:w-48">
          <Input label="Días de re-visita" type="number" inputMode="numeric" min="1" max="365" step="1" autoComplete="off"
            value={dias} error={errorCampo} ayuda="Un número entero, de 1 a 365."
            onChange={(e) => { setDias(e.target.value); setErrorCampo(''); }} />
        </div>
        <div className="flex justify-end">
          <Btn type="submit" size="md" disabled={dias === String(guardados)}>Guardar cambios</Btn>
        </div>
      </form>
    </Card>
  );

  return (
    <Layout>
      <PageHeader title="Configuración general" subtitle="Los parámetros que usa el sistema para todos los clientes." />
      {contenido}
      <Confirmar
        abierto={confirmando}
        titulo={`¿Cambiar los días de re-visita a ${numero}?`}
        textoAccion={guardando ? 'Cambiando…' : `Cambiar a ${numero} días`}
        variante="primary"
        enviando={guardando}
        error={errorGuardar}
        onConfirmar={guardar}
        onCancelar={() => setConfirmando(false)}
      >
        <p>
          Se recalcula la próxima visita de todos los clientes con una visita registrada: su última visita más {numero} días.
        </p>
        <p>Los que siguen el ciclo de visitas pueden pasar a Disponible o volver a Activo en este momento.</p>
      </Confirmar>
    </Layout>
  );
}
