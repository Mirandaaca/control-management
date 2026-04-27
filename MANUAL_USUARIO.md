# Manual de Usuario — Control Gestión

**Versión:** 1.0  
**Sistema:** Control Gestión — Plataforma de Gestión de Promotores de Campo

---

## Acceso al sistema

Abrir el navegador en `http://localhost:5173` (o la dirección que indique el administrador).

Ingresar con el correo y contraseña asignados. El sistema redirige automáticamente al **Dashboard** tras el login.

> Las sesiones duran 8 horas. Al vencer, el sistema solicita volver a iniciar sesión.

---

## Roles de usuario

| Rol | Descripción |
|---|---|
| **Admin** | Acceso completo a todos los módulos y configuraciones |
| **Supervisor** | Gestión operativa, reportes, sin acceso a configuraciones del sistema |
| **Promotor** | Registro de clientes, hojas de pedido, eventos y cobranzas en campo |

---

## Dashboard

Pantalla principal con resumen del estado del sistema.

**KPIs mostrados:**
- Clientes activos en el sistema
- Hojas de pedido confirmadas en el mes actual
- Ventas totales del mes (Bs)
- Monto total por cobrar (cobranzas pendientes)
- Eventos abiertos sin resolver
- Productos con stock bajo el mínimo

**Gráficos:**
- Distribución de usuarios por rol
- Ventas por promotor en el mes actual
- Top 8 productos más vendidos
- Tendencia de ventas en los últimos 30 días

---

## Clientes

Gestión del padrón de clientes asignados a promotores.

### Ver listado
El listado muestra todos los clientes activos con nombre, zona, promotor asignado, días de revisita y próxima visita programada.

### Crear cliente
1. Hacer clic en **Nuevo cliente**
2. Completar los campos obligatorios: Nombre completo, Código, Zona
3. Activar **Tiene crédito** si el cliente opera a crédito e ingresar el límite
4. Asignar un **Promotor** responsable
5. Hacer clic en **Guardar**

### Editar cliente
Hacer clic en el botón **Editar** de la fila correspondiente, modificar los datos y guardar.

### Campos importantes
| Campo | Descripción |
|---|---|
| Código | Identificador interno único del cliente |
| NIT | Número de identificación tributaria |
| Días de revisita | Intervalo estándar entre visitas del promotor |
| Límite de crédito | Monto máximo autorizado para ventas a crédito |
| Saldo pendiente | Deuda actual del cliente (se actualiza automáticamente) |

---

## Hojas de Pedido (HP)

Las hojas de pedido son el registro principal de ventas realizadas en campo.

### Crear una HP
1. Ir a **Hojas de Pedido** → **Nueva HP**
2. Seleccionar el **cliente**
3. Seleccionar el **tipo de venta**: Contado o Crédito
   - Para crédito, el sistema valida automáticamente el límite disponible
4. Agregar los productos con su cantidad. Se pueden aplicar descuentos por línea
5. Aplicar un descuento global si corresponde
6. Hacer clic en **Guardar como borrador**

> La HP queda en estado **Borrador** hasta ser confirmada. El stock NO se descuenta en borrador.

### Confirmar una HP
1. Desde el listado, hacer clic en **Confirmar** en la HP deseada
2. El sistema ejecuta una transacción que:
   - Descuenta el stock de cada producto
   - Actualiza la fecha de última visita del cliente
   - Si es a crédito, crea automáticamente una **Cobranza**
3. La HP pasa a estado **Confirmada**

### Anular una HP *(solo Admin/Supervisor)*
1. Hacer clic en **Anular** e ingresar el motivo
2. La HP pasa a estado **Anulada**. El stock no se devuelve automáticamente — realizar un ajuste manual si es necesario

### Estados de una HP
| Estado | Descripción |
|---|---|
| Borrador | Creada pero no procesada |
| Confirmada | Stock descontado, venta registrada |
| Anulada | Cancelada por Admin/Supervisor |

---

## Inventario

Control de stock de productos.

### Ver stock actual
El listado muestra cada producto con su stock actual, mínimo, máximo y última actualización. Los productos con stock bajo el mínimo aparecen resaltados en rojo.

### Registrar ajuste de inventario
1. Hacer clic en **Ajuste** en el producto correspondiente
2. Seleccionar el tipo: **Entrada**, **Salida** o **Ajuste**
3. Ingresar la cantidad y una justificación
4. Confirmar

> Todos los movimientos quedan registrados en la bitácora de inventario visible en Reportes → Inventario.

---

## Pedidos

Seguimiento del despacho y entrega de mercadería.

### Crear pedido
Un pedido se crea manualmente o puede vincularse a una HP existente.

### Flujo de estados
```
Pendiente → En Proceso → En Ruta → Entregado
                                 ↘ Intento fallido → En Ruta (reintento)
```

### Cambiar estado
Usar los botones de acción disponibles según el estado actual del pedido. Cada cambio queda registrado en el historial con fecha, hora y usuario.

---

## Eventos

Registro de incidencias, novedades y visitas en campo.

### Crear evento
1. Hacer clic en **Nuevo evento**
2. Seleccionar el **tipo de evento** (incidencia, visita, etc.)
3. Vincular a un **cliente** y/o **pedido** si aplica
4. Escribir la descripción de la novedad
5. Guardar

### Estados de un evento
| Estado | Descripción |
|---|---|
| Registrado | Recién creado, pendiente de atención |
| En gestión | Asignado a un responsable |
| Escalado | Requiere atención urgente de supervisor |
| Cerrado | Resuelto |

---

## Cobranzas

Gestión del cobro de ventas realizadas a crédito.

### Cómo se crean las cobranzas
Las cobranzas se crean **automáticamente** cuando se confirma una Hoja de Pedido de tipo Crédito. No se crean manualmente.

