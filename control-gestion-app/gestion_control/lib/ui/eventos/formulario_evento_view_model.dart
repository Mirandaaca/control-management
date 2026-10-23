import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/evento.dart';
import '../../data/models/pedido.dart';
import '../../data/services/camara.dart';
import '../../data/services/clave_de_idempotencia.dart';
import '../../data/services/error_de_api.dart';
import '../../data/services/ubicacion.dart';
import '../../proveedores.dart';
import '../core/router.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';
import '../sesion/sesion_notifier.dart';
import 'fotos_del_evento.dart';

const _igual = Object();

/// El cliente elegido: de la búsqueda trae su código ("Pedro Mamani Cruz · CLI-00064"); de un pedido, solo su nombre.
typedef ClienteDelEvento = ({int id, String texto});

/// Una foto del formulario, que se sube después de registrar el evento.
class FotoDelFormulario {
  const FotoDelFormulario(this.foto, {this.lugar, this.subida = false});

  final FotoTomada foto;

  /// Dónde se tomó: la mejor lectura del GPS en ese momento. Una de la galería no lo tiene.
  final Posicion? lugar;
  final bool subida;
}

class EstadoDelFormularioDeEvento {
  const EstadoDelFormularioDeEvento({
    this.tipos,
    this.errorDeCarga,
    this.reintentando = false,
    this.evento,
    this.tipo,
    this.cliente,
    this.pedidos,
    this.cargandoPedidos = false,
    this.errorDePedidos,
    this.pedido,
    this.fotos = const [],
    this.campos = const {},
    this.enviando = false,
    this.esperandoUbicacion = false,
    this.errorAlEnviar,
    this.sinUbicacion = false,
    this.registrado,
    this.errorDeFotos,
  });

  /// Los tipos de evento, de la API. Null mientras cargan.
  final List<TipoDeEvento>? tipos;
  final ErrorDeApi? errorDeCarga;
  final bool reintentando;

  /// El evento que se edita, o null si es uno nuevo.
  final Evento? evento;
  final TipoDeEvento? tipo;
  final ClienteDelEvento? cliente;

  /// Los pedidos del cliente elegido (el pedido se elige entre ellos). Null sin cliente.
  final List<PedidoResumen>? pedidos;
  final bool cargandoPedidos;
  final ErrorDeApi? errorDePedidos;
  final PedidoDelEvento? pedido;
  final List<FotoDelFormulario> fotos;

  /// Los errores por campo, con las claves de la API: `tipoEventoId`, `clienteId`, `pedidoId`, `descripcion`.
  final Map<String, String> campos;
  final bool enviando;

  /// Se tocó el botón antes de tener una ubicación: espera la primera lectura.
  final bool esperandoUbicacion;
  final ErrorDeApi? errorAlEnviar;
  final bool sinUbicacion;

  /// El evento ya quedó registrado y faltan fotos por subir: "Reintentar" sube solo esas.
  final Evento? registrado;
  final ErrorDeApi? errorDeFotos;

  bool get esNuevo => evento == null;
  int get fotosQueFaltan => fotos.where((f) => !f.subida).length;

