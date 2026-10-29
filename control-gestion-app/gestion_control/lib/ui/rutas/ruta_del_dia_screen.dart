import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/ruta.dart';
import '../../data/services/error_de_api.dart';
import '../../domain/permisos.dart';
import '../../proveedores.dart';
import '../core/formato.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/mensaje_de_exito.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/confirmar.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/vacio.dart';
import '../eventos/formulario_evento_screen.dart';
import '../sesion/sesion_notifier.dart';
import '../territorio/marcadores.dart';
import '../territorio/visitas.dart';
import 'aviso_del_gps.dart';
import 'etiqueta_de_ruta.dart';
import 'mapa_de_la_ruta.dart';
import 'marcadores.dart';
import 'marcar_visita_hoja.dart';
import 'ruta_del_dia_view_model.dart';
import 'seguimiento_de_ruta.dart';

const _slate200 = Color(0xFFE2E8F0);
const _marca300 = Color(0xFF8BB2DD);

/// La ruta del día (5.4; `frmRutaMovil`; 8.6.9): la fecha y la zona, el mapa con las paradas en orden, y las visitas,
/// que se marcan con el GPS. Abre directo la de hoy (decisión 2 de la tarea 17: el paso 1 en la app es esta ruta).
class RutaDelDiaScreen extends ConsumerStatefulWidget {
  const RutaDelDiaScreen({super.key});

  @override
  ConsumerState<RutaDelDiaScreen> createState() => _RutaDelDiaScreenState();
}

class _RutaDelDiaScreenState extends ConsumerState<RutaDelDiaScreen> {
  // Al volver a la app se pide de nuevo: el atraso se cuenta el día en que se muestra, y la ruta pudo cambiar.
  late final _ciclo = AppLifecycleListener(onResume: () => _viewModel.cargar());

  RutaDelDiaViewModel get _viewModel => ref.read(rutaDelDiaProvider.notifier);

  @override
  void initState() {
    super.initState();
    _ciclo;
  }

  @override
  void dispose() {
    _ciclo.dispose();
    super.dispose();
  }

  Future<void> _iniciar(Ruta ruta) async {
    // 7.3.20, paso 4: pide confirmar el inicio, y avisa que desde ahí se envía la ubicación.
    final confirmado = await confirmar(
      context,
      titulo: '¿Iniciar la ruta?',
      contenido: Text(
        '${ruta.zonaNombre} · ${ruta.total == 1 ? '1 visita' : '${ruta.total} visitas'}. Mientras la ruta esté en curso '
        'y la app esté abierta, la app envía tu ubicación cada 30 segundos.',
      ),
      accion: 'Iniciar ruta',
    );
    if (confirmado) await _viewModel.iniciar();
  }

  Future<void> _terminar(Ruta ruta) async {
    // 7.3.20, paso 7 y ext. 7.1a: con visitas sin marcar, avisa cuántas quedan pendientes.
    final pendientes = ruta.pendientes;
    final texto = switch (pendientes) {
      0 => 'Marcaste las ${ruta.total} visitas. La app deja de enviar tu ubicación.',
      1 => 'Queda 1 visita sin marcar: queda pendiente. La app deja de enviar tu ubicación.',
      _ => 'Quedan $pendientes visitas sin marcar: quedan pendientes. La app deja de enviar tu ubicación.',
    };
    final confirmado = await confirmar(
      context,
      titulo: '¿Terminar la ruta?',
      contenido: Text(texto),
      accion: 'Terminar ruta',
    );
    if (confirmado) await _viewModel.terminar();
  }

  Future<void> _marcar(Ruta ruta, VisitaDeRuta visita) async {
    final marcada = await marcarVisita(context, rutaId: ruta.id, visita: visita);
    if (marcada != null) _viewModel.marcada(marcada, visita.id);
  }

  // Tarea 25: la navegación hasta la visita. Al llegar, "Marcar visita" vuelve aquí y abre la hoja de marcarla.
  Future<void> _comoLlegar(Ruta ruta, VisitaDeRuta visita) async {
    final marcar = await context.push<bool>(Rutas.camino, extra: (rutaId: ruta.id, visita: visita));
    if (marcar == true && mounted) await _marcar(ruta, visita);
  }

