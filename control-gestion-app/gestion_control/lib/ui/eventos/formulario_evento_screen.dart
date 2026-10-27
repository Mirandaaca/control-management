import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/cliente.dart';
import '../../data/models/evento.dart';
import '../../data/models/pedido.dart';
import '../core/formato.dart';
import '../core/lista_paginada.dart';
import '../core/primer_problema.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';
import '../core/ubicacion/tarjeta_de_ubicacion.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/selectores.dart';
import 'evento_screen.dart';
import 'eventos_view_model.dart';
import 'formulario_evento_view_model.dart';
import 'fotos_del_evento.dart';

/// Con qué se abre un evento nuevo: desde un pedido (7.3.2, ext. 3a), con el pedido y su cliente.
class AbrirNuevoEvento {
  const AbrirNuevoEvento({this.cliente, this.pedido});

  final ClienteDelEvento? cliente;
  final PedidoDelEvento? pedido;
}

/// Registrar un evento (4.1 a 4.3; `frmEventoMovil`; 8.6.8) o, con [eventoId], modificarlo (7.3.13, paso 3).
class FormularioEventoScreen extends ConsumerStatefulWidget {
  const FormularioEventoScreen({super.key, this.eventoId, this.abrir});

  final int? eventoId;
  final AbrirNuevoEvento? abrir;

  @override
  ConsumerState<FormularioEventoScreen> createState() => _FormularioEventoScreenState();
}

class _FormularioEventoScreenState extends ConsumerState<FormularioEventoScreen> {
  final _quePaso = TextEditingController();
  final _ubicacion = (llave: GlobalKey(), foco: null);
  final _lugarDelTipo = (llave: GlobalKey(), foco: FocusNode());
  final _lugarDelCliente = (llave: GlobalKey(), foco: FocusNode());
  final _lugarDelPedido = (llave: GlobalKey(), foco: FocusNode());
  final _lugarDeQuePaso = (llave: GlobalKey(), foco: FocusNode());

  bool get _esNuevo => widget.eventoId == null;
  FormularioEventoViewModel get _viewModel => ref.read(formularioEventoProvider(widget.eventoId).notifier);

  @override
  void initState() {
    super.initState();
    Future.microtask(() {
      if (_esNuevo) ref.read(capturaDeUbicacionProvider(QueSeRegistra.evento).notifier).capturar();
      final abrir = widget.abrir;
      if (abrir != null) _viewModel.iniciar(cliente: abrir.cliente, pedido: abrir.pedido);
    });
  }

  @override
  void dispose() {
    _quePaso.dispose();
    _lugarDelTipo.foco.dispose();
    _lugarDelCliente.foco.dispose();
    _lugarDelPedido.foco.dispose();
    _lugarDeQuePaso.foco.dispose();
    super.dispose();
  }

  Future<void> _elegirTipo() async {
    final estado = ref.read(formularioEventoProvider(widget.eventoId));
    final tipos = estado.tipos ?? const <TipoDeEvento>[];
    final elegido = await elegirDeLaLista<int>(
      context,
      titulo: 'Tipo de evento',
      opciones: [for (final t in tipos) (t.id, t.texto)],
      actual: estado.tipo?.id,
    );
    if (elegido != null) _viewModel.elegirTipo(tipos.firstWhere((t) => t.id == elegido));
  }

  Future<void> _elegirCliente() async {
    final elegido = await context.push<ClienteOpcion>(Rutas.elegirClienteParaEvento);
    if (elegido != null) _viewModel.elegirCliente((id: elegido.id, texto: elegido.texto));
  }

  // "Sin pedido" es el 0: null es que se cerró la lista sin elegir.
  Future<void> _elegirPedido() async {
    final estado = ref.read(formularioEventoProvider(widget.eventoId));
    if (estado.errorDePedidos != null) return _viewModel.reintentarPedidos();
    final opciones = _opcionesDePedido(estado);
    final elegido = await elegirDeLaLista<int>(
      context,
      titulo: 'Pedido',
      opciones: [(0, 'Sin pedido'), for (final p in opciones) (p.id, p.texto)],
      actual: estado.pedido?.id ?? 0,
    );
    if (elegido == null) return;
    final pedido = opciones.where((p) => p.id == elegido).firstOrNull;
    _viewModel.elegirPedido(pedido == null ? null : PedidoDelEvento(id: pedido.id, numero: pedido.numero));
  }

  // Los pedidos del cliente. Al editar, el del evento puede no estar entre ellos (de otra cartera): se agrega.
  List<({int id, String numero, String texto})> _opcionesDePedido(EstadoDelFormularioDeEvento estado) {
    final opciones = [
      for (final p in estado.pedidos ?? const <PedidoResumen>[])
        (
          id: p.id,
          numero: p.numero,
          texto: '${p.numero} · ${p.estado.nombre} · entrega ${formatearFecha(p.fechaEntrega)}',
        ),
    ];
    final elegido = estado.pedido;
    if (elegido != null && !opciones.any((p) => p.id == elegido.id)) {
      opciones.insert(0, (id: elegido.id, numero: elegido.numero, texto: elegido.numero));
    }
    return opciones;
  }

