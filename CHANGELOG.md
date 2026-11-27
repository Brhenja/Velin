# Changelog

## 1.4.0

Dos partes: una interna (el código se separa en un núcleo sin pantallas, preparación para futuras interfaces) y una visible (ventana principal renovada, fuente a elección, Vault mejorada y sonido simplificado).

### Novedades
- **Dos estilos de interfaz** (Ajustes → General → *Estilo de interfaz*; se aplica al reiniciar):
  - **Velin New** (por defecto): tres botones de ícono sobre la lista de instancias (**+**, **−** y **engranaje**), y botones con ícono
    para **Jugar** (flecha), **Contenido** (rompecabezas), **Abrir carpeta** (carpeta), **Vault** (cofre), **Ajustes** (engranaje) y **Cuentas**
    (cabeza de Minecraft). **Mods** y **Modpacks** son botones propios y ya no están dentro de *Contenido*. El **+** abre un menú: nueva instancia,
    **desde un modpack** (sin necesidad de tener una instancia), importar un archivo o importar de otro launcher. El **engranaje**
    abre las acciones de la instancia (información, abrir carpeta, versión, opciones de lanzamiento, backups, consola, grupo,
    duplicar, renombrar, exportar, acceso directo, actualizar modpack, revisar actualizaciones de mods).
  - **Velin Legacy**: la disposición de siempre (con *Contenido*, que sigue incluyendo Mods y Modpacks, y sin íconos nuevos).
  - Íconos dibujados por código: no suman imágenes ni dependencias.
- **Elegir una versión más vieja de un mod:** botón **Elegir versión...** junto a *Instalar* (Modrinth y CurseForge, solo mods). Muestra las
  versiones de ese mod para la versión de Minecraft y el loader de la instancia (la última y las anteriores). La que elijas queda **fijada**:
  *Revisar actualizaciones* no la toca. Instalar el mod de nuevo con *Instalar seleccionado* vuelve a la última y quita la fijación.
- **Mods instalados con buscador y orden:** un buscador arriba de la lista (nombre o archivo, sin importar mayúsculas ni tildes) y un orden
  **A-Z, más recientes, más antiguos, activados primero, desactivados primero** (se recuerda el elegido).
- **Doble clic sobre una instancia = Jugar** (en los dos estilos). El menú de clic derecho ahora incluye **Abrir carpeta de la instancia**
  y **Revisar actualizaciones de mods**.
- **Se quitó el botón "Más"** (sus acciones están en el clic derecho y, en el estilo nuevo, en el engranaje).
- **Fuente de texto** en Ajustes, al lado del tema: *Predeterminada*, **Medieval** (la fuente propia de Velin, aunque el tema no la use) o una lista corta de fuentes seguras (Segoe UI, Calibri,
  Arial, Tahoma, Trebuchet MS, Cambria, Consolas; solo aparecen las instaladas). En coreano, japonés y chino manda la fuente del idioma.
- **Gestor de archivos a elección** (Ajustes → Contenido, desactivado por defecto): usar el **Explorador de Windows** en vez del gestor de archivos de Velin.
- **Idioma, fuente y estilo de interfaz** se aplican reiniciando: al guardar, Velin pregunta *¿Reiniciar ahora? Sí / No*.
- **Vault mejorada:** ventana más grande, **Renombrar** (menú y F2), atajos de teclado (Supr, Ctrl+C / X / V / A, Retroceso para subir,
  Enter, F5, Ctrl+F para buscar), resumen de la selección en la barra de estado (cantidad y tamaño) y confirmación antes de eliminar
  (se borra de forma permanente).
- En español, "Configuración" pasó a llamarse **Ajustes** (botón, título de la ventana y textos que la nombran); los demás idiomas no cambian. "Mods y contenido" pasó a **Contenido**.
- **Sonido simplificado:** se sacó la música de fondo y la del splash; queda el **click**, que ahora se puede reproducir muchas veces
  seguidas (se superponen) y solo abre el dispositivo de audio si está activado. Las claves viejas de música en `settings.json` se ignoran.

