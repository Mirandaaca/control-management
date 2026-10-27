import { useId, useRef, useState } from 'react';
import {
  Calendar as IconoDeCalendario, Check, ChevronLeft, ChevronRight, ChevronsUpDown, CircleAlert, Clock, Inbox, LoaderCircle,
  RefreshCw, Search, X,
} from 'lucide-react';
import {
  aTextoDeDia, dateADia, diaADate, enmascararDia, enmascararHora, HORAS_DEL_DIA, textoADia, textoAHora,
} from '../../utils/fechas';
import { diaEnBolivia } from '../../utils/formato';
import { coincide } from '../../utils/texto';
import { Calendario } from './calendar';
import { Checkbox } from './checkbox';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from './command';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from './dialog';
import { Popover, PopoverAnchor, PopoverContent, PopoverTrigger } from './popover';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from './select';
import { Alert } from './basicos';

// Componentes del panel con el diseño de docs/diseno.md. Los archivos en minúscula de esta carpeta (dialog, select,
// checkbox, command, popover, calendar, sonner y skeleton) son primitivos: las pantallas usan los de aquí, que ya traen
// los textos y el estilo. Alert, Card y los esqueletos viven en basicos.jsx, sin dependencias pesadas, y se exportan
// también desde aquí.

export {
  Alert, BarraDeRecarga, Card, Esqueleto, EsqueletoDeDetalle, EsqueletoDeGrafico, EsqueletoDeIndicadores,
  EsqueletoDeLista, EsqueletoDeMapa, EsqueletoDePagina, ZonaDeCarga,
} from './basicos';

