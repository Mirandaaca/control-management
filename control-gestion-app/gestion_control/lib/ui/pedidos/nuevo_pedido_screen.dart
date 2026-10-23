import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';

import '../../data/models/cliente.dart';
import '../../data/models/producto.dart';
import '../core/lineas/linea_de_producto.dart';
import '../core/lineas/lineas.dart';
import '../core/primer_problema.dart';
import '../core/router.dart';
import '../core/widgets/aviso.dart';
import '../core/widgets/barra_de_acciones.dart';
import '../core/widgets/campo_de_texto.dart';
import '../core/widgets/campos_numericos.dart';
import '../core/widgets/cargando.dart';
import '../core/widgets/error_con_reintento.dart';
import '../core/widgets/selectores.dart';
import 'nuevo_pedido_view_model.dart';
import 'pedido_screen.dart';

/// Registrar un pedido (1.1; `frmPedidoMovil`; 7.3.1, paso 2). No tiene diseño en el 8.6: sigue el de la venta (8.6.5),
/// con los textos del formulario del panel.
class NuevoPedidoScreen extends ConsumerStatefulWidget {
  const NuevoPedidoScreen({super.key});

  @override
  ConsumerState<NuevoPedidoScreen> createState() => _NuevoPedidoScreenState();
}

class _NuevoPedidoScreenState extends ConsumerState<NuevoPedidoScreen> {
  final _direccion = TextEditingController();
  final _descuento = TextEditingController();
  final _observaciones = TextEditingController();
  final _cliente = (llave: GlobalKey(), foco: FocusNode());
  final _lugarDeLaDireccion = (llave: GlobalKey(), foco: FocusNode());
  final _lugarDeLaFecha = (llave: GlobalKey(), foco: FocusNode());
  final _agregar = (llave: GlobalKey(), foco: null);
  final _lugarDelDescuento = (llave: GlobalKey(), foco: FocusNode());
  final _lugaresDeLineas = LugaresDeLasLineas();

  NuevoPedidoViewModel get _viewModel => ref.read(nuevoPedidoProvider.notifier);

  @override
  void dispose() {
    _direccion.dispose();
    _descuento.dispose();
    _observaciones.dispose();
    _cliente.foco.dispose();
    _lugarDeLaDireccion.foco.dispose();
    _lugarDeLaFecha.foco.dispose();
    _lugarDelDescuento.foco.dispose();
    _lugaresDeLineas.dispose();
    super.dispose();
  }

  Future<void> _elegirCliente() async {
    final elegido = await context.push<ClienteOpcion>(Rutas.elegirCliente);
    if (elegido != null) _viewModel.elegirCliente(elegido);
  }

  Future<void> _elegirProducto(int clave) async {
    final productos = ref.read(nuevoPedidoProvider).productos ?? const <Producto>[];
    final elegido = await context.push<Producto>(Rutas.elegirProducto, extra: productos);
    if (elegido != null) _viewModel.elegirProducto(clave, elegido);
  }

  Future<void> _agregarProducto() async => _elegirProducto(_viewModel.agregarLinea());

  // Sin fecha elegida, propone mañana: la mayoría de los pedidos son para otro día.
  Future<void> _elegirFecha() async {
    final hoy = ahoraEnBolivia();
    final hoyDia = DateTime(hoy.year, hoy.month, hoy.day);
    final fecha = await showDatePicker(
      context: context,
      initialDate: ref.read(nuevoPedidoProvider).fecha ?? hoyDia.add(const Duration(days: 1)),
      firstDate: hoyDia,
      lastDate: hoyDia.add(const Duration(days: 365)),
      helpText: 'Fecha de entrega',
    );
    if (fecha != null) _viewModel.elegirFecha(fecha);
  }

  Future<void> _elegirHora() async {
    final hora = await showTimePicker(
      context: context,
      initialTime: ref.read(nuevoPedidoProvider).hora ?? const TimeOfDay(hour: 9, minute: 0),
      helpText: 'Hora de entrega',
    );
    if (hora != null) _viewModel.elegirHora(hora);
  }