### Cambios internos
- **Nuevo proyecto `Velin.Core`:** los modelos y servicios (la lógica que no es una pantalla) pasan a una biblioteca propia que no usa
  WinForms. El programa de Windows (`Velin`) queda con las ventanas, el tema y las piezas que sólo tienen sentido en Windows
  (carpeta `Platform\`: sonido, recorte de memoria, idioma de Windows). Las pruebas ahora usan `Velin.Core` como proyecto.
- **Costuras con el sistema operativo:** `SecretProtector` define cómo se cifran los tokens de las cuentas (DPAPI en Windows, y otro
  sistema podrá poner la suya), y `SystemInfo` y los accesos directos ya no fallan fuera de Windows.
- Una prueba nueva comprueba que `Velin.Core` no referencie WinForms, `System.Drawing` ni NAudio.
- Pruebas nuevas para el filtro y orden de mods (`ModListView`), la versión fijada y para `FontChoices` (lista segura de fuentes) y `UiStyles` (estilo de interfaz), y para leer configuraciones viejas.
- La versión que se muestra se toma del programa que arranca (Velin.exe), no de la biblioteca.
- `Velin.sln` incluye el núcleo y las pruebas.

### Notas
- Para quien compile: ver `docs/COMPILAR.md` ("Qué va en cada proyecto"). Si venís de la 1.3.0, borrá las carpetas viejas `Velin\Models` y
  `Velin\Services` antes de copiar los archivos nuevos (ahora están en `Velin.Core\`).
- El instalador **sigue sin estar firmado**: Windows SmartScreen puede mostrar un aviso (*Más información → Ejecutar de todas formas*).

## 1.3.0

Versión de calidad: menos consumo, mejor en pantallas con escala, ayuda cuando el juego falla y más control sobre skins y modpacks.

### Novedades
- **Analizador de crashes:** cuando Minecraft se cierra con error, la ventana muestra arriba las **causas probables** (hasta 4)
  y abajo el log. Reconoce: argumento de JVM no válido, memoria que Java no pudo reservar, Java demasiado viejo (indica
  cuál hace falta), falta de memoria, dependencias de mods que faltan (Fabric y Forge), mods duplicados, falla de la
  placa de video (OpenGL), biblioteca nativa que no carga, disco lleno y un mod que falla al cargarse (con el mod
  sospechoso si el log lo dice). Si no reconoce nada, lo dice en vez de adivinar.
- **Capas:** en el gestor de skins de una cuenta Microsoft, lista de las capas de la cuenta con vista previa, para
  equiparlas o quitarlas. Las capas no se pierden.
- **Historial de skins:** antes de reemplazar o restaurar tu skin, la actual se guarda sola en la biblioteca (marcada con
  ↺ y la fecha). No repite imágenes y conserva las 20 más nuevas.
- **Actualizar modpacks de CurseForge** (instalados desde Velin 1.3.0 en adelante), con el mismo cuidado que los de
  Modrinth: conserva mundos, opciones y mods propios, baja solo lo que cambió y aparta lo que el pack ya no trae a
  `modpack-update-trash\`.
- **Escalado HiDPI:** las ventanas se escalan según el DPI del monitor (125 %, 150 %...) en vez de cortar el texto; se
  limitan para que entren en la pantalla y se reescalan al pasarlas a otro monitor. A 100 % no cambia nada.
- **Vault:** se pueden **arrastrar archivos y carpetas hacia afuera** (Explorador, Escritorio...). Se copian, así que la
  Vault no pierde nada, y si el destino ya tiene un archivo con ese nombre Windows pregunta si se reemplaza.
- **Menos consumo:**
  - Caché de fuentes (antes cada control de cada ventana creaba la suya).
  - Al minimizar la ventana, y mientras se juega con el launcher escondido, se devuelve a Windows la memoria que no se usa.
  - Recolector de basura sin el hilo concurrente.
  - La mascota animada del tema Flash solo se carga con ese tema y se esconde con la ventana minimizada.
- **Flags de Java:** los perfiles para HotSpot y GraalVM ignoran flags que una versión de Java no conozca y no escriben
  archivos de monitoreo en disco. En **PCs justas** (2 núcleos o menos, 4 GB de RAM o menos, o una instancia con hasta
  1,5 GB de RAM) se usa un perfil liviano: recolector serial y heap que arranca en 512 MB. Es un criterio práctico, no un
  benchmark: se puede volver a lo mínimo apagando los flags optimizados en Configuración. OpenJ9 no cambia.
- **Descargas:** el cliente de Minecraft se baja junto con las librerías y los assets usan 16 descargas simultáneas.
  (Librerías, assets y modpacks ya se descargaban en paralelo.)
- **Primer inicio:** el asistente ahora arranca en el idioma de Windows (antes siempre en inglés).
- 18 textos nuevos traducidos a los 13 idiomas y uno actualizado.

### Correcciones
- Lo elegido en el primer inicio (por ejemplo la música apagada) se pisaba al abrir Velin por una migración vieja.
- El recuadro azul de Windows al pasar el mouse por las listas, las cajas de texto, los desplegables y los cuadros de
  texto enriquecido (las casillas de verificación lo conservan: necesitan el tema de Windows para dibujarse).
- La versión mostrada en **Acerca de** y el instalador.
- "Copiar imagen" en las capturas dejaba un `Bitmap` sin liberar.
- El texto chico de la ventana de actualizar modpacks y el aviso de RAM de nueva instancia se veían con cuadraditos en
  coreano, japonés y chino.

### Notas
- Actualizar un modpack de CurseForge necesita tu clave de API de CurseForge y que el modpack se haya instalado con
  Velin 1.3.0 o posterior (antes Velin no guardaba la lista de archivos del pack).
- El historial de skins empieza a registrar desde esta versión: Mojang no ofrece uno propio.
- El instalador **sigue sin estar firmado**: Windows SmartScreen puede mostrar un aviso (*Más información → Ejecutar de
  todas formas*).
- La API de capas de Mojang y la actualización de CurseForge solo se probaron de forma limitada: si algo falla, abrí un
  issue con el mensaje que muestra Velin.

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
