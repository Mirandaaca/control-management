import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/evento.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';
import '../core/ubicacion/mapa_de_ubicacion.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/mensaje_de_exito.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/confirmar.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/visor_de_foto.dart';
import '../sesion/sesion_notifier.dart';
import 'etiqueta_de_evento.dart';
import 'evento_view_model.dart';
import 'fotos_del_evento.dart';

/// Cómo se abre el detalle de un evento: con el aviso de lo que pasó.
class AbrirEvento {
  const AbrirEvento({this.mensaje});

  final String? mensaje;
}

/// Lo que el usuario puede hacer con un evento, como `accionesDelEvento` del panel: el permiso, y además que esté
/// pendiente. Modificar y administrar las fotos, solo en los que registró, salvo con cartera completa (7.3.13, ext. 3a y
/// 3b; 7.3.14, ext. *a, 2b y 4.1a). La API lo exige igual.
({bool modificar, bool agregarFoto, bool eliminarFoto}) accionesDelEvento(Evento evento, EstadoDeSesion sesion) {
  if (sesion is! ConSesion || evento.estado != EstadoEvento.pendiente) {
    return (modificar: false, agregarFoto: false, eliminarFoto: false);
  }
  final yo = sesion.sesion;
  final suyo = yo.tiene([Permisos.seguridad.verCarteraCompleta]) || evento.promotor.id == yo.id;
  return (
    modificar: suyo && yo.tiene([Permisos.eventos.modificarEvento]),
    agregarFoto: suyo && yo.tiene([Permisos.eventos.agregarFoto]),
    eliminarFoto: suyo && yo.tiene([Permisos.eventos.eliminarFoto]),
  );
}

/// Un evento (4.1 a 4.3; `frmEventoMovil`): su tipo, su cliente o su pedido, qué pasó, dónde (7.3.15) y su estado; sus
/// fotos (7.3.14), y si ya se resolvió, la solución.
class EventoScreen extends ConsumerStatefulWidget {
  const EventoScreen({super.key, required this.id, this.abrir});

  final int id;
  final AbrirEvento? abrir;

  @override
  ConsumerState<EventoScreen> createState() => _EventoScreenState();
}

class _EventoScreenState extends ConsumerState<EventoScreen> {
  EventoViewModel get _viewModel => ref.read(eventoProvider(widget.id).notifier);

  @override
  void initState() {
    super.initState();
    final mensaje = widget.abrir?.mensaje;
    if (mensaje != null) Future.microtask(() => _viewModel.avisar(mensaje));
  }

  Future<void> _editar(Evento evento) async {
    final mensaje = await context.push<String>(Rutas.editarEvento(evento.id));
    if (mensaje == null || !mounted) return;
    await _viewModel.cargar();
    _viewModel.avisar(mensaje);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(eventoProvider(widget.id));
    ref.listen(eventoProvider(widget.id).select((e) => e.mensaje), (anterior, mensaje) {
      if (mensaje != null && mensaje != anterior) avisarExito(context, mensaje);
    });
    // La ubicación de una foto que se agrega aquí: vive mientras la pantalla está abierta.
    ref.watch(capturaDeUbicacionProvider(QueSeRegistra.foto));
    final sesion = ref.watch(sesionProvider);
    final evento = estado.evento;
    final acciones = evento == null ? null : accionesDelEvento(evento, sesion);

    return Scaffold(
      appBar: AppBar(title: Text(evento?.numero ?? 'Evento')),
      body: switch (estado) {
        DetalleDeEvento(evento: final evento?) => RefreshIndicator(
          onRefresh: _viewModel.cargar,
          child: _Contenido(evento: evento, estado: estado, acciones: acciones!, viewModel: _viewModel),
        ),
        DetalleDeEvento(error: final error?) => ErrorConReintento.de(
          error,
          reintentando: estado.reintentando,
          alReintentar: _viewModel.cargar,
        ),
        _ => const Cargando('Cargando el evento…'),
      },
      bottomNavigationBar: evento != null && acciones!.modificar
          ? BarraDeAcciones(principal: Accion('Editar', () => _editar(evento)))
          : null,
    );
  }
}

class _Contenido extends StatelessWidget {
  const _Contenido({required this.evento, required this.estado, required this.acciones, required this.viewModel});

  final Evento evento;
  final DetalleDeEvento estado;
  final ({bool modificar, bool agregarFoto, bool eliminarFoto}) acciones;
  final EventoViewModel viewModel;

  Future<void> _eliminar(BuildContext context, FotoDeEvento foto) async {
    // El texto del 8.6.12: qué va a pasar, "Cancelar" y la acción en rojo.
    final confirmado = await confirmar(
      context,
      titulo: '¿Eliminar la foto?',
      contenido: const Text('Se borra del evento y del servidor: no se puede deshacer.'),
      accion: 'Eliminar foto',
      peligro: true,
    );
    if (confirmado) await viewModel.eliminarFoto(foto.id);
  }

