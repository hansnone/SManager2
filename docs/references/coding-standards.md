# Estándares Universales de Diseño y Escritura de Código

## 1. Reglas Universales y Educativas (Cualquier lenguaje)
- **Código Educativo:** Incluye comentarios sencillos en español encima de cada función, clase y paso importante explicando qué hace y por qué.
- **Cohesión y Responsabilidad Única (SRP):** Cada archivo debe tener una responsabilidad clara y bien definida. Como guía orientativa profesional, procura que los archivos no superen las 400-500 líneas; si un archivo sobrepasa ese tamaño o asume más de una responsabilidad, divídelo en módulos o componentes más pequeños.
- **Nombres claros:** Usa nombres descriptivos en variables y funciones (ej. `calcularTotal()` en lugar de `calc()`).
- **Tipado seguro:** Define siempre el tipo de datos. En TypeScript está prohibido usar `any`.
- **Manejo de errores:** Captura siempre los posibles fallos (con `try/catch` o equivalente) mostrando mensajes comprensibles para el usuario.
- **Compatibilidad Multi-Sistema:** Escribe rutas de archivos que funcionen igual en Windows, macOS y Linux.

## 2. Reglas para Interfaces Gráficas (Angular / Web / GUI)
- **Organización en `src/app/` (Angular):** Divide en `core/` (datos y servicios globales), `shared/` (componentes reutilizables) y `features/` (pantallas principales).
- **Estilo Sobrio Azul Marino:** Importa y utiliza las variables y clases de `themes/sober-light.css` (tema claro, acento `#0F2942`).
- **Los 4 Estados Visuales:** Toda pantalla con datos debe contemplar los estados: Cargando, Vacío, Error y Éxito.

## 3. Guía de UX Writing y Diseño Humano (Anti-IA)
Para que el software tenga un acabado auténtico, humano y profesional, evita cualquier rastro o "tic" típico de generación por Inteligencia Artificial:

- **Botones directos y naturales (Infinitivo + Objeto corto):**
  - *Evitar (estilo IA):* `"Ejecutar Procesamiento de Datos"`, `"Inicializar Configuración"`, `"Enviar Formulario de Registro"`, `"¡Comenzar Ahora!"`.
  - *Usar (estilo humano):* `"Guardar cambios"`, `"Crear cliente"`, `"Exportar CSV"`, `"Añadir fila"`, `"Cancelar"`, `"Eliminar"`.
- **Uso correcto de mayúsculas en español (Sentence case):**
  - En español solo se escribe con mayúscula inicial la primera palabra de botones, títulos, pestañas o etiquetas (salvo nombres propios o siglas).
  - *Evitar:* `"Añadir Nuevo Registro De Facturación"`.
  - *Usar:* `"Añadir registro de facturación"` o `"Nueva factura"`.
- **Prohibición total de emojis en la interfaz:**
  - Nunca uses emojis (`🚀`, `✨`, `📊`, `✅`, `⚠️`, `🔍`, `🎉`) en botones, títulos, menús, tablas ni alertas. Si se requiere un icono, usa texto limpio o iconos SVG monocromáticos discretos.
- **Cero frases de relleno o "marketing de IA":**
  - No añadas subtítulos genéricos ni frases grandilocuentes debajo de cada encabezado (ej. *"Gestiona eficientemente todos tus recursos desde este panel centralizado"* o *"Bienvenido a tu plataforma integral"*). Las herramientas profesionales van directo al contenido.
- ** Ayudas en campos (Placeholders) y mensajes de estado humanos:**
  - *Placeholders breves y realistas:* Usa `"Ej. marta.lopez@empresa.es"` o `"Buscar por nombre o NIF..."` en lugar de `"Introduzca aquí su dirección de correo electrónico válida"`.
  - *Estados vacíos naturales:* Usa `"Aún no hay facturas registradas"` en lugar de `"No existen registros disponibles en la base de datos actualmente"`.
  - *Errores comprensibles:* Usa `"No se pudo guardar el archivo. Comprueba que no esté abierto en otro programa."` en lugar de `"Error 500: Fallo en la operación de escritura"`.