  EstadoDelFormularioDeEvento copiar({
    Object? tipos = _igual,
    Object? errorDeCarga = _igual,
    bool? reintentando,
    Object? evento = _igual,
    Object? tipo = _igual,
    Object? cliente = _igual,
    Object? pedidos = _igual,
    bool? cargandoPedidos,
    Object? errorDePedidos = _igual,
    Object? pedido = _igual,
    List<FotoDelFormulario>? fotos,
    Map<String, String>? campos,
    bool? enviando,
    bool? esperandoUbicacion,
    Object? errorAlEnviar = _igual,
    bool? sinUbicacion,
    Object? registrado = _igual,
    Object? errorDeFotos = _igual,
  }) => EstadoDelFormularioDeEvento(
    tipos: tipos == _igual ? this.tipos : tipos as List<TipoDeEvento>?,
    errorDeCarga: errorDeCarga == _igual ? this.errorDeCarga : errorDeCarga as ErrorDeApi?,
    reintentando: reintentando ?? this.reintentando,
    evento: evento == _igual ? this.evento : evento as Evento?,
    tipo: tipo == _igual ? this.tipo : tipo as TipoDeEvento?,
    cliente: cliente == _igual ? this.cliente : cliente as ClienteDelEvento?,
    pedidos: pedidos == _igual ? this.pedidos : pedidos as List<PedidoResumen>?,
    cargandoPedidos: cargandoPedidos ?? this.cargandoPedidos,
    errorDePedidos: errorDePedidos == _igual ? this.errorDePedidos : errorDePedidos as ErrorDeApi?,
    pedido: pedido == _igual ? this.pedido : pedido as PedidoDelEvento?,
    fotos: fotos ?? this.fotos,
    campos: campos ?? this.campos,
    enviando: enviando ?? this.enviando,
    esperandoUbicacion: esperandoUbicacion ?? this.esperandoUbicacion,
    errorAlEnviar: errorAlEnviar == _igual ? this.errorAlEnviar : errorAlEnviar as ErrorDeApi?,
    sinUbicacion: sinUbicacion ?? this.sinUbicacion,
    registrado: registrado == _igual ? this.registrado : registrado as Evento?,
    errorDeFotos: errorDeFotos == _igual ? this.errorDeFotos : errorDeFotos as ErrorDeApi?,
  );
}

/// Los avisos de éxito, con los textos del panel (`Eventos.jsx`).
String mensajeDeEventoRegistrado(Evento evento) =>
    'Se registró ${evento.numero}: queda pendiente hasta que se resuelva.';
String mensajeDeEventoModificado(Evento evento) => '${evento.numero} quedó modificado.';

/// Registrar un evento (4.1 a 4.3; 7.3.13, paso 2; 8.6.8): la ubicación del GPS, el tipo, el cliente o el pedido, qué
/// pasó y sus fotos, que se suben después de registrarlo. Con [eventoId], modificarlo (paso 3): sin la ubicación, que
/// no cambia, ni las fotos, que se administran en el detalle.
class FormularioEventoViewModel extends Notifier<EstadoDelFormularioDeEvento> {
  FormularioEventoViewModel(this.eventoId);

  final int? eventoId;
  final _clave = ClaveDeIdempotencia();

  // Errores de la API que pertenecen a un campo, como `CAMPO_DEL_ERROR` del formulario del panel.
  static const _campoDelCodigo = {
    'tipo_no_valido': 'tipoEventoId',
    'cliente_no_existe': 'clienteId',
    'pedido_no_existe': 'pedidoId',
    'cliente_no_coincide': 'pedidoId',
  };

  @override
  EstadoDelFormularioDeEvento build() {
    Future.microtask(cargar);
    return const EstadoDelFormularioDeEvento();
  }

  /// Puede ver los pedidos del cliente: sin eso, el formulario no ofrece elegir el pedido.
  bool get puedeElegirPedido {
    final sesion = ref.read(sesionProvider);
    return sesion is ConSesion && sesion.sesion.tiene(PermisosDe.pedidos);
  }

  Future<void> cargar() async {
    state = state.copiar(reintentando: state.errorDeCarga != null);
    try {
      final repositorio = ref.read(eventosRepositoryProvider);
      final tipos = await repositorio.tipos();
      final evento = eventoId == null ? null : await repositorio.obtener(eventoId!);
      if (!ref.mounted) return;
      state = state.copiar(tipos: tipos, errorDeCarga: null, reintentando: false);
      if (evento != null) _llenarCon(evento, tipos);
    } on ErrorDeApi catch (e) {
      if (ref.mounted) state = state.copiar(errorDeCarga: e, reintentando: false);
    }
  }