### Ver cobranzas
El listado muestra todas las cobranzas con su estado, montos y fecha de vencimiento. Los KPIs en la parte superior muestran:
- Total por cobrar
- Cantidad de cobranzas pendientes
- Cantidad de cobranzas vencidas

### Registrar un pago
1. Hacer clic en **Pagar** en la cobranza correspondiente (o **Ver** → **Registrar pago**)
2. Ingresar el **monto** (no puede superar el saldo pendiente)
3. Seleccionar el **método de pago**: Efectivo, Transferencia, Cheque, Débito o Crédito
4. Ingresar la referencia o número de comprobante si aplica
5. Hacer clic en **Confirmar pago**

El sistema actualiza automáticamente el saldo del cliente.

### Estados de una cobranza
| Estado | Color | Descripción |
|---|---|---|
| Pendiente | Amarillo | Sin pagos registrados |
| Parcial | Azul | Con pagos parciales |
| Pagada | Verde | Cancelada completamente |
| Vencida | Rojo | Fecha de vencimiento superada |
| Incobrable | Gris | Marcada como irrecuperable (Admin/Supervisor) |

> La detección de cobranzas vencidas es **automática**: un proceso corre cada noche y actualiza el estado de las cobranzas no pagadas cuya fecha de vencimiento fue superada.

### Marcar como incobrable *(solo Admin/Supervisor)*
Usar el botón correspondiente desde el detalle de la cobranza. Se requiere ingresar una observación justificando la decisión.

---

## Rutas

Planificación de visitas diarias de los promotores.

### Crear una ruta
1. Ir a **Rutas** → **Nueva ruta**
2. Seleccionar la **fecha** y la **zona**
3. Agregar los clientes a visitar en el orden deseado
4. Guardar

### Mapa interactivo
El panel derecho muestra el mapa con:
- **Marcadores de colores** por estado de visita: azul (pendiente), verde (visitado), gris (ausente), amarillo (reprogramado)
- **Línea de ruta** conectando los puntos en orden
- El mapa se ajusta automáticamente para mostrar todos los clientes

### Registrar visitas durante la ruta
1. Hacer clic en **Iniciar** para activar la ruta
2. Al llegar a cada cliente, hacer clic en **Marcar** y seleccionar el estado: Visitado, Ausente o Reprogramado
3. Al finalizar todas las visitas, hacer clic en **Completar ruta**

---

## Reportes

Módulo de análisis y exportación de datos. *(Acceso: Admin y Supervisor)*

### Pestaña Ventas
Muestra el resumen de ventas del período seleccionado:
- KPIs: total vendido, número de HPs, ticket promedio, ventas crédito vs contado
- Tabla de ventas por promotor
- Top 10 productos más vendidos
- Tabla de ventas por día

### Pestaña Cobranzas
- KPIs: total emitido, total cobrado, saldo pendiente
- Distribución por estado
- Lista de cobranzas vencidas con días de retraso

### Pestaña Inventario
- Stock total y valor del inventario
- Listado de productos bajo stock mínimo
- Últimos 100 movimientos del inventario en los últimos 30 días

### Pestaña Promotores
Tabla comparativa del rendimiento de cada promotor: HPs generadas, ventas totales, ticket promedio, clientes asignados y eventos registrados.

### Pestaña Auditoría *(solo Admin)*
Bitácora de todas las operaciones POST/PUT/DELETE realizadas en el sistema, con usuario, módulo afectado, operación y dirección IP.

### Filtros de fecha
Usar los campos **Desde** y **Hasta** para definir el período de análisis. Hacer clic en **Aplicar** para actualizar los datos.

### Exportar a Excel
Las pestañas **Ventas** y **Cobranzas** tienen el botón **↓ Exportar Excel** que descarga un archivo `.xlsx` con los datos del período filtrado.

---

## Configuraciones *(solo Admin)*

Parámetros globales del sistema.

| Clave | Descripción | Valor por defecto |
|---|---|---|
| DiasVencimientoCredito | Días antes de que una cobranza de crédito venza | 30 |

Para modificar un valor: hacer clic en **Editar**, ingresar el nuevo valor y guardar.

---

## Usuarios *(solo Admin)*

Gestión de cuentas del sistema.

### Crear usuario
1. Ir a **Usuarios** → **Nuevo usuario**
2. Completar nombre, email y contraseña
3. Asignar el **rol**: Admin, Supervisor o Promotor
4. Guardar

### Activar / desactivar usuario
Usar el botón **Activar/Desactivar** en el listado. Un usuario inactivo no puede iniciar sesión pero sus datos históricos se conservan.

---

## Preguntas frecuentes

**¿Por qué no puedo registrar un pago en una cobranza?**  
La cobranza puede estar en estado Pagada o Incobrable (ambos son estados finales cerrados). También puede ocurrir si la cobranza tiene un total inválido — contactar al administrador.

**¿Por qué no aparece el promotor al crear un cliente?**  
Solo aparecen en el selector los usuarios activos con rol Promotor. Verificar que el usuario esté activo y con el rol correcto en el módulo Usuarios.

**¿Por qué la HP dice "Crédito insuficiente"?**  
El cliente no tiene crédito habilitado o el total de la HP supera el límite de crédito disponible (`LimiteCredito - SaldoPendiente`). Revisar el perfil del cliente.

**¿Puedo crear una HP con descuento mayor al subtotal?**  
No. El sistema rechaza HPs con total menor o igual a cero para evitar cobranzas con montos negativos.

**¿Cómo sé que el inventario se descontó correctamente?**  
En Reportes → Inventario → Últimos movimientos aparece un registro de tipo "Salida" vinculado a cada HP confirmada.

---

## Soporte

Para reportar problemas o solicitar asistencia, contactar al administrador del sistema.