  Future<void> _guardar() async {
    FocusScope.of(context).unfocus();
    final evento = await _viewModel.guardar(_quePaso.text);
    if (!mounted) return;
    if (evento == null) return _mostrarPrimerProblema();
    _terminar(evento);
  }

  void _terminar(Evento evento) {
    recargarSiEstaAbierta(ref, eventosProvider);
    if (_esNuevo) {
      context.pushReplacement(Rutas.evento(evento.id), extra: AbrirEvento(mensaje: mensajeDeEventoRegistrado(evento)));
    } else {
      context.pop(mensajeDeEventoModificado(evento));
    }
  }

  void _mostrarPrimerProblema() {
    final estado = ref.read(formularioEventoProvider(widget.eventoId));
    final campos = estado.campos;
    mostrarPrimerProblema([
      if (estado.sinUbicacion) _ubicacion,
      if (campos.containsKey('tipoEventoId')) _lugarDelTipo,
      if (campos.containsKey('clienteId')) _lugarDelCliente,
      if (campos.containsKey('pedidoId')) _lugarDelPedido,
      if (campos.containsKey('descripcion')) _lugarDeQuePaso,
    ], (_) => true);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(formularioEventoProvider(widget.eventoId));
    // Desde el primer cuadro: si nadie la mira, la búsqueda del GPS se descarta.
    if (_esNuevo) ref.watch(capturaDeUbicacionProvider(QueSeRegistra.evento));
    // Al editar, "Qué pasó" se llena con lo que llegó de la API.
    ref.listen(formularioEventoProvider(widget.eventoId).select((e) => e.evento), (anterior, evento) {
      if (anterior == null && evento != null) _quePaso.text = evento.descripcion;
    });
    final titulo = Text(_esNuevo ? 'Nuevo evento' : 'Editar ${estado.evento?.numero ?? 'evento'}');
    if (estado.tipos == null || (!_esNuevo && estado.evento == null)) {
      return Scaffold(
        appBar: AppBar(title: titulo),
        body: estado.errorDeCarga != null
            ? ErrorConReintento.de(
                estado.errorDeCarga!,
                reintentando: estado.reintentando,
                alReintentar: _viewModel.cargar,
              )
            : const Cargando('Cargando el formulario…'),
      );
    }

    final enviando = estado.enviando;
    // Con el evento ya registrado, lo escrito no se cambia: solo faltan fotos.
    final registrado = estado.registrado != null;
    final bloqueado = enviando || registrado;

    return Scaffold(
      appBar: AppBar(title: titulo),
      body: IgnorePointer(
        ignoring: registrado,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: [
            // 7.3.13, paso 2.1, y 7.3.15, paso 2.1: la ubicación del GPS, sin que el promotor la escriba.
            if (_esNuevo) ...[
              KeyedSubtree(
                key: _ubicacion.llave,
                child: const TarjetaDeUbicacion(registro: QueSeRegistra.evento),
              ),
              const SizedBox(height: 16),
            ],
            KeyedSubtree(
              key: _lugarDelTipo.llave,
              child: CampoDeEleccion(
                etiqueta: 'Tipo de evento',
                valor: estado.tipo?.texto,
                ejemplo: 'Elige el tipo…',
                ayuda: estado.tipo?.requiereEvidencia == true
                    ? 'Este tipo se resuelve con al menos una foto: agrégala ahora o después.'
                    : null,
                focusNode: _lugarDelTipo.foco,
                error: estado.campos['tipoEventoId'],
                alTocar: bloqueado ? null : _elegirTipo,
              ),
            ),
            const SizedBox(height: 12),
            KeyedSubtree(
              key: _lugarDelCliente.llave,
              child: CampoDeEleccion(
                etiqueta: 'Cliente',
                valor: estado.cliente?.texto,
                ejemplo: 'Busca el cliente…',
                buscando: true,
                focusNode: _lugarDelCliente.foco,
                error: estado.campos['clienteId'],
                alTocar: bloqueado ? null : _elegirCliente,
              ),
            ),
            if (_viewModel.puedeElegirPedido) ...[
              const SizedBox(height: 12),
              KeyedSubtree(
                key: _lugarDelPedido.llave,
                child: CampoDeEleccion(
                  etiqueta: 'Pedido (opcional)',
                  valor: estado.pedido == null
                      ? 'Sin pedido'
                      : _opcionesDePedido(estado).firstWhere((p) => p.id == estado.pedido!.id).texto,
                  ayuda: switch (estado) {
                    EstadoDelFormularioDeEvento(cliente: null) => 'Primero elige el cliente.',
                    EstadoDelFormularioDeEvento(cargandoPedidos: true) => 'Cargando los pedidos del cliente…',
                    _ => null,
                  },
                  focusNode: _lugarDelPedido.foco,
                  error:
                      estado.campos['pedidoId'] ??
                      (estado.errorDePedidos == null
                          ? null
                          : 'No se pudieron cargar los pedidos del cliente. Toca para reintentar.'),
                  alTocar: bloqueado || estado.cliente == null || estado.cargandoPedidos ? null : _elegirPedido,
                ),
              ),
            ],
            const SizedBox(height: 12),
            KeyedSubtree(
              key: _lugarDeQuePaso.llave,
              child: CampoDeTexto(
                controller: _quePaso,
                etiqueta: 'Qué pasó',
                ejemplo: 'Por ejemplo: el local estaba cerrado y nadie atendió…',
                lineas: 4,
                textCapitalization: TextCapitalization.sentences,
                keyboardType: TextInputType.multiline,
                textInputAction: TextInputAction.newline,
                focusNode: _lugarDeQuePaso.foco,
                error: estado.campos['descripcion'],
                onChanged: (_) => _viewModel.cambioQuePaso(),
              ),
            ),
            // 7.3.13, ext. 2.3a, y 7.3.14: las fotos, cada una en miniatura con su fecha; hasta 5.
            if (_esNuevo) ...[
              const SizedBox(height: 16),
              _FotosDelFormulario(estado: estado, bloqueado: bloqueado, viewModel: _viewModel),
            ],
          ],
        ),
      ),
      bottomNavigationBar: registrado
          ? BarraDeAcciones(
              arriba: [
                Aviso(
                  estado.errorDeFotos?.mensaje ?? '',
                  tipo: TipoDeAviso.aviso,
                  titulo: estado.fotosQueFaltan == 1
                      ? 'El evento quedó registrado, pero una foto no se subió.'
                      : 'El evento quedó registrado, pero ${estado.fotosQueFaltan} fotos no se subieron.',
                ),
              ],
              secundaria: Accion('Terminar sin fotos', () => _terminar(estado.registrado!)),
              principal: Accion('Reintentar', _guardar, enviando: enviando, textoEnviando: 'Subiendo…'),
            )
          : BarraDeAcciones(
              arriba: [
                if (estado.errorAlEnviar != null)
                  Aviso.deError(estado.errorAlEnviar!, accion: AccionDeAviso('Reintentar', _guardar)),
              ],
              principal: Accion(
                _esNuevo ? 'Registrar evento' : 'Guardar cambios',
                _guardar,
                enviando: enviando,
                textoEnviando: estado.esperandoUbicacion
                    ? 'Buscando la ubicación…'
                    : (_esNuevo ? 'Registrando…' : 'Guardando…'),
              ),
            ),
    );
  }
}