  void _llenarCon(Evento evento, List<TipoDeEvento> tipos) {
    final cliente = evento.cliente;
    state = state.copiar(
      evento: evento,
      // Un tipo que se desactivó después se conserva: la API lo acepta si no cambia.
      tipo: tipos.where((t) => t.id == evento.tipo.id).firstOrNull ?? evento.tipo,
      cliente: cliente == null ? null : (id: int.parse(cliente.id), texto: cliente.nombre),
      pedido: evento.pedido,
    );
    if (cliente != null) _cargarPedidos(int.parse(cliente.id));
  }

  /// Abierto desde un pedido (7.3.2, ext. 3a), vienen el pedido y su cliente.
  void iniciar({ClienteDelEvento? cliente, PedidoDelEvento? pedido}) {
    if (cliente == null) return;
    state = state.copiar(cliente: cliente, pedido: pedido);
    _cargarPedidos(cliente.id);
  }

  void elegirTipo(TipoDeEvento tipo) =>
      state = state.copiar(tipo: tipo, campos: {...state.campos}..remove('tipoEventoId'));

  /// Otro cliente: el pedido elegido era del anterior, así que se vacía.
  void elegirCliente(ClienteDelEvento cliente) {
    if (cliente.id == state.cliente?.id) return;
    state = state.copiar(
      cliente: cliente,
      pedido: null,
      pedidos: null,
      campos: {...state.campos}..removeWhere((c, _) => c == 'clienteId' || c == 'pedidoId'),
    );
    _cargarPedidos(cliente.id);
  }

  void elegirPedido(PedidoDelEvento? pedido) =>
      state = state.copiar(pedido: pedido, campos: {...state.campos}..remove('pedidoId'));

  void cambioQuePaso() {
    if (state.campos.containsKey('descripcion')) {
      state = state.copiar(campos: {...state.campos}..remove('descripcion'));
    }
  }

  Future<void> reintentarPedidos() async {
    final cliente = state.cliente;
    if (cliente != null) await _cargarPedidos(cliente.id);
  }

  // Si se cambia de cliente antes de que llegue la respuesta, se descarta.
  Future<void> _cargarPedidos(int clienteId) async {
    if (!puedeElegirPedido) return;
    state = state.copiar(cargandoPedidos: true, errorDePedidos: null);
    try {
      final pagina = await ref.read(pedidosRepositoryProvider).listar(clienteId: clienteId, tamano: 100);
      if (!ref.mounted || state.cliente?.id != clienteId) return;
      state = state.copiar(pedidos: pagina.items, cargandoPedidos: false);
    } on ErrorDeApi catch (e) {
      if (!ref.mounted || state.cliente?.id != clienteId) return;
      state = state.copiar(cargandoPedidos: false, errorDePedidos: e);
    }
  }

  Future<void> tomarFoto() async {
    final foto = await ref.read(camaraProvider).tomarFoto();
    if (foto == null || !ref.mounted) return;
    // El lugar de la toma: la mejor lectura que ya tenga la tarjeta del evento.
    final lugar = ref.read(capturaDeUbicacionProvider(QueSeRegistra.evento)).posicion;
    _agregar(FotoDelFormulario(foto, lugar: lugar));
  }

  Future<void> elegirDeLaGaleria() async {
    final foto = await ref.read(camaraProvider).elegirDeLaGaleria();
    if (foto != null && ref.mounted) _agregar(FotoDelFormulario(foto));
  }

  void _agregar(FotoDelFormulario foto) {
    if (state.fotos.length >= maximoDeFotos) return;
    state = state.copiar(fotos: [...state.fotos, foto]);
  }

  // Todavía no se subió: quitarla no borra nada, así que no se confirma.
  void quitarFoto(int indice) => state = state.copiar(fotos: [...state.fotos]..removeAt(indice));

  /// Revisa lo obligatorio (7.3.13, ext. 2.2a): cada error va en su campo.
  bool revisar(String quePaso) {
    final errores = <String, String>{
      if (state.tipo == null) 'tipoEventoId': 'Elige el tipo de evento.',
      if (state.cliente == null && state.pedido == null) 'clienteId': 'Elige el cliente o el pedido del evento.',
      if (quePaso.trim().isEmpty) 'descripcion': 'Describe lo que pasó.',
    };
    state = state.copiar(campos: errores, errorAlEnviar: null, sinUbicacion: false);
    return errores.isEmpty;
  }