export function PageHeader({ title, subtitle, action }) {
  return (
    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between mb-6">
      <div className="min-w-0">
        <h2 className="text-2xl font-semibold text-slate-900">{title}</h2>
        {subtitle && <p className="text-slate-600 text-sm mt-1">{subtitle}</p>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  );
}

export function Badge({ children, color = 'gray' }) {
  const colors = {
    green: 'bg-green-50 text-green-800',
    red: 'bg-red-50 text-red-700',
    blue: 'bg-marca-50 text-marca-800',
    yellow: 'bg-amber-50 text-amber-800',
    gray: 'bg-slate-100 text-slate-700',
    purple: 'bg-purple-50 text-purple-800',
  };
  return (
    <span className={`inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-semibold whitespace-nowrap ${colors[color]}`}>
      {children}
    </span>
  );
}

const foco = 'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 focus-visible:ring-offset-2';

const tamanosDeBoton = {
  sm: 'h-8 px-3 text-xs',
  md: 'h-10 px-4 text-sm',
  lg: 'h-11 px-5 text-sm',
};
const variantesDeBoton = {
  primary: 'bg-marca-600 hover:bg-marca-700 text-white',
  danger: 'bg-red-600 hover:bg-red-700 text-white',
  ghost: 'bg-transparent hover:bg-slate-100 text-slate-700',
  outline: 'bg-white border border-borde-control hover:bg-slate-50 text-slate-800',
};
const clasesDeBoton = (variant, size) =>
  `inline-flex items-center justify-center gap-1.5 font-medium rounded-lg transition-colors cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed ${foco} ${tamanosDeBoton[size]} ${variantesDeBoton[variant]}`;

export function Btn({ children, onClick, variant = 'primary', size = 'sm', type = 'button', disabled = false, ...props }) {
  return (
    <button {...props} type={type} onClick={onClick} disabled={disabled} className={clasesDeBoton(variant, size)}>
      {children}
    </button>
  );
}

/**
 * Un enlace con la forma de un botón: para lo que lleva a otra página, como la HP para imprimir, que se abre en una
 * pestaña nueva. La navegación va en un <a>, no en un <button>.
 */
export function EnlaceBoton({ children, href, variant = 'outline', size = 'sm', nuevaPestana = false, ...props }) {
  return (
    <a {...props} href={href} className={clasesDeBoton(variant, size)}
      {...(nuevaPestana ? { target: '_blank', rel: 'noopener noreferrer' } : {})}>
      {children}
    </a>
  );
}

/**
 * Etiqueta arriba, el control, y debajo la ayuda o el error del campo (que viene en errors del ProblemDetails). Con
 * etiquetaOculta, la etiqueta es solo para el lector de pantalla: la de afuera ya la muestra (CampoFechaHora). Con
 * alerta, el lector anuncia el error apenas aparece: es el que el campo revisa solo al salir de él, cuando el foco ya
 * está en otro lado.
 */
function Campo({ id, label, ayuda, error, children, etiquetaOculta = false, alerta = false }) {
  return (
    <div>
      {label && (
        <label htmlFor={id} className={etiquetaOculta ? 'sr-only' : 'block text-sm font-medium text-slate-700 mb-1'}>{label}</label>
      )}
      {children}
      {error ? (
        <p id={`${id}-error`} role={alerta ? 'alert' : undefined} className="mt-1 flex items-start gap-1 text-sm text-red-700">
          <CircleAlert size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
          {error}
        </p>
      ) : ayuda && <p id={`${id}-ayuda`} className="mt-1 text-sm text-slate-600">{ayuda}</p>}
    </div>
  );
}

function describirCon(id, error, ayuda) {
  if (error) return `${id}-error`;
  return ayuda ? `${id}-ayuda` : undefined;
}

export function Input({ label, ayuda, error, id, ...props }) {
  const generado = useId();
  const campoId = id ?? generado;
  return (
    <Campo id={campoId} label={label} ayuda={ayuda} error={error}>
      <input
        {...props}
        id={campoId}
        aria-invalid={error ? true : undefined}
        aria-describedby={describirCon(campoId, error, ayuda)}
        className={`w-full h-10 px-3 border rounded-lg text-sm text-slate-900 placeholder:text-slate-500 bg-white transition focus:outline-none focus:ring-2 focus:ring-marca-500 focus:border-marca-500 disabled:bg-slate-50 disabled:text-slate-600 ${
          error ? 'border-red-600' : 'border-borde-control'
        }`}
      />
    </Campo>
  );
}

/** Texto de varias líneas, como un motivo o una observación, con su etiqueta y su error. */
export function AreaDeTexto({ label, ayuda, error, id, rows = 3, ...props }) {
  const generado = useId();
  const campoId = id ?? generado;
  return (
    <Campo id={campoId} label={label} ayuda={ayuda} error={error}>
      <textarea
        {...props}
        id={campoId}
        rows={rows}
        aria-invalid={error ? true : undefined}
        aria-describedby={describirCon(campoId, error, ayuda)}
        className={`w-full px-3 py-2 border rounded-lg text-sm text-slate-900 placeholder:text-slate-500 bg-white resize-y transition focus:outline-none focus:ring-2 focus:ring-marca-500 focus:border-marca-500 ${
          error ? 'border-red-600' : 'border-borde-control'
        }`}
      />
    </Campo>
  );
}

const claseDeCampo = (error) => `w-full h-10 border rounded-lg text-sm text-slate-900 tabular-nums placeholder:text-slate-500 bg-white transition focus:outline-none focus:ring-2 focus:ring-marca-500 focus:border-marca-500 disabled:bg-slate-50 disabled:text-slate-600 ${
  error ? 'border-red-600' : 'border-borde-control'
}`;

const botonDentroDelCampo = `absolute right-1 top-1/2 -translate-y-1/2 inline-flex size-8 items-center justify-center rounded-md text-slate-600 hover:bg-slate-100 cursor-pointer disabled:cursor-not-allowed ${foco}`;

function mensajeFueraDeRango(min, max) {
  if (min && max) return `Elige un día entre el ${aTextoDeDia(min)} y el ${aTextoDeDia(max)}.`;
  return min ? `Elige un día desde el ${aTextoDeDia(min)}.` : `Elige un día hasta el ${aTextoDeDia(max)}.`;
}

/**
 * Un día, escrito como dd/mm/aaaa o elegido en el calendario, como en los diseños 8.6.2 y 8.6.4. Reemplaza a
 * <input type="date">, que cada navegador dibuja a su manera (uno en inglés muestra mm/dd/yyyy).
 * - value es aaaa-mm-dd, y onChange recibe un evento como el de un input ({ target: { value } }): quien lo usa no cambia.
 * - min y max (aaaa-mm-dd) deshabilitan los días de afuera y rechazan los que se escriban.
 * - atajos y onAtajo van en el "Desde" de un par: el calendario ofrece períodos (atajosDePeriodo de utils/fechas), y
 *   onAtajo({ desde, hasta }) llena los dos campos.
 * - Alt+↓ abre el calendario. Enter confirma lo escrito antes de que se envíe el formulario.
 * - invalido y descritoPor los usa CampoFechaHora: el error de los dos campos se muestra una vez, debajo de ellos, y
 *   cada campo queda marcado y apunta a él.
 */
export function CampoFecha({
  label, ayuda, error, id, name, value, onChange, min, max, required, disabled, atajos, onAtajo, etiquetaOculta,
  invalido, descritoPor,
}) {
  const generado = useId();
  const campoId = id ?? generado;
  const [abierto, setAbierto] = useState(false);
  // Lo que se está escribiendo; null mientras no se escribe, y entonces se muestra el valor.
  const [borrador, setBorrador] = useState(null);
  const [errorPropio, setErrorPropio] = useState('');
  const hoy = diaEnBolivia();
  const fueraDeRango = (dia) => Boolean((min && dia < min) || (max && dia > max));

  const cambiar = (dia) => {
    setBorrador(null);
    setErrorPropio('');
    if (dia !== (value ?? '')) onChange?.({ target: { value: dia, name } });
  };

  const confirmarLoEscrito = () => {
    if (borrador === null) return;
    if (borrador.trim() === '') {
      cambiar('');
      return;
    }
    const dia = textoADia(borrador);
    if (!dia) setErrorPropio('Escribe la fecha como dd/mm/aaaa.');
    else if (fueraDeRango(dia)) setErrorPropio(mensajeFueraDeRango(min, max));
    else cambiar(dia);
  };

  const elegir = (fecha) => {
    if (fecha) cambiar(dateADia(fecha));
    setAbierto(false);
  };

  const aplicarAtajo = (atajo) => {
    setBorrador(null);
    setErrorPropio('');
    onAtajo({ desde: atajo.desde, hasta: atajo.hasta });
    setAbierto(false);
  };

  const errorVisible = errorPropio || error;
  const diasDeAfuera = [min && { before: diaADate(min) }, max && { after: diaADate(max) }].filter(Boolean);

  return (
    <Campo id={campoId} label={label} ayuda={ayuda} error={errorVisible} etiquetaOculta={etiquetaOculta} alerta={Boolean(errorPropio)}>
      <Popover open={abierto} onOpenChange={setAbierto} modal>
        <PopoverAnchor asChild>
          <div className="relative">
            <input
              id={campoId}
              name={name}
              type="text"
              inputMode="numeric"
              autoComplete="off"
              placeholder="dd/mm/aaaa"
              maxLength={10}
              required={required}
              disabled={disabled}
              value={borrador ?? aTextoDeDia(value)}
              onChange={(e) => {
                setBorrador(enmascararDia(e.target.value));
                setErrorPropio('');
              }}
              onBlur={confirmarLoEscrito}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && borrador !== null) {
                  e.preventDefault();
                  confirmarLoEscrito();
                } else if (e.altKey && e.key === 'ArrowDown') {
                  e.preventDefault();
                  setAbierto(true);
                }
              }}
              aria-invalid={errorVisible || invalido ? true : undefined}
              aria-describedby={errorVisible ? `${campoId}-error` : (descritoPor ?? describirCon(campoId, errorVisible, ayuda))}
              className={`${claseDeCampo(errorVisible || invalido)} pl-3 pr-11`}
            />
            <PopoverTrigger asChild>
              <button type="button" disabled={disabled} aria-label={`Elegir en el calendario: ${label}`} className={botonDentroDelCampo}>
                <IconoDeCalendario size={16} aria-hidden="true" />
              </button>
            </PopoverTrigger>
          </div>
        </PopoverAnchor>
        <PopoverContent align="start" collisionPadding={16} className="w-auto max-w-[calc(100vw-2rem)] p-3" aria-label={`Calendario: ${label}`}>
          <div className="flex flex-col gap-3 sm:flex-row">
            {atajos && onAtajo && (
              <ul aria-label="Períodos" className="flex flex-wrap gap-1 sm:flex-col sm:flex-nowrap sm:border-r sm:border-slate-200 sm:pr-3">
                {atajos.map((atajo) => (
                  <li key={atajo.etiqueta}>
                    <button
                      type="button"
                      onClick={() => aplicarAtajo(atajo)}
                      className={`w-full whitespace-nowrap rounded-md px-2 py-1.5 text-left text-sm text-slate-700 hover:bg-marca-50 cursor-pointer ${foco}`}
                    >
                      {atajo.etiqueta}
                    </button>
                  </li>
                ))}
              </ul>
            )}
            <div className="self-center sm:self-auto">
              <Calendario
                mode="single"
                required
                selected={diaADate(value)}
                defaultMonth={diaADate(value) ?? diaADate(min && min > hoy ? min : hoy)}
                onSelect={elegir}
                disabled={diasDeAfuera}
                today={diaADate(hoy)}
                autoFocus
              />
              <div className="mt-2 flex items-center justify-between gap-2 border-t border-slate-200 pt-2">
                {fueraDeRango(hoy) ? <span /> : <Btn variant="ghost" onClick={() => elegir(diaADate(hoy))}>Hoy</Btn>}
                {!required && value && (
                  <Btn variant="ghost" onClick={() => { cambiar(''); setAbierto(false); }}>Borrar</Btn>
                )}
              </div>
            </div>
          </div>
        </PopoverContent>
      </Popover>
    </Campo>
  );
}

