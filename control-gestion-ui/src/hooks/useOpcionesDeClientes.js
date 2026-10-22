import { useEffect, useState } from 'react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import { useDemorado } from './useDemorado';

/** Cuántos clientes pide cada búsqueda: en producción hay más de 2.000, y no se piden todos. */
export const LIMITE_DE_CLIENTES = 50;

/**
 * Las opciones de clientes para un Combobox, buscadas en la API: espera 300 ms sin teclear, pide los primeros 50 que
 * coinciden y descarta una respuesta que llega después de otra búsqueda. Se usa así:
 * <Combobox opciones={opciones} onBuscar={buscar} cargando={cargando} errorBusqueda={error} limite={LIMITE_DE_CLIENTES} />
 */
export function useOpcionesDeClientes() {
  // null hasta que se abre el selector: no se pide nada antes.
  const [texto, setTexto] = useState(null);
  const demorado = useDemorado(texto, 300);
  const [resultado, setResultado] = useState({ de: null, clientes: [], error: '' });

  useEffect(() => {
    if (demorado === null) return undefined;
    let vigente = true;
    api.get('/clientes/opciones', { params: { buscar: demorado || undefined, limite: LIMITE_DE_CLIENTES } })
      .then(({ data }) => { if (vigente) setResultado({ de: demorado, clientes: data, error: '' }); })
      .catch((err) => {
        if (vigente) setResultado({ de: demorado, clientes: [], error: mensajeDeError(err, 'No se pudieron buscar los clientes.') });
      });
    return () => { vigente = false; };
  }, [demorado]);

  return {
    clientes: resultado.clientes,
    cargando: texto !== null && texto !== resultado.de,
    error: resultado.error,
    buscar: setTexto,
  };
}