  void _ver(BuildContext context, FotoDeEvento foto, int numero) {
    final subida = 'Subida por ${foto.subidoPor} el ${formatearFecha(foto.fechaSubida, hora: true)}.';
    verFoto(
      context,
      titulo: 'Foto $numero',
      etiqueta: 'Foto $numero de ${evento.numero}',
      foto: fotoDelEventoProvider(foto.id),
      pie: foto.fechaCaptura == null ? subida : '$subida Tomada el ${formatearFecha(foto.fechaCaptura, hora: true)}.',
    );
  }

  @override
  Widget build(BuildContext context) {
    final titulo = Theme.of(context).textTheme.titleMedium;
    final fotos = evento.fotos;
    final ocupado = estado.subiendo || estado.eliminando != null;
    final hayLugar = fotos.length < maximoDeFotos;
    final pendiente = evento.estado == EstadoEvento.pendiente;

    return ListView(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
      children: [
        Wrap(
          spacing: 12,
          runSpacing: 4,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Semantics(header: true, container: true, child: Text(evento.numero, style: titulo)),
            EtiquetaDeEvento(evento.estado),
          ],
        ),
        const SizedBox(height: 12),
        _Dato('Tipo de evento', evento.tipo.texto),
        if (evento.cliente != null) _Dato('Cliente', evento.cliente!.nombre),
        if (evento.pedido != null) _Dato('Pedido', evento.pedido!.numero),
        _Dato('Registrado', '${formatearFecha(evento.fecha, hora: true)} por ${evento.promotor.nombre}'),
        _Dato('Qué pasó', evento.descripcion),
        if (evento.estado == EstadoEvento.resuelto) ...[
          _Dato('Solución', evento.solucion ?? '—'),
          _Dato('Resolución', '${formatearFecha(evento.fechaResolucion, hora: true)} por ${evento.resueltoPor ?? '—'}'),
        ],
        // 7.3.15: dónde se registró.
        MapaDeUbicacion(latitud: evento.latitud, longitud: evento.longitud),
        const SizedBox(height: 20),
        // 7.3.14, pasos 1 a 1.2: las fotos en miniatura, con su fecha.
        TituloDeFotos(titulo: 'Fotos', cantidad: fotos.length),
        if (pendiente && evento.tipo.requiereEvidencia && fotos.isEmpty) ...[
          const SizedBox(height: 4),
          const Text(
            'Este tipo se resuelve con al menos una foto: agrégala ahora o después.',
            style: TextStyle(color: textoSecundario),
          ),
        ],
        const SizedBox(height: 8),
        if (fotos.isEmpty)
          const Text('Todavía no tiene fotos.', style: TextStyle(color: textoSecundario))
        else
          GrillaDeFotos(
            fotos: [
              for (final (i, foto) in fotos.indexed)
                MiniaturaDeFoto(
                  imagen: estado.eliminando == foto.id
                      ? const Center(child: CircularProgressIndicator())
                      : ImagenDeFotoSubida(foto.id),
                  fecha: formatearFecha(foto.fechaCaptura ?? foto.fechaSubida, hora: true),
                  etiqueta: 'Foto ${i + 1}',
                  alTocar: () => _ver(context, foto, i + 1),
                  textoQuitar: 'Eliminar la foto ${i + 1}',
                  alQuitar: acciones.eliminarFoto && !ocupado ? () => _eliminar(context, foto) : null,
                ),
            ],
          ),
        if (estado.errorDeFoto != null) ...[const SizedBox(height: 12), Aviso.deError(estado.errorDeFoto!)],
        if (acciones.agregarFoto) ...[
          const SizedBox(height: 12),
          if (estado.subiendo)
            Semantics(
              liveRegion: true,
              child: const Row(
                children: [
                  SizedBox.square(dimension: 18, child: CircularProgressIndicator(strokeWidth: 2)),
                  SizedBox(width: 12),
                  Text('Subiendo la foto…'),
                ],
              ),
            )
          else if (hayLugar)
            BotonesDeFoto(
              alTomar: ocupado ? null : viewModel.tomarFoto,
              alElegir: ocupado ? null : viewModel.elegirDeLaGaleria,
            )
          else
            const Text('Ya tiene 5 fotos: elimina una para agregar otra.', style: TextStyle(color: textoSecundario)),
        ],
      ],
    );
  }
}

class _Dato extends StatelessWidget {
  const _Dato(this.titulo, this.valor);

  final String titulo;
  final String valor;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: MergeSemantics(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(titulo, style: Theme.of(context).textTheme.labelMedium?.copyWith(color: const Color(0xFF334155))),
            Text(valor),
          ],
        ),
      ),
    );
  }
}