function moverConFlechas(e) {
  if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
  e.preventDefault();
  const fila = e.target.closest('li');
  const otra = e.key === 'ArrowDown' ? fila?.nextElementSibling : fila?.previousElementSibling;
  otra?.querySelector('button')?.focus();
}

/** Una hora, escrita como hh:mm o elegida de la lista, de 15 en 15 minutos. Va dentro de CampoFechaHora. */
function CampoHora({ label, id, value, onChange, required, disabled, etiquetaOculta, invalido, descritoPor }) {
  const generado = useId();
  const campoId = id ?? generado;
  const [abierto, setAbierto] = useState(false);
  const [borrador, setBorrador] = useState(null);
  const [errorPropio, setErrorPropio] = useState('');
  const lista = useRef(null);

  const cambiar = (hora) => {
    setBorrador(null);
    setErrorPropio('');
    if (hora !== (value ?? '')) onChange(hora);
  };

  const confirmarLoEscrito = () => {
    if (borrador === null) return;
    if (borrador.trim() === '') {
      cambiar('');
      return;
    }
    const hora = textoAHora(borrador);
    if (hora) cambiar(hora);
    else setErrorPropio('Escribe la hora como hh:mm.');
  };

  // Al abrir, la hora elegida (o las 08:00) queda a la vista y con el foco. Es la única de la lista que recibe el foco
  // con Tab: las demás se recorren con las flechas, así Tab no pasa por 96 horas.
  const enfocable = value || '08:00';
  const enfocarLaElegida = (e) => {
    e.preventDefault();
    const boton = lista.current?.querySelector(`[data-hora="${enfocable}"]`);
    boton?.scrollIntoView({ block: 'center' });
    boton?.focus({ preventScroll: true });
  };

  return (
    <Campo id={campoId} label={label} error={errorPropio} etiquetaOculta={etiquetaOculta} alerta={Boolean(errorPropio)}>
      <Popover open={abierto} onOpenChange={setAbierto} modal>
        <PopoverAnchor asChild>
          <div className="relative">
            <input
              id={campoId}
              type="text"
              inputMode="numeric"
              autoComplete="off"
              placeholder="hh:mm"
              maxLength={5}
              required={required}
              disabled={disabled}
              value={borrador ?? (value || '')}
              onChange={(e) => {
                setBorrador(enmascararHora(e.target.value));
                setErrorPropio('');
              }}
              onBlur={confirmarLoEscrito}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && borrador !== null) {
                  e.preventDefault();
                  confirmarLoEscrito();
                } else if (e.altKey && e.key === 'ArrowDown') {
                  e.preventDefault();
                  setAbierto(true);
                }
              }}
              aria-invalid={errorPropio || invalido ? true : undefined}
              aria-describedby={errorPropio ? `${campoId}-error` : descritoPor}
              className={`${claseDeCampo(errorPropio || invalido)} pl-3 pr-11`}
            />
            <PopoverTrigger asChild>
              <button type="button" disabled={disabled} aria-label={`Elegir de la lista: ${label}`} className={botonDentroDelCampo}>
                <Clock size={16} aria-hidden="true" />
              </button>
            </PopoverTrigger>
          </div>
        </PopoverAnchor>
        <PopoverContent align="start" collisionPadding={16} className="w-36 p-1" onOpenAutoFocus={enfocarLaElegida} aria-label={`Horas: ${label}`}>
          <ul ref={lista} className="max-h-60 overflow-y-auto">
            {HORAS_DEL_DIA.map((hora) => (
              <li key={hora}>
                <button
                  type="button"
                  data-hora={hora}
                  tabIndex={hora === enfocable ? 0 : -1}
                  aria-current={hora === value ? 'true' : undefined}
                  onClick={() => { cambiar(hora); setAbierto(false); }}
                  onKeyDown={moverConFlechas}
                  className={`w-full rounded-md px-3 py-1.5 text-left text-sm tabular-nums cursor-pointer ${foco} ${
                    hora === value ? 'bg-marca-600 text-white hover:bg-marca-700' : 'text-slate-800 hover:bg-marca-50'
                  }`}
                >
                  {hora}
                </button>
              </li>
            ))}
          </ul>
        </PopoverContent>
      </Popover>
    </Campo>
  );
}

