import { diaEnBolivia } from './formato';

// Los campos de fecha y hora del panel: el valor viaja como aaaa-mm-dd y hh:mm (lo que recibe la API), y se muestra y se
// escribe como dd/mm/aaaa (docs/diseno.md). Son días de calendario: no se convierten de zona.

const dos = (n) => String(n).padStart(2, '0');

/** aaaa-mm-dd → dd/mm/aaaa; '' si no hay fecha. */
export function aTextoDeDia(dia) {
  if (!dia) return '';
  const [anio, mes, d] = String(dia).slice(0, 10).split('-');
  return anio && mes && d ? `${d}/${mes}/${anio}` : '';
}

/** dd/mm/aaaa → aaaa-mm-dd, o null si no es una fecha que existe (31/02, 00/10…). */
export function textoADia(texto) {
  const partes = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/.exec(String(texto).trim());
  if (!partes) return null;
  const [d, m, a] = [Number(partes[1]), Number(partes[2]), Number(partes[3])];
  const fecha = new Date(Date.UTC(a, m - 1, d));
  if (fecha.getUTCFullYear() !== a || fecha.getUTCMonth() !== m - 1 || fecha.getUTCDate() !== d) return null;
  return `${a}-${dos(m)}-${dos(d)}`;
}

/**
 * Lo que se escribe, partido en sus números: el separador se pone solo cuando uno se llena. Un dígito que no puede
 * empezar un número de dos (un 4 en el día, un 2 en el mes, un 3 en la hora) se completa con un cero, y también el que
 * queda solo antes de un separador escrito al final: así "1/10/2026" no se corre a "11/02/026", ni "9:30" a "93:0".
 * Un separador que no va al final no completa nada: es alguien que corrige en el medio, y el cursor no debe saltar.
 */
function enmascarar(texto, largos, primerDigitoMaximo, separador, separadoresAceptados) {
  const caracteres = [...String(texto)];
  const partes = [''];
  caracteres.forEach((c, posicion) => {
    const i = partes.length - 1;
    if (/\d/.test(c)) {
      if (partes[i].length === largos[i]) {
        if (i === largos.length - 1) return;
        partes.push('');
      }
      const j = partes.length - 1;
      partes[j] = partes[j] === '' && Number(c) > (primerDigitoMaximo[j] ?? 9) ? `0${c}` : partes[j] + c;
    } else if (separadoresAceptados.includes(c) && i < largos.length - 1 && partes[i] !== '') {
      if (posicion === caracteres.length - 1) partes[i] = partes[i].padStart(largos[i], '0');
      partes.push('');
    }
  });
  return partes.join(separador);
}

/** dd/mm/aaaa mientras se escribe: "29102026" → "29/10/2026", "1/" → "01/". Una fecha pegada como aaaa-mm-dd se da vuelta. */
export function enmascararDia(texto) {
  const iso = /^\s*(\d{4})-(\d{2})-(\d{2})\s*$/.exec(String(texto));
  return iso ? `${iso[3]}/${iso[2]}/${iso[1]}` : enmascarar(texto, [2, 2, 4], [3, 1], '/', '/-.');
}

/** hh:mm mientras se escribe: "1030" → "10:30", "9" → "09". */
export function enmascararHora(texto) {
  return enmascarar(texto, [2, 2], [2, 5], ':', ':.h');
}

/** hh:mm → hh:mm con dos dígitos, o null si no es una hora del día. */
export function textoAHora(texto) {
  const partes = /^(\d{1,2}):(\d{2})$/.exec(String(texto).trim());
  if (!partes) return null;
  const [h, m] = [Number(partes[1]), Number(partes[2])];
  return h <= 23 && m <= 59 ? `${dos(h)}:${dos(m)}` : null;
}

/** El día como Date, a la medianoche de la PC: el calendario muestra días, y con las partes locales no se corre. */
export function diaADate(dia) {
  if (!dia) return undefined;
  const [a, m, d] = String(dia).slice(0, 10).split('-').map(Number);
  return new Date(a, m - 1, d);
}

/** El Date que eligió el calendario, como aaaa-mm-dd. */
export function dateADia(fecha) {
  return `${fecha.getFullYear()}-${dos(fecha.getMonth() + 1)}-${dos(fecha.getDate())}`;
}

function sumarDias(dia, dias) {
  const fecha = diaADate(dia);
  fecha.setDate(fecha.getDate() + dias);
  return dateADia(fecha);
}

/** Los atajos para un par "Desde" y "Hasta", contados desde el día de hoy en Bolivia. */
export function atajosDePeriodo(hoy = diaEnBolivia()) {
  const [anio, mes] = hoy.split('-').map(Number);
  const inicioDelMes = `${anio}-${dos(mes)}-01`;
  const finDelMesPasado = dateADia(new Date(anio, mes - 1, 0));
  return [
    { etiqueta: 'Hoy', desde: hoy, hasta: hoy },
    { etiqueta: 'Este mes', desde: inicioDelMes, hasta: hoy },
    { etiqueta: 'Mes pasado', desde: `${finDelMesPasado.slice(0, 8)}01`, hasta: finDelMesPasado },
    { etiqueta: 'Últimos 30 días', desde: sumarDias(hoy, -29), hasta: hoy },
    { etiqueta: 'Este año', desde: `${anio}-01-01`, hasta: hoy },
  ];
}

/** Las horas de un día, de 15 en 15 minutos, para elegir sin escribir. */
export const HORAS_DEL_DIA = Array.from({ length: 96 }, (_, i) => `${dos(Math.floor(i / 4))}:${dos((i % 4) * 15)}`);
