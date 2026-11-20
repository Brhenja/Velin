# Velin 1.3.0

Versión de calidad: menos consumo, mejor en pantallas con escala, ayuda cuando el juego falla y más control sobre skins y modpacks.

## Novedades
- **Analizador de crashes:** si Minecraft se cierra con error, Velin muestra las causas probables (Java muy viejo, falta de memoria, dependencias de mods que faltan, mods duplicados, placa de video, disco lleno, un mod que falla...) y qué hacer.
- **Capas** de tu cuenta Microsoft con vista previa, y **historial de skins** automático (la skin que reemplazás se guarda sola en la biblioteca).
- **Actualizar modpacks de CurseForge** (instalados desde Velin 1.3.0), además de los de Modrinth.
- **Escalado HiDPI:** las ventanas se adaptan a pantallas al 125 %, 150 % y más, y se reescalan al cambiar de monitor.
- **Vault:** arrastrá archivos y carpetas hacia afuera (se copian; Windows pregunta si hay que reemplazar).
- **Menos consumo:** memoria liberada al minimizar y mientras jugás, caché de fuentes y un recolector de basura más liviano.
- **Flags de Java** más cuidadosos, con un perfil liviano para PCs justas (2 núcleos o menos, 4 GB de RAM o menos).
- Descargas: el cliente se baja junto con las librerías y los assets usan 16 descargas simultáneas.
- El asistente de primer inicio arranca en el idioma de Windows.

## Correcciones
- Lo elegido en el primer inicio (por ejemplo la música apagada) se pisaba al abrir Velin.
- Se acabó el recuadro azul de Windows al pasar el mouse por listas, cajas de texto y desplegables.
- Versión correcta en *Configuración → Acerca de*.
- Textos chicos con cuadraditos en coreano, japonés y chino en dos ventanas.

## Notas
- Actualizar un modpack de CurseForge necesita tu clave de API y que el modpack se haya instalado con Velin 1.3.0 o posterior.
- El historial de skins empieza a registrar desde esta versión.
- Las capas y la actualización de CurseForge se probaron de forma limitada: si algo falla, abrí un issue con el mensaje que muestra Velin.
- El instalador **no está firmado**: Windows SmartScreen puede mostrar un aviso (*Más información → Ejecutar de todas formas*). Podés comprobar el archivo con `SHA256SUMS.txt`.
- Las traducciones (salvo español e inglés) no pasaron por hablantes nativos.

## Archivos
- `VelinSetup-1.3.0.exe`: instalador (sin permisos de administrador, en `%LocalAppData%\Velin`).
- `Velin-1.3.0-portable-win-x64.zip`: versión portable.
- `SHA256SUMS.txt`: hashes de los dos anteriores.

---

# Velin 1.3.0 (English)

A quality release: lower resource use, better on scaled displays, help when the game fails, and more control over skins and modpacks.

## What's new
- **Crash analyzer:** if Minecraft closes with an error, Velin shows the likely causes (Java too old, out of memory, missing mod dependencies, duplicate mods, graphics card, disk full, a mod failing...) and what to do.
- **Capes** for your Microsoft account with preview, and an automatic **skin history** (the skin you replace is saved to the library).
- **Update CurseForge modpacks** (installed from Velin 1.3.0), besides Modrinth ones.
- **HiDPI scaling:** windows adapt to screens at 125 %, 150 % and more, and rescale when moved to another monitor.
- **Vault:** drag files and folders out (they are copied; Windows asks whether to replace).
- **Lower resource use:** memory released when minimized and while playing, a font cache and a lighter garbage collector.
- More careful **Java flags**, with a light profile for modest PCs (2 cores or fewer, 4 GB RAM or less).
- Downloads: the client downloads alongside the libraries and assets use 16 simultaneous downloads.
- The first-run wizard starts in the Windows language.

## Fixes
- Choices made on first run (for example music off) were overwritten when opening Velin.
- No more blue Windows highlight when hovering lists, text boxes and drop-downs.
- Correct version in *Settings → About*.
- Garbled small text in Korean, Japanese and Chinese in two windows.

## Notes
- Updating a CurseForge modpack needs your API key and a modpack installed with Velin 1.3.0 or later.
- Skin history starts recording from this version.
- Capes and CurseForge updates have only been tested to a limited extent: if something fails, open an issue with the message Velin shows.
- The installer is **not code-signed**: Windows SmartScreen may show a warning (*More info → Run anyway*). You can verify the file with `SHA256SUMS.txt`.
- Translations (except Spanish and English) have not been reviewed by native speakers.

## Files
- `VelinSetup-1.3.0.exe`: installer (no administrator rights, installs to `%LocalAppData%\Velin`).
- `Velin-1.3.0-portable-win-x64.zip`: portable build.
- `SHA256SUMS.txt`: hashes of the two files above.