/**
 * Un día y una hora, como la entrega de un pedido. Reemplaza a <input type="datetime-local">: el valor es
 * aaaa-mm-ddThh:mm en la hora de Bolivia, y onChange recibe { target: { value } }. Mientras falta una de las dos partes,
 * el valor es ''. min (aaaa-mm-ddThh:mm) limita el día; que la hora no haya pasado lo revisa la API.
 */
export function CampoFechaHora({ label, ayuda, error, id, name, value, onChange, min, required, disabled }) {
  const generado = useId();
  const campoId = id ?? generado;
  // La parte elegida mientras falta la otra; con el valor completo, las dos salen del valor.
  const [parcial, setParcial] = useState({ dia: '', hora: '' });
  const dia = value ? value.slice(0, 10) : parcial.dia;
  const hora = value ? value.slice(11, 16) : parcial.hora;

  const cambiar = (nuevoDia, nuevaHora) => {
    setParcial({ dia: nuevoDia, hora: nuevaHora });
    onChange?.({ target: { value: nuevoDia && nuevaHora ? `${nuevoDia}T${nuevaHora}` : '', name } });
  };
  const descripcion = describirCon(campoId, error, ayuda);

  return (
    <fieldset>
      <legend className="block text-sm font-medium text-slate-700 mb-1">{label}</legend>
      <div className="grid grid-cols-[minmax(0,1fr)_8.5rem] gap-2">
        <CampoFecha
          label={`${label}: el día`}
          etiquetaOculta
          id={`${campoId}-dia`}
          value={dia}
          min={min?.slice(0, 10)}
          required={required}
          disabled={disabled}
          invalido={Boolean(error)}
          descritoPor={descripcion}
          onChange={(e) => cambiar(e.target.value, hora)}
        />
        <CampoHora
          label={`${label}: la hora`}
          etiquetaOculta
          id={`${campoId}-hora`}
          value={hora}
          required={required}
          disabled={disabled}
          invalido={Boolean(error)}
          descritoPor={descripcion}
          onChange={(nueva) => cambiar(dia, nueva)}
        />
      </div>
      {error ? (
        <p id={`${campoId}-error`} className="mt-1 flex items-start gap-1 text-sm text-red-700">
          <CircleAlert size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
          {error}
        </p>
      ) : ayuda && <p id={`${campoId}-ayuda`} className="mt-1 text-sm text-slate-600">{ayuda}</p>}
    </fieldset>
  );
}

