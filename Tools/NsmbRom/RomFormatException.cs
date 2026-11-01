using System;

namespace NsmbRom {
    /// <summary>The input is not a ROM this tool understands. Thrown instead of emitting garbage.</summary>
    public sealed class RomFormatException : Exception {
        public RomFormatException(string message) : base(message) { }
    }
}
