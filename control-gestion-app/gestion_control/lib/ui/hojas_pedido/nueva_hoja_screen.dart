import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../data/models/cliente.dart';
import '../../data/models/hoja_pedido.dart';
import '../../data/models/producto.dart';
import '../core/formato.dart';
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

typedef _LugaresDeLinea = ({LugarDelFormulario producto, LugarDelFormulario cantidad, LugarDelFormulario descuento});

class _NuevaHojaScreenState extends ConsumerState<NuevaHojaScreen> {
  final _descuento = TextEditingController();
  final _ubicacion = (llave: GlobalKey(), foco: null);
  final _cliente = (llave: GlobalKey(), foco: FocusNode());
  final _agregar = (llave: GlobalKey(), foco: null);
  final _lugarDelDescuento = (llave: GlobalKey(), foco: FocusNode());
  final _lugaresDeLineas = <int, _LugaresDeLinea>{};

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
    for (final lugares in _lugaresDeLineas.values) {
      for (final lugar in [lugares.producto, lugares.cantidad, lugares.descuento]) {
        lugar.foco?.dispose();
      }
    }
    super.dispose();
  }

  _LugaresDeLinea _lugaresDe(int clave) => _lugaresDeLineas.putIfAbsent(
    clave,
    () => (
      producto: (llave: GlobalKey(), foco: FocusNode()),
      cantidad: (llave: GlobalKey(), foco: FocusNode()),
      descuento: (llave: GlobalKey(), foco: FocusNode()),
    ),
  );

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
      for (final (i, linea) in estado.lineas.indexed) ...[
        if (campos.containsKey('detalles[$i].productoId')) _lugaresDe(linea.clave).producto,
        if (campos.containsKey('detalles[$i].cantidad')) _lugaresDe(linea.clave).cantidad,
        if (campos.containsKey('detalles[$i].descuento')) _lugaresDe(linea.clave).descuento,
      ],
      if (campos.containsKey('detalles')) _agregar,
      if (campos.containsKey('descuento')) _lugarDelDescuento,
    ], (_) => true);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(nuevaHojaProvider);
    // Desde el primer cuadro, aunque los productos todavía carguen: si nadie la mira, la búsqueda del GPS se descarta.
    ref.watch(capturaDeUbicacionProvider(QueSeRegistra.venta));
    const titulo = Text('Nueva hoja de pedido');
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
            _LineaDeProducto(
              key: ValueKey(linea.clave),
              indice: i,
              linea: linea,
              estado: estado,
              lugares: _lugaresDe(linea.clave),
              alElegirProducto: enviando ? null : () => _elegirProducto(linea.clave),
              alQuitar: enviando ? null : () => _viewModel.quitarLinea(linea.clave),
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
          _Resumen(estado),
        ],
      ),
      bottomNavigationBar: BarraDeAcciones(
        arriba: [
          _TotalDeLaBarra(estado.total),
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

/// Una línea de la venta (8.6.5): el producto con su precio y su stock, la cantidad, el descuento por unidad y el
/// subtotal. Sus campos viven aquí: siguen a la línea aunque se quiten otras.
class _LineaDeProducto extends ConsumerStatefulWidget {
  const _LineaDeProducto({
    super.key,
    required this.indice,
    required this.linea,
    required this.estado,
    required this.lugares,
    required this.alElegirProducto,
    required this.alQuitar,
  });

  final int indice;
  final LineaDelFormulario linea;
  final EstadoNuevaHoja estado;
  final _LugaresDeLinea lugares;
  final VoidCallback? alElegirProducto;
  final VoidCallback? alQuitar;

  @override
  ConsumerState<_LineaDeProducto> createState() => _LineaDeProductoState();
}

class _LineaDeProductoState extends ConsumerState<_LineaDeProducto> {
  late final _cantidad = TextEditingController(text: widget.linea.cantidad);
  late final _descuento = TextEditingController(text: widget.linea.descuento);

  @override
  void dispose() {
    _cantidad.dispose();
    _descuento.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final linea = widget.linea;
    final producto = linea.producto;
    final campos = widget.estado.campos;
    final prefijo = 'detalles[${widget.indice}]';
    final numero = widget.indice + 1;
    final viewModel = ref.read(nuevaHojaProvider.notifier);
    final avisoDeStock = widget.estado.avisoDeStock(linea);
    // Con la letra grande, la cantidad y el descuento van uno debajo del otro: lado a lado, sus etiquetas se cortarían.
    final apilados = MediaQuery.textScalerOf(context).scale(16) > 22;

    final cantidad = KeyedSubtree(
      key: widget.lugares.cantidad.llave,
      child: CampoDeTexto(
        controller: _cantidad,
        etiqueta: 'Cantidad',
        keyboardType: tecladoDecimal,
        inputFormatters: [FormatoDecimal(decimales: 3)],
        textAlign: TextAlign.end,
        focusNode: widget.lugares.cantidad.foco,
        error: campos['$prefijo.cantidad'],
        onChanged: (texto) => viewModel.cambiarCantidad(linea.clave, texto),
      ),
    );
    final descuento = KeyedSubtree(
      key: widget.lugares.descuento.llave,
      child: CampoDeTexto(
        controller: _descuento,
        etiqueta: 'Descuento c/u (Bs)',
        keyboardType: tecladoDecimal,
        inputFormatters: [FormatoDecimal()],
        textAlign: TextAlign.end,
        focusNode: widget.lugares.descuento.foco,
        error: campos['$prefijo.descuento'],
        onChanged: (texto) => viewModel.cambiarDescuentoDeLinea(linea.clave, texto),
      ),
    );

    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Stack(
              clipBehavior: Clip.none,
              children: [
                KeyedSubtree(
                  key: widget.lugares.producto.llave,
                  child: CampoDeEleccion(
                    etiqueta: 'Producto $numero',
                    valor: producto?.descripcion,
                    ejemplo: 'Busca el producto…',
                    buscando: true,
                    focusNode: widget.lugares.producto.foco,
                    error: campos['$prefijo.productoId'],
                    ayuda: producto == null
                        ? null
                        : '${formatearMonto(producto.precio)} c/u · '
                              '${producto.sinStock ? 'Sin stock' : 'Stock: ${formatearCantidad(producto.stock)}'}',
                    alTocar: widget.alElegirProducto,
                  ),
                ),
                // El tacho va a la altura de la etiqueta. El ícono mide 24 dp, pero se toca en 48 dp.
                Positioned(
                  top: -14,
                  right: -12,
                  child: IconButton(
                    onPressed: widget.alQuitar,
                    tooltip: 'Quitar el producto $numero',
                    icon: const Icon(Icons.delete_outline, color: textoSecundario),
                  ),
                ),
              ],
            ),
            if (avisoDeStock != null) ...[const SizedBox(height: 4), _AvisoDeStock(avisoDeStock)],
            const SizedBox(height: 12),
            if (apilados) ...[
              cantidad,
              const SizedBox(height: 12),
              descuento,
            ] else
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(child: cantidad),
                  const SizedBox(width: 12),
                  Expanded(child: descuento),
                ],
              ),
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerRight,
              child: MergeSemantics(
                child: Wrap(
                  spacing: 8,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    const Text('Subtotal', style: TextStyle(color: textoSecundario)),
                    Text(
                      formatearMonto(linea.subtotal),
                      style: const TextStyle(fontWeight: FontWeight.w600, color: texto),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// "Solo hay N en stock.", en ámbar: avisa antes de confirmar, sin impedir guardar.
class _AvisoDeStock extends StatelessWidget {
  const _AvisoDeStock(this.texto);

  final String texto;

  @override
  Widget build(BuildContext context) {
    const color = Color(0xFF92400E);
    return Semantics(
      liveRegion: true,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const ExcludeSemantics(child: Icon(Icons.warning_amber_outlined, size: 18, color: color)),
          const SizedBox(width: 6),
          Expanded(
            child: Text(
              texto,
              style: const TextStyle(color: color, fontSize: 14, height: 20 / 14),
            ),
          ),
        ],
      ),
    );
  }
}

class _Resumen extends StatelessWidget {
  const _Resumen(this.estado);

  final EstadoNuevaHoja estado;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        child: Column(
          children: [
            _Fila('Subtotal', formatearMonto(estado.subtotal)),
            _Fila('Descuento', formatearMonto(estado.descuentoNumero)),
            const Divider(height: 16),
            _Fila('Total', formatearMonto(estado.total), fuerte: true),
          ],
        ),
      ),
    );
  }
}

class _Fila extends StatelessWidget {
  const _Fila(this.titulo, this.valor, {this.fuerte = false});

  final String titulo;
  final String valor;
  final bool fuerte;

  @override
  Widget build(BuildContext context) {
    final estilo = fuerte ? Theme.of(context).textTheme.titleMedium : null;
    return MergeSemantics(
      child: Padding(
        padding: const EdgeInsets.symmetric(vertical: 2),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Text(titulo, style: estilo ?? const TextStyle(color: textoSecundario)),
            ),
            Text(valor, style: estilo),
          ],
        ),
      ),
    );
  }
}

/// El total en la barra de abajo: se ve mientras se edita cualquier cantidad o descuento.
class _TotalDeLaBarra extends StatelessWidget {
  const _TotalDeLaBarra(this.total);

  final double total;

  @override
  Widget build(BuildContext context) {
    final estilo = Theme.of(context).textTheme.titleMedium;
    return Semantics(
      liveRegion: true,
      child: MergeSemantics(
        child: Row(
          children: [
            Expanded(child: Text('Total', style: estilo)),
            Text(formatearMonto(total), style: estilo),
          ],
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
