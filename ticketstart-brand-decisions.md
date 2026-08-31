# TICKETSTART — Decisiones de Identidad, UX y Alcance (MVP)

> **Estado:** Aprobado por el equipo — 2026-08-18
> **Propósito:** Fuente de verdad para el desarrollo del frontend. Las decisiones aquí registradas guían identidad de marca, sistema de color, experiencia de usuario y alcance de las vistas del MVP.
> **Regla de oro:** Todo lo que no se lista como "a definir" o "a modificar" en este documento **NO se toca**. El checkout, el sistema de roles/permisos y el navbar ya están aprobados tal como están.

---

## 1. Identidad de Marca

| Decisión | Valor |
|----------|-------|
| **Personalidad** | Joven, energética y divertida (opción *a*) |
| **Referencias** | Sin referencias externas definidas |
| **Tono de voz (UI)** | Voseo rioplatense, cálido y directo, consistente con lo ya manejado en el producto |

La marca se apoya en una paleta multicolor vibrante: la energía viene de la combinación de colores, no de un color dominante.

---

## 2. Sistema de Color

### 2.1 Paleta de marca (exacta, no se modifica)

| Nombre | Hex | RGB | Rol |
|--------|-----|-----|-----|
| Naranja | `#F78B2D` | (247, 139, 45) | Color de marca |
| Amarillo | `#F5C01F` | (245, 192, 31) | Color de marca |
| Verde | `#67CF65` | (103, 207, 101) | Color de marca |
| Cian | `#18C8DB` | (24, 200, 219) | Color de marca |
| Púrpura | `#B65DC2` | (182, 93, 194) | Color de marca |
| Gris Oscuro | `#4A4A4A` | (74, 74, 74) | Base neutra / texto principal |

### 2.2 Jerarquía de color

- **No hay color dominante.** Los 5 colores de marca tienen el mismo peso de identidad y se combinan buscando **armonía visual**, no un orden jerárquico fijo.
- Regla de combinación propuesta (a validar en diseño):
  - Gris Oscuro `#4A4A4A` como base neutra (fondo, texto, estructura).
  - Máximo 2-3 colores de marca por pantalla para no saturar.
  - Un color de acento por sección/categoría cuando aporte significado (ej. categorías de eventos, estados, roles).
- **Uso de los colores de marca:** fondos decorativos, ilustración, iconografía, badges/tags, grandes áreas, acentos gráficos. **No para texto normal** (ver 2.3).

### 2.3 Accesibilidad del color — WCAG AA (4.5:1 texto normal, 3:1 texto grande)

**Contraste medido de los colores de marca sobre blanco `#FFFFFF`:**

| Color de marca | Contraste | ¿Texto normal (4.5:1)? |
|----------------|-----------|-------------------------|
| Naranja `#F78B2D` | 2.42:1 | ❌ FALLA |
| Amarillo `#F5C01F` | 1.69:1 | ❌ FALLA |
| Verde `#67CF65` | 1.96:1 | ❌ FALLA |
| Cian `#18C8DB` | 2.03:1 | ❌ FALLA |
| Púrpura `#B65DC2` | 3.94:1 | ❌ FALLA |
| Gris Oscuro `#4A4A4A` | 8.86:1 | ✅ PASA |

**Blanco como texto sobre cada color de marca:** ninguno pasa 4.5:1 → **no usar texto blanco sobre los colores de marca brillantes**.

**Gris Oscuro como texto sobre cada color de marca:**

| Sobre | Contraste | ¿Texto normal? | ¿Texto grande (3:1)? |
|-------|-----------|----------------|----------------------|
| Naranja `#F78B2D` | 3.67:1 | ❌ | ✅ |
| Amarillo `#F5C01F` | 5.25:1 | ✅ | ✅ |
| Verde `#67CF65` | 4.51:1 | ✅ | ✅ |
| Cian `#18C8DB` | 4.36:1 | ❌ (borde) | ✅ |
| Púrpura `#B65DC2` | 2.25:1 | ❌ | ❌ |