  /// Registra el evento (con la ubicación de la tarjeta) y sube sus fotos, o guarda los cambios. Devuelve el evento, o
  /// null si algo lo impidió: los errores quedan en el estado y lo escrito no se pierde, ni las fotos.
  Future<Evento?> guardar(String quePaso) async {
    if (state.enviando) return null;
    // El evento ya quedó registrado: solo faltan fotos.
    if (state.registrado != null) return reintentarFotos();
    if (!revisar(quePaso)) return null;

    final datos = DatosDeEvento(
      tipoEventoId: state.tipo!.id,
      clienteId: state.cliente?.id,
      pedidoId: state.pedido?.id,
      descripcion: quePaso,
    );
    final repositorio = ref.read(eventosRepositoryProvider);

    if (!state.esNuevo) {
      state = state.copiar(enviando: true);
      try {
        final evento = await repositorio.modificar(state.evento!.id, datos);
        if (ref.mounted) state = state.copiar(enviando: false);
        return evento;
      } on ErrorDeApi catch (e) {
        if (ref.mounted) _fallo(e);
        return null;
      }
    }

    state = state.copiar(enviando: true, esperandoUbicacion: true);
    final ubicacion = await ref.read(capturaDeUbicacionProvider(QueSeRegistra.evento).notifier).paraGuardar();
    if (!ref.mounted) return null;
    if (ubicacion == null) {
      state = state.copiar(enviando: false, esperandoUbicacion: false, sinUbicacion: true);
      return null;
    }

    state = state.copiar(esperandoUbicacion: false);
    final Evento evento;
    try {
      evento = await repositorio.registrar(datos, ubicacion, clave: _clave.valor);
      _clave.despuesDelEnvio(huboRespuesta: true);
    } on ErrorDeApi catch (e) {
      _clave.despuesDelEnvio(huboRespuesta: !e.sinConexion);
      if (ref.mounted) _fallo(e);
      return null;
    }
    if (!ref.mounted) return null;
    if (state.fotos.isEmpty) {
      state = state.copiar(enviando: false);
      return evento;
    }
    state = state.copiar(registrado: evento);
    return reintentarFotos();
  }

  void _fallo(ErrorDeApi e) {
    final campos = e.camposCon(_campoDelCodigo);
    state = state.copiar(
      enviando: false,
      esperandoUbicacion: false,
      campos: campos,
      errorAlEnviar: campos.isEmpty ? e : null,
    );
  }

  /// Sube las fotos que faltan del evento ya registrado, una por una. Si una falla, el evento no se repite.
  Future<Evento?> reintentarFotos() async {
    var evento = state.registrado;
    if (evento == null) return null;
    state = state.copiar(enviando: true, errorDeFotos: null);
    final repositorio = ref.read(eventosRepositoryProvider);
    for (var i = 0; i < state.fotos.length; i++) {
      final foto = state.fotos[i];
      if (foto.subida) continue;
      try {
        evento = await repositorio.agregarFoto(evento!.id, foto.foto, lugar: foto.lugar);
        if (!ref.mounted) return null;
        state = state.copiar(
          fotos: [...state.fotos]..[i] = FotoDelFormulario(foto.foto, lugar: foto.lugar, subida: true),
        );
      } on ErrorDeApi catch (e) {
        if (ref.mounted) state = state.copiar(enviando: false, errorDeFotos: e);
        return null;
      }
    }
    if (ref.mounted) state = state.copiar(enviando: false, registrado: evento);
    return evento;
  }
}

final formularioEventoProvider = NotifierProvider.autoDispose
    .family<FormularioEventoViewModel, EstadoDelFormularioDeEvento, int?>(FormularioEventoViewModel.new);
