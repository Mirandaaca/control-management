import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/cliente.dart';
import '../../domain/geometria.dart';
import '../core/lista_paginada.dart';
import '../core/primer_problema.dart';
import '../core/router.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';
import '../core/ubicacion/mapa_de_ubicacion.dart';
import '../core/ubicacion/tarjeta_de_ubicacion.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/selectores.dart';
import 'clientes_view_model.dart';
import 'formulario_cliente_view_model.dart';

/// Registrar o editar un cliente (8.6.6): la ubicación del GPS con su mapa, los obligatorios (nombre, celular, dirección
/// y zona) y después los opcionales.
class FormularioClienteScreen extends ConsumerStatefulWidget {
  const FormularioClienteScreen({super.key, this.id, this.capturarAlAbrir = false, this.devolver = false});

  /// El cliente que se edita; sin él, se registra uno nuevo.
  final int? id;

  /// Al editar un cliente sin ubicación, "Capturar ubicación" abre el formulario buscándola.
  final bool capturarAlAbrir;

  /// Abierto desde la búsqueda de una venta: al registrarlo, vuelve con el cliente elegido (7.3.5, ext. 2.2a).
  final bool devolver;

  @override
  ConsumerState<FormularioClienteScreen> createState() => _FormularioClienteScreenState();
}

class _FormularioClienteScreenState extends ConsumerState<FormularioClienteScreen> {
  final _nombre = TextEditingController();
  final _celular = TextEditingController();
  final _direccion = TextEditingController();
  final _razonSocial = TextEditingController();
  final _nit = TextEditingController();
  final _correo = TextEditingController();
  final _focoDelCelular = FocusNode();

  // Los lugares del formulario, en el orden de la pantalla: la vista va al primero con un problema.
  final _ubicacion = (llave: GlobalKey(), foco: null);
  final _lugares = {
    for (final campo in ['nombreCompleto', 'telefono', 'direccionTexto', 'zonaId', 'razonSocial', 'nit', 'email'])
      campo: (llave: GlobalKey(), foco: FocusNode()),
  };

  bool get _esNuevo => widget.id == null;

  FormularioClienteViewModel get _viewModel => ref.read(formularioClienteProvider(widget.id).notifier);

  CapturaDeUbicacion get _captura => ref.read(capturaDeUbicacionProvider(QueSeRegistra.cliente).notifier);

  @override
  void initState() {
    super.initState();
    _focoDelCelular.addListener(() {
      if (!_focoDelCelular.hasFocus) _viewModel.revisarCelular(_celular.text);
    });
    // Un cliente nuevo se ubica donde está el promotor; uno que se edita conserva la suya.
    if (_esNuevo) Future.microtask(_captura.capturar);
  }

  @override
  void dispose() {
    for (final controlador in [_nombre, _celular, _direccion, _razonSocial, _nit, _correo]) {
      controlador.dispose();
    }
    _focoDelCelular.dispose();
    for (final lugar in _lugares.values) {
      lugar.foco.dispose();
    }
    super.dispose();
  }

  void _llenar(Cliente cliente) {
    _nombre.text = cliente.nombreCompleto;
    _celular.text = cliente.telefono;
    _direccion.text = cliente.direccion;
    _razonSocial.text = cliente.razonSocial ?? '';
    _nit.text = cliente.nit ?? '';
    _correo.text = cliente.email ?? '';
    _captura.usarGuardada(cliente.latitud, cliente.longitud);
    if (widget.capturarAlAbrir) _captura.capturar();
  }

  Future<void> _elegirZona(EstadoFormularioCliente estado) async {
    final elegida = await elegirDeLaLista<int>(
      context,
      titulo: 'Zona',
      opciones: [for (final zona in estado.zonas!) (zona.id, zona.nombre)],
      actual: estado.zonaId,
    );
    if (elegida != null) _viewModel.elegirZona(elegida);
  }

