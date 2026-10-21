using Velin.Services;

namespace Velin.UI
{
    // Texto de ayuda sobre cuánta RAM conviene asignar, según la PC.
    public static class RamHint
    {
        public static string Build(int selectedMB)
        {
            var mem = SystemInfo.GetMemory();
            if (mem is not { } m) return "";

            var recommended = SystemInfo.RecommendedMaxRamMB(m);
            var text = Localization.T("ram_hint", m.TotalMB, m.FreeMB, recommended);
            if (selectedMB > recommended) text += "  ⚠";
            return text;
        }
    }
}