### 2.4 Variantes oscuras (colores modificados — DOCUMENTADOS)

Para que la marca cumpla WCAG AA en texto y botones, se definen **variantes oscuras por color**. Los colores de marca originales NO se modifican; estas variantes son tokens adicionales del sistema de color.

| Color de marca original | Variante oscura (texto/botón) | Contraste sobre blanco | Uso |
|-------------------------|-------------------------------|------------------------|-----|
| Naranja `#F78B2D` | **`#B45309`** | 5.02:1 ✅ | Texto de marca sobre fondos claros; fondo de botón primario con texto blanco |
| Amarillo `#F5C01F` | **`#6B5300`** | 7.34:1 ✅ | Texto de marca; fondo de botón con texto blanco |
| Verde `#67CF65` | **`#166534`** | 7.13:1 ✅ | Texto de marca; fondo de botón con texto blanco |
| Cian `#18C8DB` | **`#0B6170`** | 7.10:1 ✅ | Texto de marca; fondo de botón con texto blanco |
| Púrpura `#B65DC2` | **`#6A2176`** | 10.04:1 ✅ | Texto de marca; fondo de botón con texto blanco |
| Gris Oscuro `#4A4A4A` | *(sin variante)* | 8.86:1 ✅ | Texto principal sobre blanco |

**Reglas resultantes:**
- **Texto normal** (≥ 4.5:1): Gris Oscuro `#4A4A4A` sobre blanco, o variante oscura de marca sobre blanco.
- **Botones primarios:** fondo = variante oscura + texto blanco (todas las variantes pasan 4.5:1). Alternativa válida: fondo = color de marca + texto Gris Oscuro **solo donde pase** (Amarillo y Verde).
- **Texto grande** (≥24px, o ≥18.66px bold): permite Gris Oscuro sobre Naranja/Cian (3:1), pero se prefiere usar las variantes oscuras.
- **Estados interactivos (hover/pressed):** usar variantes oscuras, nunca aclarar sobre blanco.
- **Foco visible:** anillo de foco usando variante oscura del acento de la sección o doble anillo (gris + color).

> 📌 **Recordatorio:** cualquier color adicional que se modifique o agregue en el futuro debe registrarse en este documento con su contraste medido.

### 2.5 Modo oscuro

- **Decisión:** MVP en **modo claro solamente**.
- **Dejado documentado para futuro:** la arquitectura de tokens debe permitir agregar modo oscuro sin refactor (tokens semánticos, no valores de color hardcodeados). Cuando se agregue, las variantes de marca en oscuro se documentarán en esta misma sección.

---

## 3. Usuarios y Roles

| Rol | ¿En MVP? | Notas |
|-----|----------|-------|
| **Comprador** (público) | ✅ | Compra como invitado; ve catálogo, detalle, compra, mis compras/tickets |
| **Organizador** | ✅ | Gestiona sus eventos (dashboard, métricas, creación/edición); también escanea QR como un staff más (StaffScan) |
| **Staff / Escáner** | ✅ | Escaneo QR en el evento (StaffScan) |
| **Admin** | ✅ | Panel admin, compras, usuarios |

- **El sistema de roles y permisos actual NO se modifica.** Las vistas y el navbar se atan lógicamente a los permisos de cada usuario, **tal cual está hoy** (`ProtectedRoute` / `RoleGuard`).
- **Actualización (2026-08-31):** el Organizador ahora también accede a la sección de escaneos (StaffScan), con el mismo alcance que un Staff. El resto de los permisos queda intacto.

---

## 4. Flujo de Compra

- **Checkout como invitado (guest): SÍ.**
- **NO tocar:** el flujo de checkout actual (Checkout → CheckoutReturn → CheckoutSuccess) ya está aprobado y bien hecho. No se rediseña ni se reestructura.

