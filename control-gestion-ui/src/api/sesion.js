// La sesión vive en localStorage para sobrevivir a una recarga. Solo se tocan estas claves: cerrar sesión no
// borra las preferencias del usuario, como el menú replegado.
const TOKEN = 'token';
const USUARIO = 'usuario';
const USUARIO_ANTERIOR = 'user'; // formato anterior a los permisos (solo traía roles)

// Si el navegador no deja leer el almacenamiento (modo privado, sitio bloqueado), se actúa como si no hubiera sesión.
export function leerToken() {
  try { return localStorage.getItem(TOKEN); } catch { return null; }
}

export function leerUsuario() {
  try {
    const usuario = JSON.parse(localStorage.getItem(USUARIO));
    return Array.isArray(usuario?.permisos) ? usuario : null;
  } catch {
    return null;
  }
}

export function guardarSesion(token, usuario) {
  localStorage.setItem(TOKEN, token);
  guardarUsuario(usuario);
}

export function guardarUsuario(usuario) {
  localStorage.setItem(USUARIO, JSON.stringify(usuario));
}

export function borrarSesion() {
  try {
    [TOKEN, USUARIO, USUARIO_ANTERIOR].forEach((clave) => localStorage.removeItem(clave));
  } catch {
    // Sin acceso al almacenamiento no hay nada guardado que borrar.
  }
}