  Future<void> _enviar() async {
    FocusScope.of(context).unfocus();
    final estado = ref.read(formularioClienteProvider(widget.id));
    final guardado = await _viewModel.enviar(
      DatosDeCliente(
        nombreCompleto: _nombre.text,
        telefono: _celular.text,
        direccion: _direccion.text,
        zonaId: estado.zonaId,
        razonSocial: _razonSocial.text,
        nit: _nit.text,
        email: _correo.text,
      ),
    );
    if (!mounted) return;
    if (guardado == null) {
      final despues = ref.read(formularioClienteProvider(widget.id));
      mostrarPrimerProblema(
        [_ubicacion, ..._lugares.values],
        (lugar) =>
            (identical(lugar, _ubicacion) && despues.sinUbicacion) ||
            _lugares.entries.any((e) => identical(e.value, lugar) && despues.campos.containsKey(e.key)),
      );
      return;
    }
    recargarSiEstaAbierta(ref, clientesProvider);
    if (!_esNuevo) {
      context.pop('Se guardaron los cambios de ${guardado.nombreCompleto}.');
    } else if (widget.devolver) {
      context.pop(guardado.comoOpcion);
    } else {
      context.pushReplacement(Rutas.cliente(guardado.id), extra: 'Se registró a ${guardado.nombreCompleto}.');
    }
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(formularioClienteProvider(widget.id));
    final ubicacion = ref.watch(capturaDeUbicacionProvider(QueSeRegistra.cliente));
    ref.listen(formularioClienteProvider(widget.id).select((e) => e.original), (anterior, original) {
      if (original != null && anterior == null) _llenar(original);
    });
    ref.listen(capturaDeUbicacionProvider(QueSeRegistra.cliente), (_, ubicacion) {
      if (ubicacion is UbicacionCapturada) _viewModel.alCapturar(ubicacion.lectura);
    });

    final titulo = _esNuevo ? 'Nuevo cliente' : 'Editar cliente';
    if (estado.errorDeCarga != null) {
      return Scaffold(
        appBar: AppBar(title: Text(titulo)),
        body: ErrorConReintento.de(
          estado.errorDeCarga!,
          reintentando: estado.reintentando,
          alReintentar: _viewModel.cargar,
        ),
      );
    }
    if (estado.zonas == null) {
      return Scaffold(
        appBar: AppBar(title: Text(titulo)),
        body: const Cargando('Cargando…'),
      );
    }

    final punto = ubicacion.posicion;
    final zona = estado.zona;
    final caeEnLaZona =
        punto != null && zona?.limite != null && puntoEnPoligono(punto.latitud, punto.longitud, zona!.limite!);
    final accion = _esNuevo ? 'Registrar cliente' : 'Guardar cambios';

    return Scaffold(
      appBar: AppBar(title: Text(titulo)),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          KeyedSubtree(
            key: _ubicacion.llave,
            child: TarjetaDeUbicacion(registro: QueSeRegistra.cliente, alEditar: !_esNuevo),
          ),
          if (punto != null) ...[
            const SizedBox(height: 12),
            MapaDeUbicacion(latitud: punto.latitud, longitud: punto.longitud),
          ],
          const SizedBox(height: 16),
          _campo(
            'nombreCompleto',
            estado,
            controlador: _nombre,
            etiqueta: 'Nombre completo',
            mayusculas: TextCapitalization.words,
            pistas: const [AutofillHints.name],
          ),
          _campo(
            'telefono',
            estado,
            controlador: _celular,
            etiqueta: 'Celular',
            teclado: TextInputType.phone,
            pistas: const [AutofillHints.telephoneNumber],
            foco: _focoDelCelular,
            alCambiar: _viewModel.cambioElCelular,
            debajo: _avisoDelCelular(estado.avisoCelular, accion),
          ),
          _campo(
            'direccionTexto',
            estado,
            controlador: _direccion,
            etiqueta: 'Dirección',
            mayusculas: TextCapitalization.sentences,
            pistas: const [AutofillHints.fullStreetAddress],
            lineas: 3,
          ),
          KeyedSubtree(
            key: _lugares['zonaId']!.llave,
            child: CampoDeEleccion(
              etiqueta: 'Zona',
              valor: zona?.nombre,
              ejemplo: estado.zonas!.isEmpty ? 'No tienes zonas asignadas' : 'Elige la zona…',
              alTocar: estado.zonas!.isEmpty ? null : () => _elegirZona(estado),
              error: estado.campos['zonaId'],
              ayuda: caeEnLaZona ? 'El punto cae dentro del límite de esta zona.' : null,
              focusNode: _lugares['zonaId']!.foco,
            ),
          ),
          const SizedBox(height: 12),
          _campo('razonSocial', estado, controlador: _razonSocial, etiqueta: 'Razón social (opcional)'),
          _campo('nit', estado, controlador: _nit, etiqueta: 'NIT (opcional)', teclado: TextInputType.number),
          _campo(
            'email',
            estado,
            controlador: _correo,
            etiqueta: 'Correo (opcional)',
            teclado: TextInputType.emailAddress,
            pistas: const [AutofillHints.email],
            ultimo: true,
          ),
        ],
      ),
      bottomNavigationBar: BarraDeAcciones(
        arriba: [
          if (estado.errorAlEnviar != null)
            Aviso.deError(estado.errorAlEnviar!, accion: AccionDeAviso('Reintentar', _enviar)),
        ],
        principal: Accion(
          accion,
          _enviar,
          enviando: estado.envio != EnvioDeCliente.ninguno,
          textoEnviando: estado.envio == EnvioDeCliente.esperandoUbicacion
              ? 'Buscando la ubicación…'
              : (_esNuevo ? 'Registrando…' : 'Guardando…'),
        ),
      ),
    );
  }

  Widget? _avisoDelCelular(AvisoDeCelular? aviso, String accion) => switch (aviso) {
    null => null,
    CelularDeMiCartera(:final cliente) => Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Aviso(
        'Si es otro cliente, puedes ${_esNuevo ? 'registrarlo' : 'guardarlo'} igual.',
        tipo: TipoDeAviso.aviso,
        titulo: 'Ya existe el cliente ${cliente.nombreCompleto} (${cliente.codigo}) con ese celular.',
        accion: AccionDeAviso('Abrir cliente', () => context.push(Rutas.cliente(cliente.id))),
      ),
    ),
    CelularDeOtraCartera() => Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Aviso(
        'Toca “$accion” otra vez para ${_esNuevo ? 'registrarlo' : 'guardarlo'} igual.',
        tipo: TipoDeAviso.aviso,
        titulo: 'Ya hay un cliente con este celular en otra cartera.',
      ),
    ),
  };

  Widget _campo(
    String clave,
    EstadoFormularioCliente estado, {
    required TextEditingController controlador,
    required String etiqueta,
    TextInputType? teclado,
    TextCapitalization mayusculas = TextCapitalization.none,
    Iterable<String>? pistas,
    FocusNode? foco,
    ValueChanged<String>? alCambiar,
    Widget? debajo,
    int lineas = 1,
    bool ultimo = false,
  }) {
    final lugar = _lugares[clave]!;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: KeyedSubtree(
        key: lugar.llave,
        child: CampoDeTexto(
          controller: controlador,
          etiqueta: etiqueta,
          keyboardType: teclado,
          textCapitalization: mayusculas,
          autofillHints: pistas,
          focusNode: foco ?? lugar.foco,
          onChanged: (texto) {
            _viewModel.cambioElCampo(clave);
            alCambiar?.call(texto);
          },
          lineas: lineas,
          error: estado.campos[clave],
          debajo: debajo,
          inputFormatters: teclado == TextInputType.number ? [FilteringTextInputFormatter.digitsOnly] : null,
          textInputAction: ultimo ? TextInputAction.done : TextInputAction.next,
          onSubmitted: ultimo ? (_) => _enviar() : null,
        ),
      ),
    );
  }
}
