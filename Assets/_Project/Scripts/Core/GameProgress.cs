using System.Collections.Generic;
namespace _Project.Scripts.Core
{
    public class GameProgress
    {
        public int CurrentDay { get; private set; } = 1;
        public TimeOfDay CurrentTimeOfDay { get; private set; } = TimeOfDay.Day;

        private readonly HashSet<StoryFlag> storyFlags = new();

        public void StartDay()
        {
            CurrentTimeOfDay = TimeOfDay.Day;
        }

        public void StartNight()
        {
            CurrentTimeOfDay = TimeOfDay.Night;
        }

        public void AdvanceDay()
        {
            CurrentDay++;
        }

        /// <summary>
        /// フラグを達成したらHashSetにStoryFlagを入れる
        /// </summary>
        /// <param name="storyFlag"></param>
        public void SetStoryFlag(StoryFlag storyFlag)
        {
            storyFlags.Add(storyFlag);
        }

        public bool HasStoryFlag(StoryFlag storyFlag)
        {
            return storyFlags.Contains(storyFlag);
        }
        
    }
}