/// Las fotos del evento nuevo (8.6.8): las miniaturas con su hora y su X, "Tomar foto" y "Elegir de la galería".
class _FotosDelFormulario extends StatelessWidget {
  const _FotosDelFormulario({required this.estado, required this.bloqueado, required this.viewModel});

  final EstadoDelFormularioDeEvento estado;
  final bool bloqueado;
  final FormularioEventoViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    final fotos = estado.fotos;
    final hayLugar = fotos.length < maximoDeFotos;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TituloDeFotos(titulo: 'Fotos (opcional)', cantidad: fotos.length),
        if (fotos.isNotEmpty) ...[
          const SizedBox(height: 8),
          GrillaDeFotos(
            fotos: [
              for (final (i, foto) in fotos.indexed)
                MiniaturaDeFoto(
                  imagen: Image.memory(foto.foto.bytes, fit: BoxFit.cover),
                  fecha: foto.foto.deLaGaleria ? 'De la galería' : formatearMomento(foto.foto.tomadaUtc, hora: true),
                  etiqueta: 'Foto ${i + 1}',
                  textoQuitar: 'Quitar la foto ${i + 1}',
                  // Todavía no se subió: quitarla no borra nada, así que no se confirma.
                  alQuitar: bloqueado ? null : () => viewModel.quitarFoto(i),
                ),
            ],
          ),
        ],
        const SizedBox(height: 12),
        BotonesDeFoto(
          alTomar: bloqueado || !hayLugar ? null : viewModel.tomarFoto,
          alElegir: bloqueado || !hayLugar ? null : viewModel.elegirDeLaGaleria,
        ),
        const SizedBox(height: 4),
        Text(
          hayLugar ? 'Se suben al registrar el evento.' : 'Ya tiene 5 fotos: quita una para agregar otra.',
          style: const TextStyle(color: textoSecundario, fontSize: 14, height: 20 / 14),
        ),
      ],
    );
  }
}
