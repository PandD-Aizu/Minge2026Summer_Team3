using _Project.Scripts.Core;
using UnityEngine;

public class TimeOfDayView : MonoBehaviour
{
    [SerializeField] private Light _directionalLight;


    public void ApplyTimeToSky(TimeOfDay timeOfDay)
    {
        _directionalLight.color =
        (timeOfDay == TimeOfDay.Night)? Color.midnightBlue : Color.cornsilk;

    }
}