const SIN_FILTRO = '__todos';

/**
 * Lista desplegable. opciones: [{ valor, texto }]. Con "todos" agrega una primera opción que vale '' (para filtros:
 * "Todos los roles"); Radix no acepta '' como valor de una opción, por eso se traduce aquí.
 */
export function Selector({ label, value, onChange, opciones, placeholder = 'Elegir…', todos, ayuda, error, id, disabled }) {
  const generado = useId();
  const campoId = id ?? generado;
  // Sin elegir, el valor es '' y no undefined: Radix muestra el placeholder igual, y React no avisa que el Select
  // pasa de no controlado a controlado al elegir la primera opción.
  const valor = value === '' && todos ? SIN_FILTRO : (value ?? '');
  return (
    <Campo id={campoId} label={label} ayuda={ayuda} error={error}>
      <Select value={valor} onValueChange={(v) => onChange(v === SIN_FILTRO ? '' : v)} disabled={disabled}>
        <SelectTrigger
          id={campoId}
          className={`w-full ${error ? 'border-red-600' : ''}`}
          aria-invalid={error ? true : undefined}
          aria-describedby={describirCon(campoId, error, ayuda)}
        >
          <SelectValue placeholder={placeholder} />
        </SelectTrigger>
        <SelectContent>
          {todos && <SelectItem value={SIN_FILTRO}>{todos}</SelectItem>}
          {opciones.map((o) => <SelectItem key={o.valor} value={o.valor}>{o.texto}</SelectItem>)}
        </SelectContent>
      </Select>
    </Campo>
  );
}

