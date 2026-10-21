import api from './axios';

/**
 * Descarga un archivo de la API (un Excel) con la sesión del usuario. Si falla, el ProblemDetails de la API llega como
 * Blob: se lee y se deja en err.response.data, así mensajeDeError lo entiende igual que en cualquier otra petición.
 */
export async function descargar(ruta, params, nombreArchivo) {
  try {
    const { data } = await api.get(ruta, { params, responseType: 'blob' });
    const url = URL.createObjectURL(data);
    const enlace = document.createElement('a');
    enlace.href = url;
    enlace.download = nombreArchivo;
    document.body.appendChild(enlace);
    enlace.click();
    enlace.remove();
    URL.revokeObjectURL(url);
  } catch (err) {
    if (err.response?.data instanceof Blob) {
      try {
        err.response.data = JSON.parse(await err.response.data.text());
      } catch {
        // No era un ProblemDetails: mensajeDeError usará el texto por defecto.
      }
    }
    throw err;
  }
}
