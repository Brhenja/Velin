# Changelog

## 1.2.0

Versión de comodidad e infraestructura.

### Novedades
- **Un solo `Velin.exe`:** el programa se publica en un único ejecutable (junto a la carpeta `Assets\`). El zip portable
  trae una sola carpeta `Velin\`, y el instalador borra las `.dll` sueltas de versiones anteriores. Cada release incluye
  `SHA256SUMS.txt`.
- **Aviso de actualización:** al iniciar (como mucho cada 12 horas) y desde **Configuración → Acerca de** Velin consulta
  si hay una versión nueva en GitHub. Muestra las notas y, con **Descargar último .exe**, baja el instalador, verifica su
  SHA256 y lo ejecuta. En la versión portable abre la página de descarga. Se puede desactivar en Configuración u omitir una
  versión.
- **Acceso directo / `--jugar`:** en el menú de la instancia, **Crear acceso directo...** deja un `.lnk` (escritorio o menú
  Inicio) que abre Velin y lanza esa instancia directamente (`Velin.exe --jugar "nombre"`).
- **Primer inicio guiado:** la primera vez que abrís Velin un asistente permite elegir idioma (detectado por el sistema),
  tema, nombre de usuario, aviso de actualizaciones, pantalla de inicio, música y sonido de click.
- **13 idiomas:** español, inglés, portugués (Brasil), ruso, alemán, francés, italiano, chino simplificado, japonés,
  turco, polaco, ucraniano y coreano. Los idiomas son archivos JSON en `Assets\Lang\`; se pueden agregar o reemplazar
  poniendo un `<código>.json` en `data\lang\` (las claves que falten caen a inglés). Los idiomas que las fuentes
  medievales no cubren usan una fuente normal de Windows (coreano, japonés y chino, una que tenga sus caracteres).
- **Filtros en los buscadores:** orden (relevancia, descargas, más nuevos, actualizados), categoría, versión de Minecraft
  (editable) y loader, para Modrinth y CurseForge.
- **Actualizar modpacks de Modrinth:** menú de la instancia → **Actualizar modpack...** Conserva mundos, opciones y mods
  propios, descarga solo los archivos que cambiaron, mueve a `modpack-update-trash\` lo que el modpack nuevo ya no trae y
  no pisa la configuración existente. (Los modpacks de CurseForge se registran, pero todavía no se pueden actualizar.)
- **Importar desde otros launchers:** **Importar... → De otro launcher** busca instancias de Prism Launcher / MultiMC /
  PolyMC, de la app de CurseForge y del launcher oficial (`.minecraft`) y las **copia** (nunca toca los originales), con
  opciones para mundos, mods y el resto.
- **GitHub Actions:** `ci.yml` corre las pruebas y compila en cada push y pull request; `release.yml` arma el instalador, el
  zip y los hashes a pedido y los deja como artefactos descargables.

### Correcciones
- El publish en un solo archivo dejaba afuera algunos archivos de `Assets\` (fuentes, ícono y música): ahora `build.ps1`
  copia la carpeta completa y se detiene si falta alguno imprescindible.
- Al cambiar de tema en el primer inicio, la barra de título (minimizar / cerrar) no cambiaba de color.
- Advertencias de compilación: `SkinsForm.Scale` ocultaba `Control.Scale`, un posible `null` en el enlace de los detalles de
  mods y un aviso de plataforma en el servicio de accesos directos.

### Notas
- El instalador **sigue sin estar firmado**: Windows SmartScreen puede mostrar un aviso (*Más información → Ejecutar de todas
  formas*).
- Las traducciones nuevas (todas menos español e inglés) no pasaron por hablantes nativos: si encontrás un error, abrí un
  issue o mandá un pull request con el `.json` corregido.

## 1.1.1

### Novedades
- **Paginación** en todos los buscadores (mods, modpacks, resource packs, shaders y datapacks): botones ◀ / ▶ con "Página X de Y".
  Antes solo se mostraban los primeros 30 resultados. (CurseForge no permite pasar de los primeros 10.000.)
- **Gestor de skins** en Cuentas → Skins...: skin actual de la cuenta Microsoft, subir `.png` (Clásico/Slim), biblioteca
  local (`data\skins`), vista previa 2D de frente y espalda, restaurar la skin por defecto y cara de la skin en la
  ventana principal. Para cuentas offline, copia la skin a la instancia en el formato de CustomSkinLoader.

### Correcciones
- Íconos en negro en los detalles de mods: se usa la versión original (`raw_icon_url` de Modrinth / logo completo de
  CurseForge) cuando la principal es `.webp`/`.svg`, y una letra si no hay ninguna que Windows pueda mostrar.
- Los botones de enlaces cortaban el texto ("Reportar problemas"): ahora se ajustan al texto.
- Las imágenes de la galería se pueden abrir completas (clic) en un visor con navegación.

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