/**
 * Selector con búsqueda (el combobox de shadcn/ui), para elegir entre muchas opciones, como un cliente o un producto.
 * Para pocas opciones fijas, como los filtros de una lista, se usa Selector.
 * - opciones: [{ valor, texto, detalle? }]. "detalle" se muestra debajo del texto y también se busca en él.
 * - onChange(valor, opcion): recibe además la opción entera, por ejemplo para proponer la dirección de un cliente.
 * - Sin onBuscar, filtra aquí al escribir, sin distinguir mayúsculas ni tildes.
 * - Con onBuscar(texto), la búsqueda es de quien lo usa (la API): "opciones" trae los resultados, "cargando" dice si
 *   está buscando y "limite" avisa que hay más cuando llegan tantas como pidió.
 * - textoInicial: el texto del valor elegido mientras todavía no está entre las opciones (al editar).
 */
export function Combobox({
  label, value, onChange, opciones, placeholder = 'Elegir…', placeholderBusqueda = 'Buscar…', ayuda, error, id, disabled,
  onBuscar, cargando = false, errorBusqueda, limite, textoInicial,
}) {
  const generado = useId();
  const campoId = id ?? generado;
  const listaId = `${campoId}-opciones`;
  const [abierto, setAbierto] = useState(false);
  const [busqueda, setBusqueda] = useState('');
  // La última opción elegida: su texto se sigue mostrando aunque una búsqueda nueva ya no la traiga.
  const [elegida, setElegida] = useState(null);

  const seleccionada = opciones.find((o) => String(o.valor) === String(value))
    ?? (elegida && String(elegida.valor) === String(value) ? elegida : null);
  const textoElegido = seleccionada?.texto ?? (value ? textoInicial : '');
  const visibles = onBuscar ? opciones : opciones.filter((o) => coincide(busqueda, o.texto, o.detalle));
  const hayMas = Boolean(onBuscar && limite && opciones.length >= limite);

  let anuncio = visibles.length === 1 ? '1 resultado' : `${visibles.length} resultados`;
  if (cargando) anuncio = 'Buscando…';
  else if (errorBusqueda) anuncio = errorBusqueda;
  else if (visibles.length === 0) anuncio = 'Sin resultados';

  const cambiarAbierto = (abre) => {
    setAbierto(abre);
    setBusqueda('');
    if (abre) onBuscar?.('');
  };

  const buscar = (texto) => {
    setBusqueda(texto);
    onBuscar?.(texto);
  };

  const elegir = (opcion) => {
    setElegida(opcion);
    onChange(opcion.valor, opcion);
    setAbierto(false);
  };

  return (
    <Campo id={campoId} label={label} ayuda={ayuda} error={error}>
      {/* modal: dentro de un Modal, la rueda del mouse desplaza la lista y no la página. */}
      <Popover open={abierto} onOpenChange={cambiarAbierto} modal>
        <PopoverTrigger asChild>
          <button
            type="button"
            id={campoId}
            role="combobox"
            aria-expanded={abierto}
            aria-controls={abierto ? listaId : undefined}
            aria-invalid={error ? true : undefined}
            aria-describedby={describirCon(campoId, error, ayuda)}
            disabled={disabled}
            className={`flex h-10 w-full items-center justify-between gap-2 rounded-lg border bg-white px-3 text-left text-sm transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-marca-500 disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-600 ${
              error ? 'border-red-600' : 'border-borde-control'
            }`}
          >
            <span className={`min-w-0 truncate ${textoElegido ? 'text-slate-900' : 'text-slate-500'}`}>{textoElegido || placeholder}</span>
            <ChevronsUpDown size={16} className="shrink-0 text-slate-600" aria-hidden="true" />
          </button>
        </PopoverTrigger>
        <PopoverContent align="start" collisionPadding={16} className="w-(--radix-popover-trigger-width) min-w-56 p-0">
          <Command shouldFilter={false} label={label}>
            <CommandInput value={busqueda} onValueChange={buscar} placeholder={placeholderBusqueda} aria-label={`Buscar: ${label}`} />
            <CommandList id={listaId}>
              {!cargando && !errorBusqueda && <CommandEmpty>Sin resultados.</CommandEmpty>}
              {visibles.length > 0 && (
                <CommandGroup>
                  {visibles.map((o) => (
                    <CommandItem key={o.valor} value={String(o.valor)} onSelect={() => elegir(o)}>
                      <Check size={16} className={String(o.valor) === String(value) ? 'text-marca-700' : 'invisible'} aria-hidden="true" />
                      <span className="min-w-0">
                        <span className="block truncate">{o.texto}</span>
                        {o.detalle && <span className="block truncate text-xs text-slate-600">{o.detalle}</span>}
                      </span>
                    </CommandItem>
                  ))}
                </CommandGroup>
              )}
            </CommandList>
            {(cargando || errorBusqueda || hayMas) && (
              <p className={`border-t border-border px-3 py-2 text-xs ${errorBusqueda && !cargando ? 'text-red-700' : 'text-slate-600'}`}>
                {cargando && 'Buscando…'}
                {!cargando && errorBusqueda}
                {!cargando && !errorBusqueda && hayMas && 'Hay más: escribe para afinar la búsqueda.'}
              </p>
            )}
            <p className="sr-only" aria-live="polite">{anuncio}</p>
          </Command>
        </PopoverContent>
      </Popover>
    </Campo>
  );
}

