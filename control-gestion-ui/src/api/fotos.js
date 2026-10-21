import api from './axios';
import { reducirFoto } from '../utils/reducirFoto';

// Las fotos de los eventos y los comprobantes se suben reducidas, como un formulario multipart con el campo "archivo".
async function formularioCon(archivo) {
  const formulario = new FormData();
  formulario.append('archivo', await reducirFoto(archivo));
  return formulario;
}

/** Agrega una foto a un evento. Devuelve el evento con sus fotos. */
export async function subirFoto(eventoId, archivo) {
  const { data } = await api.post(`/eventos/${eventoId}/fotos`, await formularioCon(archivo));
  return data;
}

/** Adjunta (o reemplaza) el comprobante de un pago. Devuelve la cobranza con sus pagos. */
export async function subirComprobante(cobranzaId, pagoId, archivo) {
  const { data } = await api.put(`/cobranzas/${cobranzaId}/pagos/${pagoId}/comprobante`, await formularioCon(archivo));
  return data;
}

// El campo de archivo: solo imágenes, y en el celular abre la cámara.
export const ACEPTA_FOTOS = 'image/jpeg,image/png,image/webp';