---

## 5. Vistas del MVP (alcance)

Todas las vistas actuales se mantienen; la navegación se hace desde el **navbar, visible según el rol del usuario** (no se modifica la lógica actual).

| Vista | Rol | Nota |
|-------|-----|------|
| Home / Catálogo (EventList) | Todos (público) | |
| Detalle de evento (EventDetail) | Todos (público) | |
| Checkout / Retorno / Éxito | Comprador (guest) | Intacto |
| Mis compras / Tickets (con QR) | Comprador | |
| Login | Todos | |
| OrganizerDashboard / EventDetail / EventMetrics / EventNew | Organizador | |
| StaffScan | Staff, Organizador | |
| AdminPanel / AdminPurchases | Admin | |
| TicketLookup | Público/staff | |
| FAQ / NotFound | Todos | |

---

## 6. Dispositivos y Responsive

- **Mobile-first:** un buen porcentaje del tráfico entra por celular → el diseño es **responsive** desde el inicio.
- **No es PWA:** no se necesita instalación ni tickets offline, porque los tickets llegan al **mail de la persona**.

---

## 7. Idioma y Localización

- **UI en español rioplatense (voseo)**, como se viene manejando en el producto.
- Sin i18n en el MVP; textos externalizados donde sea barato para facilitar un futuro cambio.

---

## 8. Accesibilidad (WCAG)

- **Estándar objetivo: WCAG AA** (2.2) desde el inicio.
  - Texto normal ≥ 4.5:1, texto grande ≥ 3:1.
  - Foco visible, navegación por teclado, etiquetas/ARIA en controles.
  - El color nunca es el único canal de información (íconos + texto junto al color).
- Los colores modificados para cumplir el estándar quedan **documentados en 2.4**.

---

## 9. Lenguaje Visual y Tipografía

| Decisión | Valor |
|----------|-------|
| **Tipografía display** (títulos, logo, números grandes) | **Geométrica bold** — letras redondas y gruesas tipo afiche de festival (candidatas: Poppins, Baloo, Sora). Transmite energía y juventud. |
| **Tipografía de cuerpo** (textos, formularios, legibilidad diaria) | **Sans humanista** — Inter o similar. Máxima legibilidad en pantalla, segura para producto. |
| **Geometría** | **Mixta**: cards redondeadas (radios generosos) + botones pill + inputs suaves. Cada elemento con su forma, variedad controlada. |
| **Lenguaje visual de superficies** | **Confetti**: el color es protagonista en grandes áreas, gradientes y bloques. El color NO se usa para texto (aplica todo lo de 2.3/2.4). |
| **Hero de Home** | **Categorías coloridas + eventos**: chips/badges de categorías (cada una con su color de marca) arriba, grid de eventos destacados debajo. |
| **Movimiento** | **Micro-interacciones sutiles**: hover y transiciones de 150-300ms, estados claros. Siempre respetando `prefers-reduced-motion`. |

> **Nota de coherencia:** "Confetti" define la presencia del color en superficies (grandes áreas, gradientes, bloques), NO animación. El movimiento se mantiene deliberadamente sutil para no competir con el color.

---

## 10. Fuera de Alcance (MVP)

- Modo oscuro (documentado para futuro, ver 2.5).
- PWA / tickets offline.
- Rediseño del checkout o del sistema de roles/permisos.
- i18n / multi-idioma.

---

## Próximos Pasos

1. Definir la escala tipográfica y pesos exactos (display vs cuerpo).
2. Convertir este documento en tokens de diseño (CSS variables: color, tipografía, radios, sombras, spacing).
3. Definir las categorías de eventos y su asignación de color de marca (para los chips de la Home).
4. Validar la regla de combinación de color con una pantalla real (Home o EventDetail).
5. Diseñar e implementar las vistas siguiendo el orden del alcance del MVP.