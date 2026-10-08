// THIS FILE IS AUTO-GENERATED. DO NOT EDIT MANUALLY.

using FMODUnity;

namespace FMODSettings
{
    public readonly struct FMODEventPath
    {
        public EventReference Reference { get; }
        private FMODEventPath(string path) => Reference = RuntimeManager.PathToEventReference(path);

        public static readonly FMODEventPath BGM_ANCIENT_FOREST = new ("event:/BGM/Ancient Forest");
        public static readonly FMODEventPath BGM_BENEATH_THE_WAVES = new ("event:/BGM/Beneath The Waves");
        public static readonly FMODEventPath BGM_MIDNIGHT_DEEP = new ("event:/BGM/Midnight Deep");
        public static readonly FMODEventPath BGM_MIDNIGHT_FOREST = new ("event:/BGM/Midnight Forest");
        public static readonly FMODEventPath ENVIRONMENT_FOREST_BIRD = new ("event:/Environment/ForestBird");
        public static readonly FMODEventPath ENVIRONMENT_FOREST_MIDNIGHT = new ("event:/Environment/ForestMidnight");
        public static readonly FMODEventPath ENVIRONMENT_FOREST_WIND = new ("event:/Environment/ForestWind");
        public static readonly FMODEventPath ENVIRONMENT_RAIN = new ("event:/Environment/Rain");
        public static readonly FMODEventPath ENVIRONMENT_SEA_GULL = new ("event:/Environment/SeaGull");
        public static readonly FMODEventPath ENVIRONMENT_SEA_WAVES = new ("event:/Environment/SeaWaves");
        public static readonly FMODEventPath SE_CLOSE_ENEMY = new ("event:/SE/CloseEnemy");
        public static readonly FMODEventPath SE_FISH_ROD_SWING = new ("event:/SE/FishRodSwing");
        public static readonly FMODEventPath SE_FISSHING_GREAT = new ("event:/SE/FisshingGreat");
        public static readonly FMODEventPath SE_FISSHING_MISS = new ("event:/SE/FisshingMiss");
        public static readonly FMODEventPath SE_FISSHING_OK = new ("event:/SE/FisshingOK");
        public static readonly FMODEventPath SE_HEART_BEAT = new ("event:/SE/HeartBeat");
        public static readonly FMODEventPath SE_MESSAGE_WINDOW_OK = new ("event:/SE/MessageWindowOK");
        public static readonly FMODEventPath SE_OLD_DOOR_OPEN = new ("event:/SE/OldDoorOpen");
        public static readonly FMODEventPath SE_PLAYER_DEATH = new ("event:/SE/PlayerDeath");
        public static readonly FMODEventPath SE_WALK_FOREST = new ("event:/SE/WalkForest");
        public static readonly FMODEventPath SE_WALK_SEA = new ("event:/SE/WalkSea");
        public static readonly FMODEventPath TEST_BGM = new ("event:/TestBGM");
        public static readonly FMODEventPath TEST_SE = new ("event:/TestSE");
    }
}
