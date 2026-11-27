using System.Collections.Generic;

namespace Velin.Services
{
    public static class JavaArgsBuilder
    {
        // Elige los flags según la JVM que se va a usar. OpenJ9 (Semeru) NO entiende los flags de G1/HotSpot,
        // así que tiene su propio juego; GraalVM corre sobre HotSpot y usa los mismos que Temurin.
        public static List<string> Build(int ramMB, JavaFlavor flavor, bool optimized)
            => Build(ramMB, flavor, optimized, System.Environment.ProcessorCount, SystemInfo.GetMemory()?.TotalMB ?? 0);

        // Igual, pero con los datos de la PC a la vista (núcleos y RAM total en MB; 0 = no se sabe), para poder probarlo.
        public static List<string> Build(int ramMB, JavaFlavor flavor, bool optimized, int cores, long totalRamMB)
        {
            if (!optimized) return BuildMinimal(ramMB);
            if (flavor == JavaFlavor.OpenJ9) return BuildOpenJ9Flags(ramMB);
            return IsWeakMachine(ramMB, cores, totalRamMB) ? BuildLightFlags(ramMB) : BuildAikarFlags(ramMB);
        }

        // PC justa: 2 núcleos o menos, 4 GB de RAM o menos, o un heap chico (hasta 1,5 GB). Ahí G1 gasta hilos y memoria
        // extra (marcado concurrente, conjuntos recordados) que no compensa: el recolector serial es más liviano.
        // Es un criterio práctico, no un benchmark: quien lo prefiera puede apagar los flags optimizados en Configuración.
        public static bool IsWeakMachine(int ramMB, int cores, long totalRamMB)
            => cores <= 2 || ramMB <= 1536 || (totalRamMB > 0 && totalRamMB <= 4096);

        // Para PCs justas (HotSpot/GraalVM): recolector serial, el heap arranca chico y crece según haga falta.
        public static List<string> BuildLightFlags(int ramMB) => new()
        {
            "-XX:+IgnoreUnrecognizedVMOptions",
            $"-Xms{System.Math.Min(ramMB, 512)}M",
            $"-Xmx{ramMB}M",
            "-XX:+UseSerialGC",
            "-XX:+DisableExplicitGC",
            "-XX:-UsePerfData", // no escribe los archivos de monitoreo de la JVM en disco
            "-Dfile.encoding=UTF-8",
            "-Dsun.rmi.dgc.server.gcInterval=2147483646",
            "-Dsun.rmi.dgc.client.gcInterval=2147483646"
        };

        // Sin optimizaciones: solo memoria y codificación.
        public static List<string> BuildMinimal(int ramMB) => new()
        {
            $"-Xms{System.Math.Min(ramMB, 512)}M",
            $"-Xmx{ramMB}M",
            "-Dfile.encoding=UTF-8"
        };

        // OpenJ9: política de GC "gencon" (la recomendada para juegos/aplicaciones interactivas) y caché de clases compartida
        // (acelera mucho los arranques siguientes). Nada de flags -XX de G1.
        public static List<string> BuildOpenJ9Flags(int ramMB) => new()
        {
            $"-Xms{ramMB}M",
            $"-Xmx{ramMB}M",
            "-Xgcpolicy:gencon",
            "-Xshareclasses:name=velin",
            "-Dfile.encoding=UTF-8",
            "-Dsun.rmi.dgc.server.gcInterval=2147483646",
            "-Dsun.rmi.dgc.client.gcInterval=2147483646"
        };

        // Aikar's Flags adaptados para cliente: minimizan los lag spikes del garbage collector (G1GC).
        public static List<string> BuildAikarFlags(int ramMB)
        {
            return new List<string>
            {
                // Si alguna versión de Java no conoce un flag, lo ignora en vez de no arrancar.
                "-XX:+IgnoreUnrecognizedVMOptions",
                $"-Xms{ramMB}M",
                $"-Xmx{ramMB}M",
                "-XX:+UseG1GC",
                "-XX:+ParallelRefProcEnabled",
                "-XX:MaxGCPauseMillis=200",
                "-XX:+UnlockExperimentalVMOptions",
                "-XX:+DisableExplicitGC",
                // OJO: a propósito NO usamos -XX:+AlwaysPreTouch. Ese flag hace que Java
                // reserve TODA la RAM configurada de una sola vez al arrancar, lo cual en
                // un servidor (que corre días seguidos) no se nota, pero en un launcher de
                // cliente puede trabar la PC un instante justo al iniciar el juego.
                "-XX:G1NewSizePercent=30",
                "-XX:G1MaxNewSizePercent=40",
                "-XX:G1HeapRegionSize=8M",
                "-XX:G1ReservePercent=20",
                "-XX:G1HeapWastePercent=5",
                "-XX:G1MixedGCCountTarget=4",
                "-XX:InitiatingHeapOccupancyPercent=15",
                "-XX:G1MixedGCLiveThresholdPercent=90",
                "-XX:G1RSetUpdatingPauseTimePercent=5",
                "-XX:SurvivorRatio=32",
                "-XX:MaxTenuringThreshold=1",
                // Extra, seguros (no afectan mods ni jugabilidad):
                "-XX:+UseStringDeduplication", // reduce RAM al no duplicar strings idénticos en memoria
                "-XX:-UsePerfData", // no escribe los archivos de monitoreo de la JVM en disco
                "-Dfile.encoding=UTF-8",
                "-Dsun.rmi.dgc.server.gcInterval=2147483646", // evita pausas periódicas innecesarias del RMI
                "-Dsun.rmi.dgc.client.gcInterval=2147483646"
            };
        }
    }
}
