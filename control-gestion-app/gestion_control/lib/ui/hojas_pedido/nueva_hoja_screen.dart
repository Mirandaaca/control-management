import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/cliente.dart';
import '../../data/models/hoja_pedido.dart';
import '../../data/models/producto.dart';
import '../core/lineas/linea_de_producto.dart';
import '../core/lineas/lineas.dart';
import '../core/primer_problema.dart';
import '../core/router.dart';
import '../core/tema.dart';
import '../core/ubicacion/captura_de_ubicacion.dart';
import '../core/ubicacion/tarjeta_de_ubicacion.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/campos_numericos.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/segmentado.dart';
import '../core/widgets/selectores.dart';
import '../core/widgets/titulo_de_barra.dart';
import 'nueva_hoja_view_model.dart';
import 'venta.dart';

/// Registrar una hoja de pedido (2.1; 8.6.5): la ubicación sola, el cliente, los productos, contado o crédito y el total.
/// "Guardar" la deja en borrador y "Confirmar venta" la cierra en un solo paso.
class NuevaHojaScreen extends ConsumerStatefulWidget {
  const NuevaHojaScreen({super.key, this.cliente});

  /// Abierta desde un cliente, ya viene elegido.
  final ClienteOpcion? cliente;

  @override
  ConsumerState<NuevaHojaScreen> createState() => _NuevaHojaScreenState();
}

class _NuevaHojaScreenState extends ConsumerState<NuevaHojaScreen> {
  final _descuento = TextEditingController();
  final _ubicacion = (llave: GlobalKey(), foco: null);
  final _cliente = (llave: GlobalKey(), foco: FocusNode());
  final _agregar = (llave: GlobalKey(), foco: null);
  final _lugarDelDescuento = (llave: GlobalKey(), foco: FocusNode());
  final _lugaresDeLineas = LugaresDeLasLineas();

  /// Lo último que se envió: "Reintentar" lo repite, sin volver a pedir la confirmación.
  var _ultimo = QueSeEnvia.guardar;

  NuevaHojaViewModel get _viewModel => ref.read(nuevaHojaProvider.notifier);

  @override
  void initState() {
    super.initState();
    Future.microtask(() {
      ref.read(capturaDeUbicacionProvider(QueSeRegistra.venta).notifier).capturar();
      if (widget.cliente != null) _viewModel.elegirCliente(widget.cliente!);
    });
  }

  @override
  void dispose() {
    _descuento.dispose();
    _cliente.foco.dispose();
    _lugarDelDescuento.foco.dispose();
    _lugaresDeLineas.dispose();
    super.dispose();
  }

  Future<void> _elegirCliente() async {
    final elegido = await context.push<ClienteOpcion>(Rutas.elegirCliente);
    if (elegido != null) _viewModel.elegirCliente(elegido);
  }

  Future<void> _elegirProducto(int clave) async {
    final productos = ref.read(nuevaHojaProvider).productos ?? const <Producto>[];
    final elegido = await context.push<Producto>(Rutas.elegirProducto, extra: productos);
    if (elegido != null) _viewModel.elegirProducto(clave, elegido);
  }

  Future<void> _agregarProducto() async => _elegirProducto(_viewModel.agregarLinea());

  Future<void> _confirmar() async {
    FocusScope.of(context).unfocus();
    if (!_viewModel.revisar()) return _mostrarPrimerProblema();
    final estado = ref.read(nuevaHojaProvider);
    final confirmado = await confirmarVenta(
      context,
      cliente: estado.cliente!.nombreCompleto,
      productos: estado.lineas.length,
      unidades: estado.lineas.fold(0, (suma, l) => suma + l.cantidadNumero),
      tipo: estado.tipoVenta,
      total: estado.total,
    );
    if (confirmado) await _enviar(QueSeEnvia.confirmar);
  }

  Future<void> _enviar(QueSeEnvia que) async {
    FocusScope.of(context).unfocus();
    _ultimo = que;
    final hoja = await _viewModel.enviar(que);
    if (!mounted) return;
    if (hoja == null) return _mostrarPrimerProblema();
    context.pushReplacement(Rutas.hojaPedido(hoja.id), extra: AbrirHoja(mensaje: mensajeDeVenta(hoja)));
  }

