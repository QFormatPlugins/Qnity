using UnityEngine;

public class QkitPointLight : QuakeKit.EntityPropertyReceiver
{
    [SerializeField] private Light lightComponent;

    public override void OnProperty(string propertyName, string propertyValue)
    {
        lightComponent = GetComponent<Light>();
        if (propertyName == "light")
        {
            var quakeLightValue = float.Parse(propertyValue);
            float rangeQuakeUnits = (-1f + Mathf.Sqrt(1f + 1024f * quakeLightValue)) * 0.5f;
            lightComponent.range = rangeQuakeUnits / inverseScale;
            lightComponent.intensity = quakeLightValue * 6.283f / (inverseScale * inverseScale);
        }
    }
}