export function Casilla({ checked, onCheckedChange, disabled, children, id }) {
  const generado = useId();
  const casillaId = id ?? generado;
  return (
    <div className="flex items-start gap-2.5">
      <Checkbox id={casillaId} checked={checked} onCheckedChange={onCheckedChange} disabled={disabled} className="mt-0.5" />
      <label htmlFor={casillaId} className={`text-sm leading-snug ${disabled ? 'text-slate-600' : 'text-slate-900 cursor-pointer'}`}>
        {children}
      </label>
    </div>
  );
}

const anchos = { md: 'sm:max-w-lg', lg: 'sm:max-w-3xl' };

/**
 * Ventana modal: atrapa el foco, se cierra con Escape o con la X, y devuelve el foco al botón que la abrió. Un clic
 * fuera no la cierra: perdería lo que se escribió en un formulario.
 */
export function Modal({ abierto, onCerrar, titulo, descripcion, children, pie, ancho = 'md' }) {
  // Radix devuelve el foco solo a un DialogTrigger, y las ventanas del panel se abren con su estado: sin esto, al cerrar
  // el foco caía en el body. Se guarda quién lo tenía al abrir y se le devuelve, si sigue en la página (WCAG 2.4.3).
  const abrio = useRef(null);
  return (
    <Dialog open={abierto} onOpenChange={(abre) => { if (!abre) onCerrar(); }}>
      <DialogContent
        className={`${anchos[ancho]} max-h-[90vh] grid-rows-[auto_minmax(0,1fr)_auto]`}
        onOpenAutoFocus={() => { abrio.current = document.activeElement; }}
        onCloseAutoFocus={(e) => {
          if (!abrio.current?.isConnected) return;
          e.preventDefault();
          abrio.current.focus();
        }}
        onPointerDownOutside={(e) => e.preventDefault()}
        // Sin descripción, Radix pide aria-describedby={undefined}: así el lector de pantalla no lee el título dos veces.
        {...(descripcion ? {} : { 'aria-describedby': undefined })}
      >
        <DialogHeader>
          <DialogTitle>{titulo}</DialogTitle>
          {descripcion && <DialogDescription>{descripcion}</DialogDescription>}
        </DialogHeader>
        <div className="min-h-0 overflow-y-auto -mx-6 px-6">{children}</div>
        {pie && <DialogFooter>{pie}</DialogFooter>}
      </DialogContent>
    </Dialog>
  );
}

/**
 * Pide confirmación antes de desactivar, anular o eliminar (docs/diseno.md, Mensajes). Con variante="primary" sirve
 * para una acción que no destruye pero tampoco se deshace, como confirmar una venta. Con ancho="lg" entra un mapa.
 */
export function Confirmar({
  abierto, titulo, children, textoAccion, enviando, error, onConfirmar, onCancelar, variante = 'danger', ancho = 'md',
}) {
  return (
    <Modal
      abierto={abierto}
      onCerrar={onCancelar}
      titulo={titulo}
      ancho={ancho}
      pie={(
        <>
          <Btn variant="outline" size="md" onClick={onCancelar} disabled={enviando}>Cancelar</Btn>
          <Btn variant={variante} size="md" onClick={onConfirmar} disabled={enviando}>
            {enviando && <LoaderCircle size={16} className="animate-spin" aria-hidden="true" />}
            {textoAccion}
          </Btn>
        </>
      )}
    >
      <div className="space-y-3 text-sm text-slate-700">
        {children}
        {error && <Alert type="error">{error}</Alert>}
      </div>
    </Modal>
  );
}