  Future<void> _registrar() async {
    FocusScope.of(context).unfocus();
    final pedido = await _viewModel.registrar(observaciones: _observaciones.text);
    if (!mounted) return;
    if (pedido == null) return _mostrarPrimerProblema();
    context.pushReplacement(Rutas.pedido(pedido.id), extra: AbrirPedido(mensaje: mensajeDePedido(pedido)));
  }

  void _mostrarPrimerProblema() {
    final estado = ref.read(nuevoPedidoProvider);
    final campos = estado.campos;
    mostrarPrimerProblema([
      if (campos.containsKey('clienteId')) _cliente,
      if (campos.containsKey('direccionEntrega')) _lugarDeLaDireccion,
      if (campos.containsKey('fechaEntregaEstimada')) _lugarDeLaFecha,
      ..._lugaresDeLineas.conError(estado.lineas, campos),
      if (campos.containsKey('detalles')) _agregar,
      if (campos.containsKey('descuento')) _lugarDelDescuento,
    ], (_) => true);
  }

  @override
  Widget build(BuildContext context) {
    final estado = ref.watch(nuevoPedidoProvider);
    // La dirección propuesta del cliente llega desde el view model: se escribe en el campo sin pisar lo que se teclea.
    ref.listen(nuevoPedidoProvider.select((e) => e.direccion), (_, direccion) {
      if (_direccion.text != direccion) _direccion.text = direccion;
    });
    const titulo = Text('Nuevo pedido');
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

    final enviando = estado.enviando;
    final fecha = estado.fecha;
    final hora = estado.hora;

    return Scaffold(
      appBar: AppBar(title: titulo),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
        children: [
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
          const SizedBox(height: 12),
          KeyedSubtree(
            key: _lugarDeLaDireccion.llave,
            child: CampoDeTexto(
              controller: _direccion,
              etiqueta: 'Dirección de entrega',
              ejemplo: 'Calle, número y una referencia…',
              ayuda: 'Se propone la del cliente: cámbiala si la entrega es en otro lugar.',
              lineas: 2,
              textCapitalization: TextCapitalization.sentences,
              autofillHints: const [AutofillHints.fullStreetAddress],
              focusNode: _lugarDeLaDireccion.foco,
              error: estado.campos['direccionEntrega'],
              onChanged: _viewModel.cambiarDireccion,
            ),
          ),
          const SizedBox(height: 12),
          KeyedSubtree(
            key: _lugarDeLaFecha.llave,
            child: CampoDeEleccion(
              etiqueta: 'Fecha de entrega',
              valor: fecha == null ? null : DateFormat('dd/MM/yyyy').format(fecha),
              ejemplo: 'Elige el día…',
              icono: Icons.calendar_today_outlined,
              focusNode: _lugarDeLaFecha.foco,
              error: estado.campos['fechaEntregaEstimada'],
              alTocar: enviando ? null : _elegirFecha,
            ),
          ),
          const SizedBox(height: 12),
          CampoDeEleccion(
            etiqueta: 'Hora de entrega',
            valor: hora == null ? null : '${_dos(hora.hour)}:${_dos(hora.minute)}',
            ejemplo: 'Elige la hora…',
            ayuda: 'En la hora de Bolivia.',
            icono: Icons.schedule,
            alTocar: enviando ? null : _elegirHora,
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
            ),
          ),
          const SizedBox(height: 12),
          CampoDeTexto(
            controller: _observaciones,
            etiqueta: 'Observaciones (opcional)',
            ejemplo: 'Por ejemplo, a qué hora atienden…',
            lineas: 3,
            textCapitalization: TextCapitalization.sentences,
            error: estado.campos['observaciones'],
            textInputAction: TextInputAction.newline,
            keyboardType: TextInputType.multiline,
          ),
          const SizedBox(height: 16),
          ResumenDeTotales(subtotal: estado.subtotal, descuento: estado.descuentoNumero, total: estado.total),
        ],
      ),
      bottomNavigationBar: BarraDeAcciones(
        arriba: [
          TotalDeLaBarra(estado.total),
          if (estado.errorAlEnviar != null)
            Aviso.deError(estado.errorAlEnviar!, accion: AccionDeAviso('Reintentar', _registrar)),
        ],
        principal: Accion('Registrar pedido', _registrar, enviando: enviando, textoEnviando: 'Registrando…'),
      ),
    );
  }
}

String _dos(int n) => n.toString().padLeft(2, '0');
