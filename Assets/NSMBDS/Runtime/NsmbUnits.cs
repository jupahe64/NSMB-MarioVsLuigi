namespace NsmbDs {
    /// <summary>Conversions between DS units and Unity world units (1 tile = 16 px = 0.5 units).</summary>
    public static class NsmbUnits {
        public const float PixelsPerUnit = 32f;
        public const float StepsPerSecond = 60f;
        public const float StepSeconds = 1f / StepsPerSecond;

        public static float PixelsToUnits(float pixels) {
            return pixels / PixelsPerUnit;
        }

        public static float PixelsPerFrameToUnitsPerSecond(float pixelsPerFrame) {
            return pixelsPerFrame * StepsPerSecond / PixelsPerUnit;
        }
    }
}