export function Buscador({ value, onChange, placeholder = 'Buscar…', label = 'Buscar' }) {
  const id = useId();
  return (
    <div className="w-full sm:w-72">
      <label htmlFor={id} className="block text-sm font-medium text-slate-700 mb-1">{label}</label>
      <div className="relative">
        <Search size={16} className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-600 pointer-events-none" aria-hidden="true" />
        <input
          id={id}
          type="search"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          className="w-full h-10 pl-9 pr-3 border border-borde-control rounded-lg text-sm text-slate-900 placeholder:text-slate-500 bg-white transition focus:outline-none focus:ring-2 focus:ring-marca-500 focus:border-marca-500"
        />
      </div>
    </div>
  );
}

/**
 * Barra de búsqueda y filtros, dentro de la tarjeta de la tabla. Los filtros se aplican al cambiar, sin botón
 * "Filtrar"; "Limpiar filtros" aparece cuando hay alguno activo.
 */
export function BarraDeFiltros({ children, resultado, hayFiltros, onLimpiar }) {
  return (
    <div className="flex flex-col gap-3 p-4 border-b border-slate-200 lg:flex-row lg:items-end lg:justify-between">
      <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-end">{children}</div>
      <div className="flex shrink-0 items-center gap-3 text-sm text-slate-600 lg:h-10">
        <span aria-live="polite" className="whitespace-nowrap">{resultado}</span>
        {hayFiltros && (
          <button
            type="button"
            onClick={onLimpiar}
            className={`inline-flex items-center gap-1 rounded-md font-medium text-marca-700 hover:text-marca-800 hover:underline cursor-pointer ${foco}`}
          >
            <X size={14} aria-hidden="true" />
            Limpiar filtros
          </button>
        )}
      </div>
    </div>
  );
}

/** Página anterior y siguiente de una lista paginada en la API. Mientras la lista recarga, quedan deshabilitadas. */
export function Paginacion({ pagina, totalPaginas, onCambiar, deshabilitada = false }) {
  if (totalPaginas <= 1) return null;
  return (
    <nav aria-label="Paginación" className="flex items-center justify-between gap-3 px-4 py-3 border-t border-slate-200 text-sm text-slate-600">
      <span>Página {pagina} de {totalPaginas}</span>
      <div className="flex gap-2">
        <Btn variant="outline" onClick={() => onCambiar(pagina - 1)} disabled={deshabilitada || pagina <= 1}>
          <ChevronLeft size={14} aria-hidden="true" />
          Anterior
        </Btn>
        <Btn variant="outline" onClick={() => onCambiar(pagina + 1)} disabled={deshabilitada || pagina >= totalPaginas}>
          Siguiente
          <ChevronRight size={14} aria-hidden="true" />
        </Btn>
      </div>
    </nav>
  );
}

/** Un número que resume la pantalla, como lo que hay por cobrar. */
export function Indicador({ titulo, valor, detalle, icono: Icono }) {
  return (
    <div className="bg-white rounded-xl shadow-tarjeta border border-slate-200 p-5 min-w-0">
      <p className="flex items-center gap-2 text-sm font-medium text-slate-600">
        {Icono && <Icono size={16} aria-hidden="true" />}
        {titulo}
      </p>
      <p className="mt-1 text-2xl font-semibold text-slate-900 tabular-nums truncate">{valor}</p>
      {detalle && <p className="mt-1 text-sm text-slate-600">{detalle}</p>}
    </div>
  );
}

export function EstadoVacio({ titulo, children, accion, icono: Icono = Inbox }) {
  return (
    <div className="flex flex-col items-center text-center py-14 px-4">
      <Icono size={32} className="text-slate-400 mb-3" aria-hidden="true" />
      <p className="text-base font-semibold text-slate-900">{titulo}</p>
      {children && <p className="mt-1 max-w-sm text-sm text-slate-600">{children}</p>}
      {accion && <div className="mt-4">{accion}</div>}
    </div>
  );
}

export function ErrorDeCarga({ mensaje, onReintentar, reintentando }) {
  return (
    <div className="p-4">
      <Alert type="error">
        <p>{mensaje}</p>
        <button
          type="button"
          onClick={onReintentar}
          disabled={reintentando}
          className={`mt-2 inline-flex items-center gap-1.5 font-medium underline cursor-pointer disabled:opacity-50 ${foco}`}
        >
          <RefreshCw size={14} className={reintentando ? 'animate-spin' : ''} aria-hidden="true" />
          Reintentar
        </button>
      </Alert>
    </div>
  );
}
