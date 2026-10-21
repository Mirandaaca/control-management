const LADO_MAXIMO = 1920;
const CALIDAD = 0.8;
const IMAGENES = ['image/jpeg', 'image/png', 'image/webp'];

/**
 * Reduce una foto antes de subirla, como hará la app: lado mayor de 1920 px y JPEG al 80 %. Una foto de celular pasa
 * de varios MB a unos cientos de KB y sube rápido. Si el navegador no la puede leer (no es una imagen), devuelve el
 * archivo tal cual: la API lo rechaza con su propio mensaje.
 */
export async function reducirFoto(archivo) {
  if (!IMAGENES.includes(archivo.type) || typeof createImageBitmap !== 'function') return archivo;

  let imagen;
  try {
    imagen = await createImageBitmap(archivo);
  } catch {
    return archivo; // Dice ser una imagen pero no lo es: que lo rechace la API, que valida el contenido.
  }
  const escala = Math.min(1, LADO_MAXIMO / Math.max(imagen.width, imagen.height));
  const ancho = Math.round(imagen.width * escala);
  const alto = Math.round(imagen.height * escala);
  const lienzo = document.createElement('canvas');
  lienzo.width = ancho;
  lienzo.height = alto;
  const contexto = lienzo.getContext('2d');
  // El JPEG no tiene transparencia: sin un fondo blanco, lo transparente de un PNG saldría negro.
  contexto.fillStyle = '#ffffff';
  contexto.fillRect(0, 0, ancho, alto);
  contexto.drawImage(imagen, 0, 0, ancho, alto);
  imagen.close();

  const reducida = await new Promise((resolver) => lienzo.toBlob(resolver, 'image/jpeg', CALIDAD));
  if (!reducida || reducida.size >= archivo.size) return archivo;
  return new File([reducida], `${archivo.name.replace(/\.[^.]+$/, '')}.jpg`, { type: 'image/jpeg' });
}
