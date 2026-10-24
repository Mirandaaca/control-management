import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Printer } from 'lucide-react';
import api from '../api/axios';
import { mensajeDeError } from '../api/errores';
import EncabezadoDeLaEmpresa from '../components/hojas/EncabezadoDeLaEmpresa';
import { Btn, Cargando, ErrorDeCarga } from '../components/ui';
import { formatearCantidad, formatearFecha, formatearMonto } from '../utils/formato';

const ESTADOS = { 0: 'Borrador', 1: 'Confirmada', 2: 'Anulada' };
const ANULADA = 2;
const CREDITO = 1;

function Dato({ titulo, children, ancho = false }) {
  return (
    <div className={ancho ? 'col-span-2' : ''}>
      <dt className="inline text-slate-600">{titulo}: </dt>
      <dd className="inline font-medium text-slate-900">{children}</dd>
    </div>
  );
}

function Firma({ titulo }) {
  return (
    <div className="flex-1">
      <div className="h-14 border-b border-slate-400" />
      <p className="mt-1 text-sm font-medium text-slate-900">{titulo}</p>
      <p className="mt-3 text-sm text-slate-600">Nombre: <span className="inline-block w-48 border-b border-dotted border-slate-400" /></p>
    </div>
  );
}

/**
 * La HP para imprimir o guardar en PDF (6.5, 7.3.26), con la forma de la hoja de papel de la Figura 1. Se abre en una
 * pestaña nueva, sin el menú; el navegador la imprime o la guarda en PDF. Una HP anulada sale marcada (ext. 2.2a).
 */
