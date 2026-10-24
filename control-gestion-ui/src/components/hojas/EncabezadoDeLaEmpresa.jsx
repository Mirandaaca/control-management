import { EMPRESA } from './empresa';

/** El encabezado de la HP impresa, como el de la hoja de papel (Figura 1 de la tesis). */
export default function EncabezadoDeLaEmpresa() {
  return (
    <div className="flex items-center gap-3">
      {EMPRESA.logo && <img src={EMPRESA.logo} alt="" className="h-14 w-auto" />}
      <div>
        <p className="text-lg font-bold text-slate-900">{EMPRESA.nombre}</p>
        <p className="text-sm font-semibold uppercase tracking-wide text-slate-700">Hoja de pedido</p>
        {EMPRESA.datos.map((linea) => <p key={linea} className="text-xs text-slate-600">{linea}</p>)}
      </div>
    </div>
  );
}
