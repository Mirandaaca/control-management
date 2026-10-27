import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/models/pagina.dart';
import '../../data/services/error_de_api.dart';
import 'widgets/aviso.dart';
import 'widgets/error_con_reintento.dart';

class EstadoDeLista<T> {
  const EstadoDeLista({
    this.items,
    this.total = 0,
    this.pagina = 0,
    this.cargando = false,
    this.cargandoMas = false,
    this.error,
    this.fallaAlCargarMas = false,
  });

  /// Null hasta que llega la primera página.
  final List<T>? items;
  final int total;
  final int pagina;

  /// Vuelve a pedir la primera página (otra búsqueda, otro filtro): lo de antes sigue a la vista mientras tanto.
  final bool cargando;
  final bool cargandoMas;
  final ErrorDeApi? error;

  /// El error fue de "Cargar más": "Reintentar" pide esa página otra vez, no la primera.
  final bool fallaAlCargarMas;

  bool get hayMas => items != null && items!.length < total;
}

/// Una lista larga de la API, de a 25, con "Cargar más" (docs/diseno.md, "Listas"). Cada lista dice cómo pide una
/// página con sus filtros; al cambiar un filtro vuelve a la primera.
abstract class ListaPaginada<T> extends Notifier<EstadoDeLista<T>> {
  // Cada pedido lleva su número: si el promotor sigue escribiendo, la respuesta de una búsqueda vieja se descarta.
  var _pedido = 0;

  Future<Pagina<T>> pedir(int pagina);

  @override
  EstadoDeLista<T> build() {
    Future.microtask(cargar);
    return EstadoDeLista<T>(cargando: true);
  }

  Future<void> cargar() async {
    final pedido = ++_pedido;
    state = EstadoDeLista<T>(items: state.items, total: state.total, pagina: state.pagina, cargando: true);
    try {
      final pagina = await pedir(1);
      if (!ref.mounted || pedido != _pedido) return;
      state = EstadoDeLista<T>(items: pagina.items, total: pagina.total, pagina: 1);
    } on ErrorDeApi catch (e) {
      if (!ref.mounted || pedido != _pedido) return;
      state = EstadoDeLista<T>(items: state.items, total: state.total, pagina: state.pagina, error: e);
    }
  }

  Future<void> cargarMas() async {
    final items = state.items;
    if (items == null || state.cargandoMas || state.cargando || !state.hayMas) return;
    final pedido = _pedido;
    state = EstadoDeLista<T>(items: items, total: state.total, pagina: state.pagina, cargandoMas: true);
    try {
      final pagina = await pedir(state.pagina + 1);
      if (!ref.mounted || pedido != _pedido) return;
      state = EstadoDeLista<T>(items: [...items, ...pagina.items], total: pagina.total, pagina: pagina.numero);
    } on ErrorDeApi catch (e) {
      if (!ref.mounted || pedido != _pedido) return;
      state = EstadoDeLista<T>(
        items: items,
        total: state.total,
        pagina: state.pagina,
        error: e,
        fallaAlCargarMas: true,
      );
    }
  }
}

/// Después de guardar algo desde un formulario: si la lista de ese tipo sigue abierta debajo, vuelve a pedir la primera
/// página, con sus filtros. El formulario se reemplaza por la ficha (`pushReplacement`), y en go_router eso no cumple la
/// promesa del `push` de la lista: sin este aviso, al volver mostraba lo de antes hasta deslizar (prueba 10.1.5).
void recargarSiEstaAbierta<T>(WidgetRef ref, NotifierProvider<ListaPaginada<T>, EstadoDeLista<T>> lista) {
  if (ref.exists(lista)) ref.read(lista.notifier).cargar();
}

/// Lo que va debajo de los filtros de una lista: la carga, el error con "Reintentar", la lista vacía o los registros, y
/// "Cargar más" si quedan. [vacio] es la lista sin nada; [sinCoincidencias], la búsqueda sin resultados.
List<Widget> cuerpoDeLista<T>({
  required EstadoDeLista<T> estado,
  required String textoDeCarga,
  required VoidCallback alReintentar,
  required VoidCallback alCargarMas,
  required Widget vacio,
  required Widget sinCoincidencias,
  required bool hayFiltros,
  required Widget Function(T item) item,
}) {
  final items = estado.items;
  return [
    if (estado.cargando && items != null) const LinearProgressIndicator(),
    if (estado.error != null) ...[
      Aviso.deError(estado.error!),
      const SizedBox(height: 12),
      BotonReintentar(alReintentar: estado.fallaAlCargarMas ? alCargarMas : alReintentar),
      const SizedBox(height: 16),
    ],
    if (items == null && estado.error == null)
      Padding(
        padding: const EdgeInsets.symmetric(vertical: 32),
        child: Semantics(
          liveRegion: true,
          child: Column(
            children: [
              const CircularProgressIndicator(),
              const SizedBox(height: 16),
              Text(textoDeCarga, style: const TextStyle(color: Color(0xFF64748B))),
            ],
          ),
        ),
      )
    else if (items != null && items.isEmpty && !estado.cargando)
      hayFiltros ? sinCoincidencias : vacio
    else if (items != null) ...[
      for (final registro in items) ...[item(registro), const SizedBox(height: 12)],
      if (estado.hayMas && estado.error == null)
        OutlinedButton(
          onPressed: estado.cargandoMas ? null : alCargarMas,
          child: Text(estado.cargandoMas ? 'Cargando…' : 'Cargar más'),
        ),
    ],
  ];
}