  void _mostrarPrimerProblema() {
    final estado = ref.read(nuevaHojaProvider);
    final campos = estado.campos;
    mostrarPrimerProblema([
      if (estado.sinUbicacion) _ubicacion,
      if (campos.containsKey('clienteId')) _cliente,
      ..._lugaresDeLineas.conError(estado.lineas, campos),
      if (campos.containsKey('detalles')) _agregar,
      if (campos.containsKey('descuento')) _lugarDelDescuento,
    ], (_) => true);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(nuevaHojaProvider);
    // Desde el primer cuadro, aunque los productos todavía carguen: si nadie la mira, la búsqueda del GPS se descarta.
    ref.watch(capturaDeUbicacionProvider(QueSeRegistra.venta));
    const titulo = TituloDeBarra('Nueva hoja de pedido');
    if (estado.productos == null) {
      return Scaffold(
        appBar: AppBar(title: titulo),
        body: estado.errorDeCarga != null
            ? ErrorConReintento.de(
                estado.errorDeCarga!,
                reintentando: estado.reintentando,
                alReintentar: _viewModel.cargarProductos,
              )
            : const Cargando('Cargando los productos…'),
      );
    }

    final enviando = estado.enviando != null;
    String textoEnviando(String texto) => estado.esperandoUbicacion ? 'Buscando la ubicación…' : texto;

    return Scaffold(
      appBar: AppBar(title: titulo),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
          KeyedSubtree(
            key: _ubicacion.llave,
            child: const TarjetaDeUbicacion(registro: QueSeRegistra.venta),
          ),
          const SizedBox(height: 16),
          KeyedSubtree(
            key: _cliente.llave,
            child: CampoDeEleccion(
              etiqueta: 'Cliente',
              valor: estado.cliente?.texto,
              ejemplo: 'Busca el cliente…',
              buscando: true,
              error: estado.campos['clienteId'],
              focusNode: _cliente.foco,
              alTocar: enviando ? null : _elegirCliente,
            ),
          ),
          const SizedBox(height: 20),
          Semantics(
            header: true,
            container: true,
            child: Text('Productos', style: Theme.of(context).textTheme.titleMedium),
          ),
          const SizedBox(height: 8),
          for (final (i, linea) in estado.lineas.indexed) ...[
            LineaDeProducto(
              key: ValueKey(linea.clave),
              indice: i,
              linea: linea,
              campos: estado.campos,
              lugares: _lugaresDeLineas.de(linea.clave),
              avisoDeStock: estado.avisoDeStock(linea),
              alElegirProducto: enviando ? null : () => _elegirProducto(linea.clave),
              alQuitar: enviando ? null : () => _viewModel.quitarLinea(linea.clave),
              alCambiarCantidad: (texto) => _viewModel.cambiarCantidad(linea.clave, texto),
              alCambiarDescuento: (texto) => _viewModel.cambiarDescuentoDeLinea(linea.clave, texto),
            ),
            const SizedBox(height: 12),
          ],
          KeyedSubtree(
            key: _agregar.llave,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (estado.campos['detalles'] != null) ...[
                  ErrorDeCampo(estado.campos['detalles']!),
                  const SizedBox(height: 8),
                ],
                OutlinedButton.icon(
                  onPressed: enviando ? null : _agregarProducto,
                  icon: const Icon(Icons.add),
                  label: const Text('Agregar producto'),
                ),
              ],
            ),
          ),
          const SizedBox(height: 20),
          Segmentado(
            etiqueta: 'Tipo de venta',
            opciones: const [(TipoVenta.contado, 'Contado'), (TipoVenta.credito, 'Crédito')],
            valor: estado.tipoVenta,
            alCambiar: _viewModel.elegirTipo,
          ),
          if (estado.tipoVenta == TipoVenta.credito) ...[
            const SizedBox(height: 4),
            const Text('La deuda vence a los 30 días.', style: TextStyle(color: textoSecundario)),
          ],
          const SizedBox(height: 16),
          KeyedSubtree(
            key: _lugarDelDescuento.llave,
            child: CampoDeTexto(
              controller: _descuento,
              etiqueta: 'Descuento sobre el total (Bs, opcional)',
              keyboardType: tecladoDecimal,
              inputFormatters: [FormatoDecimal()],
              textAlign: TextAlign.end,
              focusNode: _lugarDelDescuento.foco,
              error: estado.campos['descuento'],
              onChanged: _viewModel.cambiarDescuento,
              textInputAction: TextInputAction.done,
            ),
          ),
          const SizedBox(height: 16),
          ResumenDeTotales(subtotal: estado.subtotal, descuento: estado.descuentoNumero, total: estado.total),
        ],
      ),
      bottomNavigationBar: BarraDeAcciones(
        arriba: [
          TotalDeLaBarra(estado.total),
          if (estado.errorAlEnviar != null)
            Aviso.deError(estado.errorAlEnviar!, accion: AccionDeAviso('Reintentar', () => _enviar(_ultimo))),
        ],
        secundaria: Accion(
          'Guardar',
          enviando ? null : () => _enviar(QueSeEnvia.guardar),
          enviando: estado.enviando == QueSeEnvia.guardar,
          textoEnviando: textoEnviando('Guardando…'),
        ),
        principal: Accion(
          'Confirmar venta',
          enviando ? null : _confirmar,
          enviando: estado.enviando == QueSeEnvia.confirmar,
          textoEnviando: textoEnviando('Confirmando…'),
        ),
      ),
    );
  }
}

/// Cómo se abre el detalle de una HP: con el aviso de lo que pasó, o pidiendo la confirmación de un borrador.
class AbrirHoja {
  const AbrirHoja({this.mensaje, this.confirmar = false});

  final String? mensaje;
  final bool confirmar;
}
