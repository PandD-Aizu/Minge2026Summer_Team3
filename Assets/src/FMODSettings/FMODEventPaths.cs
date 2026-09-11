// THIS FILE IS AUTO-GENERATED. DO NOT EDIT MANUALLY.
// Generated at: 2026-09-11 16:42:35

using FMODUnity;

namespace FMODSettings
{
    public readonly struct FMODEventPath
    {
        public EventReference Reference { get; }
        private FMODEventPath(string path) => Reference = RuntimeManager.PathToEventReference(path);

        public static readonly FMODEventPath TEST_SE = new ("event:/TestSE");
        public static readonly FMODEventPath TEST_BGM = new ("event:/TestBGM");
    }
}
