<div align="center">

# Velin

**Launcher de Minecraft Java para Windows: liviano, sin vueltas y con estética medieval.**

[English](README.en.md) · [Descargar](https://github.com/Brhenja/Velin/releases) · [Reportar un problema](https://github.com/Brhenja/Velin/issues)

![Licencia MIT](https://img.shields.io/badge/licencia-MIT-8B5A2B) ![Windows x64](https://img.shields.io/badge/Windows-x64-8B5A2B) ![.NET 8](https://img.shields.io/badge/.NET-8-8B5A2B)

</div>

Velin es un launcher de Minecraft Java hecho en **C# / .NET 8 / WinForms**. Cada instancia vive en su propia
carpeta, descarga Java sola, instala Fabric, Forge, Quilt y NeoForge, y busca mods, modpacks, resource packs y
shaders en **Modrinth** y **CurseForge** desde la misma interfaz.

> **Aviso:** Velin es un proyecto independiente. **No está afiliado, asociado ni respaldado por Mojang Studios ni
> por Microsoft.** Minecraft es una marca registrada de Mojang Studios.

> **Sobre cómo se hizo:** la mayor parte de este proyecto fue **"vibecodeada"**. Las ideas, el diseño y la dirección
> son de Brhenja; el código lo escribió **Claude**, la IA de Anthropic. Los recursos (música, sonido de click, mascota
> del tema Flash e ícono) también los generó una IA.

## Estado de la versión 1.0.0

- **Login de Microsoft (cuentas premium): desactivado.** El código está implementado, pero hace falta un Client ID
  aprobado por Mojang, que todavía no existe. La ventana **Cuentas** lo avisa. Por ahora se juega con **cuentas
  offline**. No se pudo probar contra los servidores reales de Microsoft.
- **CurseForge y NeoForge 1.20.1** están implementados pero **todavía no se probaron contra los servidores reales**.
- El instalador **no está firmado digitalmente**: Windows SmartScreen puede mostrar un aviso ("Windows protegió tu PC").
  Se puede continuar con *Más información → Ejecutar de todas formas*.

## Requisitos

- Windows 10 u 11, **64 bits**.
- Conexión a Internet para descargar Minecraft, Java y el contenido.
- No hace falta instalar .NET ni Java: el instalador incluye .NET y Velin descarga el Java que cada versión necesita.

## Instalación

Bajá lo que prefieras de la página de [Releases](https://github.com/Brhenja/Velin/releases):

**Instalador (`VelinSetup-1.0.0.exe`)**
1. Ejecutalo. No pide permisos de administrador: instala en `%LocalAppData%\Velin`.
2. Elegí si querés acceso directo en el escritorio y terminá. Queda en el menú Inicio.
3. Para desinstalar: *Configuración de Windows → Aplicaciones*. Te pregunta si querés borrar también tus datos
   (instancias y mundos); **por defecto no se borran**.

**Portable (`Velin-1.0.0-portable-win-x64.zip`)**
1. Descomprimilo en una carpeta donde tu usuario pueda escribir (no en `Program Files`).
2. Ejecutá `Velin.exe`. Para quitarlo, borrá la carpeta.

Los datos se guardan en la carpeta `data\` junto al `.exe`, así que podés mover la carpeta y llevarte todo.

## Uso

**Primer uso**
1. Tocá **Nueva instancia** (`Ctrl+N`): elegí nombre, versión de Minecraft, mod loader (o Vanilla) y RAM.
2. Tocá **Jugar** (`Enter`). La primera vez descarga todo (Java, librerías y assets) con barra de progreso y botón
   **Cancelar**; después arranca directo.
3. Si Minecraft se cierra con error, aparece una ventana con las últimas líneas del log y botones para abrir el log
   completo, el crash report o copiar el texto.

**Mods y contenido**
- El botón **Mods y contenido** abre un panel por instancia con mods, modpacks, mundos, servidores, resource packs,
  shaders, datapacks, capturas, backups y consola.
- Arriba a la izquierda elegís la fuente: **Modrinth** o **CurseForge** (los datapacks son solo de Modrinth).
- Al instalar un mod se instalan sus **dependencias obligatorias**, sin duplicar lo que ya tenés.
- **Revisar actualizaciones de mods** compara contra ambas fuentes y baja la versión nueva antes de borrar la vieja.

**Cuentas**
- Desde **Cuentas** creás cuentas offline (nombre libre) y elegís una por defecto o una por instancia.
- Las cuentas de Microsoft están desactivadas en esta versión (ver arriba).

**.jar propio**
1. Creá una carpeta dentro de `data\instances\` (por ejemplo `data\instances\Mi cliente`).
2. Poné adentro un `.jar` de cliente.
3. Abrí (o recargá con `F5`) Velin: la carpeta aparece como instancia con "jar propio". Si el `.jar` no dice su versión
   (los anteriores a 1.14), Velin te la pregunta la primera vez que jugás.

**API key de CurseForge**
CurseForge exige una key personal. Velin no trae ninguna: conseguí una gratis en
[console.curseforge.com](https://console.curseforge.com) y pegala en **Configuración**. Se guarda solo en tu PC.
Sin key, Modrinth sigue funcionando normal.

**Atajos:** `Ctrl+N` nueva instancia · `Ctrl+I` importar · `F5` recargar · `F2` renombrar · `Supr` eliminar ·
`Enter` jugar.

## Funciones

**Instancias**
- Cada una con su carpeta (`versions`, `mods`, `saves`, etc.): crear, duplicar, renombrar y eliminar.
- Exportar e importar: `.zip` de Velin, `.mrpack` de Modrinth y `.zip` de CurseForge.
- Grupos con filtro, orden por nombre / última vez jugada / tiempo jugado, y tiempo jugado por instancia.
- Cambiar la versión o el loader de una instancia conservando mundos, mods y configuración.
- Instancias con **.jar propio**.

**Loaders:** Vanilla, Fabric, Forge, Quilt y NeoForge (incluida 1.20.1). La lista solo muestra versiones compatibles y
elige por defecto la estable más nueva.

**Java y lanzamiento**
- Java **Temurin** descargado automáticamente según la versión de Minecraft, detección de otros Java instalados y
  Java propio por instancia.
- Flags de JVM tipo Aikar, RAM fija por instancia (con aviso si no alcanza).
- Opciones por instancia: ventana o pantalla completa, entrar directo a un servidor o mundo, argumentos extra.
- Descargas verificadas (SHA1 y tamaño, reintentos, sin dejar archivos a medias).
- Log `velin-launch.log`, consola en vivo y opción de mantener el launcher abierto mientras jugás.

**Contenido**
- Mods (activar/desactivar, quitar, actualizar), modpacks (con elección de versión), resource packs, shaders y
  datapacks.
- Reconocimiento por hash de `.jar` locales (Modrinth).
- **Mundos:** backup de uno, duplicar, eliminar, exportar e importar `.zip`.
- **Copias de seguridad automáticas** (opcionales) con restauración desde la interfaz.
- **Servidores:** lista con estado en vivo y entrada directa.
- **Capturas de pantalla** con miniaturas.
- **Vault:** explorador de archivos propio con subcarpetas, arrastrar y soltar, cortar/copiar/pegar y extracción de
  `.zip`, `.7z` y `.rar`; más un Vault de mods por instancia.

**Interfaz**
- Ventana propia sin bordes de Windows, temas JSON (Velin medieval y Flash), español e inglés.
- Música de fondo (con interruptor) y sonido de click, splash opcional y salteable.
- Proxy opcional (lo usa también el juego).

## Temas personalizados

Los temas son archivos `.json`. Los de fábrica están en `Assets\Themes\`. Para crear el tuyo, copiá uno, cambiale
nombre y colores (códigos hex) y guardalo en `data\themes\` (la carpeta se crea sola la primera vez que abrís Velin).
Aparece en **Configuración → Tema visual**, y el tema **se cambia sin reiniciar** (el idioma, en cambio, sí pide
reiniciar).

```json
{
  "Name": "Mi Tema",
  "BgDark": "#112233",
  "BgLight": "#223344",
  "ControlBg": "#334455",
  "ControlText": "#FFFFFF",
  "InputBg": "#EEEEEE",
  "InputText": "#112233",
  "Accent": "#FF8800",
  "ButtonTextOnAccent": "#000000",
  "TitleBarBg": "#0A1522",
  "TitleBarText": "#FFFFFF",
  "ProgressBarColor": "#FF8800",
  "ButtonGradientTop": "#445566",
  "ButtonGradientBottom": "#223344",
  "UseMedievalFont": false
}
```

## Dónde se guardan los datos

Todo vive en `data\` junto al `.exe` (en el instalador: `%LocalAppData%\Velin\data`):

```
data\
  instances\   tus instancias (mundos, mods, configuración)
  java\        Java descargado
  libraries\   librerías y assets de Minecraft
  assets\
  cache\
  vault\       tu Vault personal
  themes\      tus temas JSON
  backups\     copias de seguridad de mundos
  settings.json
  accounts.json   (tokens cifrados con DPAPI)
```

Actualizar Velin con el instalador **no toca** `data\`.

## Compilar desde el código

Ver [docs/COMPILAR.md](docs/COMPILAR.md). Para activar el login de Microsoft en un fork:
[docs/LOGIN_MICROSOFT_FORKS.md](docs/LOGIN_MICROSOFT_FORKS.md).

## Créditos

- Creado por **[Brhenja](https://github.com/Brhenja)**.
- Código escrito por **Claude** (Anthropic) a partir de las ideas y la dirección de Brhenja (ver "Sobre cómo se hizo").

## Licencias de terceros

Velin está bajo licencia **MIT** (ver [LICENSE](LICENSE)). Usa:

- [NAudio](https://github.com/naudio/NAudio) — MIT
- [SharpCompress](https://github.com/adamhathcock/sharpcompress) — MIT
- System.Security.Cryptography.ProtectedData — MIT (Microsoft)
- Fuentes **MedievalSharp** y **Cinzel** — SIL Open Font License (las licencias están en `Velin\Assets\`)
- **Eclipse Temurin** (Adoptium): Java que Velin descarga de Adoptium; no se redistribuye con Velin.
- APIs de **Modrinth**, **CurseForge** y **Mojang**, usadas para listar y descargar contenido.

## Licencia

[MIT](LICENSE) © 2026 Brhenja