  // 7.3.20, ext. 5a: la venta de la visita, con el cliente ya elegido.
  Future<void> _nuevaHoja(VisitaDeRuta visita) async {
    try {
      final cliente = await ref.read(clientesRepositoryProvider).obtener(visita.cliente.id);
      if (mounted) await context.push(Rutas.nuevaHoja, extra: cliente.comoOpcion);
    } on ErrorDeApi catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.mensaje)));
    }
  }

  // 7.3.20, ext. 5.3a: el evento del cliente ausente, con el cliente ya elegido.
  void _registrarEvento(VisitaDeRuta visita) => context.push(
    Rutas.nuevoEvento,
    extra: AbrirNuevoEvento(
      cliente: (id: visita.cliente.id, texto: '${visita.cliente.nombreCompleto} · ${visita.cliente.codigo}'),
    ),
  );

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(rutaDelDiaProvider);
    ref.listen(rutaDelDiaProvider.select((e) => e.mensaje), (anterior, mensaje) {
      if (mensaje != null && mensaje != anterior) avisarExito(context, mensaje);
    });
    final seguimiento = ref.watch(seguimientoDeRutaProvider);
    final sesion = ref.watch(sesionProvider);
    bool tiene(String permiso) => sesion is ConSesion && sesion.sesion.tiene([permiso]);
    final ruta = estado.ruta;

    return Scaffold(
      appBar: AppBar(title: const Text('Ruta del día')),
      body: switch (estado) {
        EstadoDeLaRuta(cargada: false, errorDeCarga: final error?) => ErrorConReintento.de(
          error,
          reintentando: estado.reintentando,
          alReintentar: _viewModel.cargar,
        ),
        EstadoDeLaRuta(cargada: false) => const Cargando('Cargando la ruta del día…'),
        _ => RefreshIndicator(
          onRefresh: _viewModel.cargar,
          child: ruta == null
              // 7.3.20, ext. 3.1a.
              ? ListView(
                  children: const [
                    Vacio(
                      icono: Icons.route_outlined,
                      titulo: 'No tienes una ruta para hoy',
                      texto:
                          'Aquí aparece la ruta que el gerente general te planifica para cada día, con los clientes en '
                          'el orden de visita.',
                    ),
                  ],
                )
              : ListView(
                  padding: const EdgeInsets.only(bottom: 24),
                  children: [
                    _Banda(ruta),
                    MapaDeLaRuta(
                      ruta: ruta,
                      zona: estado.zona,
                      puntos: estado.puntos,
                      tu: ruta.estado == EstadoRuta.enCurso && seguimiento.rutaId == ruta.id
                          ? seguimiento.ultima
                          : null,
                    ),
                    _LeyendaDeLaRuta(conTu: ruta.estado == EstadoRuta.enCurso, conPuntos: estado.puntos.isNotEmpty),
                    Padding(
                      padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          if (ruta.estado == EstadoRuta.enCurso && seguimiento.rutaId == ruta.id)
                            if (avisoDelGps(ref, seguimiento.problema, ruta.id) case final aviso?) ...[
                              aviso,
                              const SizedBox(height: 16),
                            ],
                          if (estado.errorDeAccion ?? estado.errorDeCarga case final error?) ...[
                            Aviso.deError(error),
                            const SizedBox(height: 16),
                          ],
                          Semantics(
                            header: true,
                            container: true,
                            child: Text(
                              'Visitas · ${ruta.marcadas} de ${ruta.total} marcadas',
                              style: Theme.of(context).textTheme.titleMedium,
                            ),
                          ),
                          const SizedBox(height: 12),
                          for (final visita in ruta.visitas) ...[
                            _TarjetaDeVisita(
                              visita,
                              enCurso: ruta.estado == EstadoRuta.enCurso,
                              siguiente: ruta.siguiente?.id == visita.id,
                              recienMarcada: estado.recienMarcada == visita.id,
                              puedeVender: tiene(Permisos.ventas.registrarHp),
                              puedeRegistrarEvento: tiene(Permisos.eventos.registrarEvento),
                              alMarcar: () => _marcar(ruta, visita),
                              alComoLlegar: () => _comoLlegar(ruta, visita),
                              alVender: () => _nuevaHoja(visita),
                              alRegistrarEvento: () => _registrarEvento(visita),
                            ),
                            const SizedBox(height: 12),
                          ],
                        ],
                      ),
                    ),
                  ],
                ),
        ),
      },
      bottomNavigationBar: switch (ruta?.estado) {
        EstadoRuta.planificada => BarraDeAcciones(
          principal: Accion(
            'Iniciar ruta',
            () => _iniciar(ruta!),
            enviando: estado.enviando,
            textoEnviando: 'Iniciando…',
          ),
        ),
        // Secundario mientras quedan visitas sin marcar; con todas marcadas, es lo que sigue (aviso del alcance).
        EstadoRuta.enCurso => BarraDeAcciones(
          conEstiloSecundario: ruta!.pendientes > 0,
          principal: Accion(
            'Terminar ruta',
            () => _terminar(ruta),
            enviando: estado.enviando,
            textoEnviando: 'Terminando…',
          ),
        ),
        _ => null,
      },
    );
  }
}

/// La fecha de la ruta, su zona y su estado (7.3.20, paso 3): "30/10/2026 · Zona Norte" y "En curso desde las 08:15".
class _Banda extends StatelessWidget {
  const _Banda(this.ruta);

  final Ruta ruta;

  @override
  Widget build(BuildContext context) => Container(
    color: Colors.white,
    padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Semantics(
          header: true,
          container: true,
          // La fecha llega sin hora ("2026-10-30"): se lee como el mediodía, así ninguna zona la corre de día.
          child: Text(
            '${formatearFecha('${ruta.fecha}T16:00:00')} · ${ruta.zonaNombre}',
            style: const TextStyle(fontSize: 18, height: 24 / 18, fontWeight: FontWeight.w600),
          ),
        ),
        const SizedBox(height: 6),
        EtiquetaDeRuta(ruta),
      ],
    ),
  );
}

