# Changelog

## 1.1.0

### Novedades
- **Login de Microsoft** activado con el Client ID aprobado por Mojang (cuentas premium).
- **Detalles de proyectos** (mods, modpacks, resource packs, shaders): descripción (Markdown/HTML de Modrinth y
  CurseForge dibujado con un `RichTextBox` nativo, sin WebView), galería de imágenes con caché en `data\cache\img` y
  enlaces oficiales. Se abre con el botón **Detalles** o con doble clic (también en los mods instalados).
- **Configuración** reorganizada en secciones y con opciones nuevas: confirmar al eliminar instancias, flags de JVM
  optimizados (sí/no), argumentos de JVM globales, volumen de la música, fuente de contenido por defecto, abrir la
  carpeta de datos, limpiar la caché y sección "Acerca de".
- **Tema Black & White** (oscuro y claro).
- **Java opcionales:** GraalVM 21 e IBM Semeru (OpenJ9) 8/17/21, descarga y activación desde Configuración, elección por
  instancia en Opciones de lanzamiento. Detección de GraalVM/Semeru en el equipo y flags de JVM propios para OpenJ9.

### Correcciones
- **Mods y contenido** no se podía tocar sin instancias: ahora siempre está activo y ofrece crear una.
- Quedaba el cartel "Minecraft se está iniciando" abajo a la izquierda después de cerrar el juego.
- Los botones deshabilitados ahora se ven apagados y los botones muestran hover; los menús contextuales usan los
  colores del tema.
- Configuración ya no se rompe si `DefaultRamMB` en `settings.json` está fuera del rango permitido.
- `settings.json`, `instance.json`, el índice de mods y los índices de Minecraft se guardan de forma atómica (un cierre a
  la mitad ya no los deja truncados).
- La descarga de Java se hace a una carpeta temporal y se verifica: si se corta no queda un Java roto ni un `.zip` suelto.
- Aviso cuando el Java elegido es más viejo que el que pide la versión de Minecraft.
