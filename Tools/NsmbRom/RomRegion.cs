namespace NsmbRom {
    public enum RomRegion {
        Usa,
        Europe,
    }

    public static class RomRegions {
        public const string UsaGameCode = "A2DE";
        public const string EuropeGameCode = "A2DP";

        public static RomRegion FromGameCode(string gameCode) {
            return gameCode switch {
                UsaGameCode => RomRegion.Usa,
                EuropeGameCode => RomRegion.Europe,
                _ => throw new RomFormatException($"Unsupported game code '{gameCode}': only {UsaGameCode} (USA) and {EuropeGameCode} (Europe) are supported."),
            };
        }
    }
}