class _LeyendaDeLaRuta extends StatelessWidget {
  const _LeyendaDeLaRuta({required this.conTu, required this.conPuntos});

  final bool conTu;
  final bool conPuntos;

  @override
  Widget build(BuildContext context) => Container(
    decoration: const BoxDecoration(
      color: Colors.white,
      border: Border(bottom: BorderSide(color: _slate200)),
    ),
    padding: const EdgeInsets.fromLTRB(16, 12, 16, 12),
    child: Leyenda([
      const EntradaDeLeyenda(Pastilla(4, EstadoVisita.pendiente, sombra: false), 'Pendiente'),
      const EntradaDeLeyenda(Pastilla(1, EstadoVisita.visitado, sombra: false), 'Visitado'),
      const EntradaDeLeyenda(Pastilla(2, EstadoVisita.ausente, sombra: false), 'Ausente'),
      if (conTu) const EntradaDeLeyenda(CirculoDelPromotor(lado: 24), 'Tú'),
      if (conPuntos) const EntradaDeLeyenda(MarcadorDePunto(lado: 24), 'Punto de referencia'),
    ]),
  );
}

/// Una visita de la ruta (8.6.9): su pastilla, el cliente, la dirección y el resultado. Con la ruta en curso, la
/// siguiente pendiente lleva "Marcar visita" primario; las otras pendientes y las ausentes, secundario (un ausente se
/// vuelve a marcar: tarea 14). La recién marcada ofrece lo que sigue (decisión 3 de la tarea 17). La siguiente, además,
/// lleva "Cómo llegar" (tarea 25).
class _TarjetaDeVisita extends StatelessWidget {
  const _TarjetaDeVisita(
    this.visita, {
    required this.enCurso,
    required this.siguiente,
    required this.recienMarcada,
    required this.puedeVender,
    required this.puedeRegistrarEvento,
    required this.alMarcar,
    required this.alComoLlegar,
    required this.alVender,
    required this.alRegistrarEvento,
  });

  final VisitaDeRuta visita;
  final bool enCurso;
  final bool siguiente;
  final bool recienMarcada;
  final bool puedeVender;
  final bool puedeRegistrarEvento;
  final VoidCallback alMarcar;
  final VoidCallback alComoLlegar;
  final VoidCallback alVender;
  final VoidCallback alRegistrarEvento;

  @override
  Widget build(BuildContext context) {
    final marcable = enCurso && visita.resultado != EstadoVisita.visitado;
    final destacada = enCurso && siguiente;
    final despues = switch (visita.resultado) {
      _ when !recienMarcada || !enCurso => null,
      // Sin ícono, como "Marcar visita": con la letra al 200 % el texto se parte y el ícono quedaba corrido.
      EstadoVisita.visitado when puedeVender => OutlinedButton(
        onPressed: alVender,
        child: const Text('Nueva hoja de pedido', textAlign: TextAlign.center),
      ),
      EstadoVisita.ausente when puedeRegistrarEvento => OutlinedButton(
        onPressed: alRegistrarEvento,
        child: const Text('Registrar evento', textAlign: TextAlign.center),
      ),
      _ => null,
    };
    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: destacada ? _marca300 : _slate200, width: destacada ? 2 : 1),
      ),
      padding: const EdgeInsets.all(12),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Padding(
            padding: const EdgeInsets.only(top: 2),
            child: ExcludeSemantics(child: Pastilla(visita.orden, visita.resultado, ancho: 48, sombra: false)),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // El número de orden, que la pastilla muestra, para el lector de pantalla.
                Semantics(
                  label: '${visita.orden}. ',
                  child: Text(visita.cliente.nombreCompleto, style: const TextStyle(fontWeight: FontWeight.w600)),
                ),
                Text(visita.cliente.direccion, style: const TextStyle(color: textoSecundario)),
                const SizedBox(height: 4),
                Text(
                  resultadoDeLaVisita(visita),
                  style: const TextStyle(color: Color(0xFF334155), fontFeatures: [FontFeature.tabularFigures()]),
                ),
                if (visita.observacion case final observacion?)
                  Text('“$observacion”', style: const TextStyle(color: textoSecundario)),
                if (marcable) ...[
                  const SizedBox(height: 12),
                  if (destacada) ...[
                    OutlinedButton(onPressed: alComoLlegar, child: const Text('Cómo llegar')),
                    const SizedBox(height: 8),
                    FilledButton(onPressed: alMarcar, child: const Text('Marcar visita')),
                  ] else
                    OutlinedButton(onPressed: alMarcar, child: const Text('Marcar visita')),
                ],
                if (despues != null) ...[const SizedBox(height: 12), despues],
              ],
            ),
          ),
        ],
      ),
    );
  }
}