export default function HojaParaImprimir() {
  const { id } = useParams();
  const [hoja, setHoja] = useState(null);
  const [error, setError] = useState('');
  const [reintentando, setReintentando] = useState(false);

  const cargar = useCallback(() => api.get(`/hojas-pedido/${id}/impresion`)
    .then(({ data }) => { setHoja(data); setError(''); })
    .catch((err) => setError(mensajeDeError(err, 'No se pudo cargar la hoja de pedido.')))
    .finally(() => setReintentando(false)), [id]);

  useEffect(() => { cargar(); }, [cargar]);

  // El nombre que el navegador propone al guardar el PDF.
  useEffect(() => {
    if (hoja) document.title = `${hoja.numero} · Hoja de pedido`;
  }, [hoja]);

  if (error) return (
    <main className="mx-auto max-w-3xl p-4">
      <ErrorDeCarga mensaje={error} onReintentar={() => { setReintentando(true); cargar(); }} reintentando={reintentando} />
    </main>
  );
  if (!hoja) return <Cargando texto="Preparando la hoja de pedido…" />;

  const anulada = hoja.estadoHP === ANULADA;
  return (
    <main className="min-h-screen bg-slate-100 px-4 py-6 print:bg-white print:p-0">
      <div className="mx-auto mb-4 flex max-w-3xl flex-col gap-2 sm:flex-row sm:items-center sm:justify-between print:hidden">
        <p className="text-sm text-slate-600">En el diálogo de impresión, elige "Guardar como PDF" para guardarla.</p>
        <Btn size="md" onClick={() => window.print()}>
          <Printer size={16} aria-hidden="true" />
          Imprimir o guardar en PDF
        </Btn>
      </div>

      <article className="relative mx-auto max-w-3xl overflow-hidden rounded-xl bg-white p-6 shadow-tarjeta sm:p-10 print:max-w-none print:rounded-none print:p-0 print:shadow-none">
        {anulada && (
          <p aria-hidden="true"
            className="pointer-events-none absolute inset-0 flex select-none items-center justify-center text-8xl font-black tracking-widest text-slate-200 -rotate-30">
            ANULADA
          </p>
        )}

        <div className="relative">
          <header className="flex flex-col gap-3 border-b-2 border-slate-800 pb-4 sm:flex-row sm:items-start sm:justify-between">
            <EncabezadoDeLaEmpresa />
            <p className="text-right font-mono text-lg font-semibold text-slate-900" translate="no">Nº {hoja.numero}</p>
          </header>

          {anulada && (
            <p className="mt-4 rounded-lg border-2 border-slate-800 px-3 py-2 text-sm font-semibold text-slate-900">
              ANULADA el {formatearFecha(hoja.fechaAnulacion, { hora: true })}
              {hoja.anuladoPor && ` por ${hoja.anuladoPor.nombreCompleto}`}: {hoja.motivoAnulacion}.
            </p>
          )}

          <dl className="mt-4 grid grid-cols-1 gap-x-6 gap-y-1.5 rounded-lg border border-slate-300 p-4 text-sm sm:grid-cols-2">
            <Dato titulo="Cliente">{hoja.cliente.nombreCompleto} <span className="font-mono text-xs" translate="no">({hoja.cliente.codigo})</span></Dato>
            <Dato titulo="Fecha">{formatearFecha(hoja.fechaRegistro, { hora: true })}</Dato>
            {hoja.cliente.razonSocial && <Dato titulo="Razón social">{hoja.cliente.razonSocial}</Dato>}
            {hoja.cliente.nit && <Dato titulo="NIT">{hoja.cliente.nit}</Dato>}
            <Dato titulo="Dirección">{hoja.cliente.direccion}</Dato>
            <Dato titulo="Celular">{hoja.cliente.celular}</Dato>
            <Dato titulo="Promotor">{hoja.promotor.nombreCompleto}</Dato>
            <Dato titulo="Zona">{hoja.zona.nombre}</Dato>
            <Dato titulo="Estado">{ESTADOS[hoja.estadoHP] ?? '—'}</Dato>
          </dl>

          <table className="mt-5 w-full text-sm">
            <thead>
              <tr className="border-y-2 border-slate-800 text-left text-xs uppercase tracking-wide text-slate-700">
                <th scope="col" className="py-2 pr-2">Producto</th>
                <th scope="col" className="py-2 px-2 text-right">Cantidad</th>
                <th scope="col" className="py-2 px-2 text-right">Precio u.</th>
                <th scope="col" className="py-2 px-2 text-right">Descuento</th>
                <th scope="col" className="py-2 pl-2 text-right">Subtotal</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-300">
              {hoja.detalles.map((d) => (
                <tr key={d.id}>
                  <td className="py-2 pr-2 text-slate-900">{d.descripcion}</td>
                  <td className="py-2 px-2 text-right tabular-nums">{formatearCantidad(d.cantidad)}</td>
                  <td className="py-2 px-2 text-right tabular-nums whitespace-nowrap">{formatearMonto(d.precioUnitario)}</td>
                  <td className="py-2 px-2 text-right tabular-nums whitespace-nowrap">{d.descuento > 0 ? `${formatearMonto(d.descuento)} c/u` : '—'}</td>
                  <td className="py-2 pl-2 text-right tabular-nums whitespace-nowrap">{formatearMonto(d.subtotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <dl className="ml-auto mt-3 w-full max-w-64 space-y-1 border-t-2 border-slate-800 pt-2 text-sm">
            <div className="flex justify-between"><dt className="text-slate-600">Subtotal</dt><dd className="tabular-nums">{formatearMonto(hoja.subTotal)}</dd></div>
            {hoja.descuento > 0 && (
              <div className="flex justify-between"><dt className="text-slate-600">Descuento</dt><dd className="tabular-nums">− {formatearMonto(hoja.descuento)}</dd></div>
            )}
            <div className="flex justify-between text-base font-bold"><dt>Total</dt><dd className="tabular-nums">{formatearMonto(hoja.total)}</dd></div>
          </dl>

          <p className="mt-4 text-sm text-slate-900">
            {hoja.tipoVenta === CREDITO
              // Anulada, su deuda también se anuló: ya no vence.
              ? <>Crédito{hoja.cobranza && `: ${hoja.cobranza.numero}`}{hoja.cobranza && !anulada && `, vence el ${formatearFecha(hoja.cobranza.fechaVencimiento)}`}</>
              : 'Tipo de venta: Contado'}
          </p>

          <div className="mt-12 flex flex-col gap-10 sm:flex-row print:flex-row">
            <Firma titulo="Solicitado" />
            <Firma titulo="Recibido" />
          </div>
        </div>
      </article>
    </main>
  );
}
