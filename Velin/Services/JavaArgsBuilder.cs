using System.Collections.Generic;

namespace Velin.Services
{
    public static class JavaArgsBuilder
    {
        // Aikar's Flags adaptados para cliente: minimizan los lag spikes del garbage collector (G1GC).
        public static List<string> BuildAikarFlags(int ramMB)
        {
            return new List<string>
            {
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
                // cliente puede trabar la PC un instante justo al iniciar el juego — muy
                // probablemente la causa del tirón/estática que reportaste.
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
                "-Dfile.encoding=UTF-8",
                "-Dsun.rmi.dgc.server.gcInterval=2147483646", // evita pausas periódicas innecesarias del RMI
                "-Dsun.rmi.dgc.client.gcInterval=2147483646"
            };
        }
    }
}
