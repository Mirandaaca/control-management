import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../data/services/ubicacion.dart';
import '../formato.dart';
import '../tema.dart';
import '../widgets/aviso.dart';
import 'captura_de_ubicacion.dart';

/// La tarjeta de ubicación de la venta y del cliente (8.6.5, 8.6.6 y 8.6.12): dice si busca, qué capturó y con qué
/// precisión, o qué falta para capturarla. Sin ubicación no se guarda ni la venta ni el cliente.
class TarjetaDeUbicacion extends ConsumerStatefulWidget {
  const TarjetaDeUbicacion({super.key, required this.registro, this.alEditar = false});

  final QueSeRegistra registro;

  /// Al editar un cliente, la ubicación se toma de nuevo solo en su negocio (7.3.8).
  final bool alEditar;

  @override
  ConsumerState<TarjetaDeUbicacion> createState() => _TarjetaDeUbicacionState();
}

class _TarjetaDeUbicacionState extends ConsumerState<TarjetaDeUbicacion> {
  late final AppLifecycleListener _ciclo;

  CapturaDeUbicacion get _captura => ref.read(capturaDeUbicacionProvider(widget.registro).notifier);

  @override
  void initState() {
    super.initState();
    // Al volver de los ajustes ("Activar GPS", "Dar permiso"), vuelve a buscar sin que el promotor tenga que tocar nada.
    _ciclo = AppLifecycleListener(onResume: () => _captura.alVolverALaApp());
  }

  @override
  void dispose() {
    _ciclo.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(capturaDeUbicacionProvider(widget.registro));
    final queSeRegistra = widget.registro.nombre;
    final volverACapturar = AccionDeAviso('Volver a capturar', _captura.capturar);
    final aproximada = Aviso(
      'Dale permiso a la ubicación precisa: la aproximada se equivoca por cuadras.',
      tipo: TipoDeAviso.aviso,
      titulo: 'La app solo tiene tu ubicación aproximada.',
      accion: AccionDeAviso('Dar permiso', _captura.darPermiso),
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ...switch (estado) {
          UbicacionBuscando(soloAproximada: true) => [
            aproximada,
            const SizedBox(height: 12),
            const Aviso('', tipo: TipoDeAviso.informacion, titulo: 'Buscando la señal del GPS…', buscando: true),
          ],
          UbicacionSinIniciar() || UbicacionBuscando() => [
            const Aviso('', tipo: TipoDeAviso.informacion, titulo: 'Buscando la señal del GPS…', buscando: true),
          ],
          UbicacionCapturada(precisa: true, :final lectura) => [
            Aviso(
              _coordenadas(lectura),
              tipo: TipoDeAviso.informacion,
              icono: Icons.location_on,
              titulo: 'Ubicación capturada · ${_precision(lectura)}',
              accion: volverACapturar,
            ),
          ],
          UbicacionCapturada(:final lectura, :final soloAproximada) => [
            if (soloAproximada) ...[aproximada, const SizedBox(height: 12)],
            Aviso(
              _coordenadas(lectura),
              tipo: TipoDeAviso.aviso,
              icono: Icons.location_on,
              titulo: 'Ubicación poco precisa · ${_precision(lectura)}',
              accion: volverACapturar,
            ),
          ],
          UbicacionGuardada(:final lectura) => [
            Aviso(
              _coordenadas(lectura),
              tipo: TipoDeAviso.informacion,
              icono: Icons.location_on,
              titulo: 'Ubicación guardada',
              accion: volverACapturar,
            ),
          ],
          SinUbicacionGuardada() => [
            Aviso(
              'Sin ubicación, el cliente no se guarda.',
              tipo: TipoDeAviso.aviso,
              icono: Icons.location_off_outlined,
              titulo: 'Este cliente no tiene ubicación.',
              accion: AccionDeAviso('Capturar ubicación', _captura.capturar),
            ),
          ],
          GpsDesactivado() => [
            Aviso(
              'Actívalo para registrar $queSeRegistra: sin ubicación, $queSeRegistra no se guarda.',
              tipo: TipoDeAviso.aviso,
              titulo: 'El GPS está desactivado.',
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: _captura.activarGps,
              icon: const Icon(Icons.my_location),
              label: const Text('Activar GPS'),
            ),
          ],
          UbicacionSinPermiso() => [
            Aviso(
              'Dale permiso para registrar $queSeRegistra: sin ubicación, $queSeRegistra no se guarda.',
              tipo: TipoDeAviso.aviso,
              titulo: 'La app no tiene permiso para usar tu ubicación.',
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: _captura.darPermiso,
              icon: const Icon(Icons.location_on_outlined),
              label: const Text('Dar permiso'),
            ),
          ],
          SinUbicacionPrecisa() => [
            Aviso(
              'Dale permiso a la ubicación precisa para registrar $queSeRegistra: sin ubicación, $queSeRegistra no se '
              'guarda.',
              tipo: TipoDeAviso.aviso,
              titulo: 'La app solo tiene tu ubicación aproximada.',
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: _captura.darPermiso,
              icon: const Icon(Icons.location_on_outlined),
              label: const Text('Dar permiso'),
            ),
          ],
          SinSenalDelGps() => [
            Aviso(
              'Busca un lugar abierto y vuelve a intentarlo: sin ubicación, $queSeRegistra no se guarda.',
              tipo: TipoDeAviso.aviso,
              titulo: 'No hay señal del GPS.',
            ),
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: _captura.capturar,
              icon: const Icon(Icons.refresh),
              label: const Text('Reintentar'),
            ),
          ],
        },
        if (widget.alEditar && (estado is UbicacionGuardada || estado is UbicacionCapturada)) ...[
          const SizedBox(height: 4),
          const Text(
            'Captúrala de nuevo solo si estás en el negocio del cliente.',
            style: TextStyle(color: textoSecundario, fontSize: 14, height: 20 / 14),
          ),
        ],
      ],
    );
  }
}

String _coordenadas(Posicion p) => 'Lat. ${formatearCoordenada(p.latitud)} · Long. ${formatearCoordenada(p.longitud)}';

String _precision(Posicion p) => '±${formatearCantidad((p.precision ?? 0).round())} m';
