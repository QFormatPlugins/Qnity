using UnityEngine;

public class QkitPointLight : QuakeKit.EntityPropertyReceiver
{
    private const float IntensityBoost = 1.5f; 
    
    [SerializeField] private Light _lightComponent;
    
    public override void OnProperty(string propertyName, string propertyValue)
    {
        _lightComponent = GetComponent<Light>();
        if (propertyName == "light")
        {
            var quakeLightValue = int.Parse(propertyValue);
            
            // 1. Calculate the distance the light should reach
            var calculatedRange = quakeLightValue / inverseScale;
            _lightComponent.range = calculatedRange;
            _lightComponent.intensity = quakeLightValue * IntensityBoost / inverseScale;
        }
    }
}
