using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform), typeof(Image))]
public sealed class GugolMapWeatherPresenter : MonoBehaviour
{
    Image _overlay;

    void Awake()
    {
        _overlay = GetComponent<Image>();
        _overlay.raycastTarget = false;
    }

    public void Apply(GugolMapKnowledgeSnapshot snapshot)
    {
        if (_overlay == null) _overlay = GetComponent<Image>();
        string condition = snapshot?.WeatherCondition ?? "clear";
        _overlay.color = condition switch
        {
            "fog" => new Color(0.78f, 0.80f, 0.76f, 0.12f),
            "rain" => new Color(0.20f, 0.35f, 0.46f, 0.10f),
            "sleet" => new Color(0.48f, 0.58f, 0.64f, 0.12f),
            "storm" => new Color(0.13f, 0.17f, 0.22f, 0.16f),
            "hail" => new Color(0.55f, 0.62f, 0.66f, 0.13f),
            "snow" => new Color(0.82f, 0.85f, 0.84f, 0.12f),
            "wind" => new Color(0.60f, 0.58f, 0.50f, 0.06f),
            _ => Color.clear,
        };
        if (snapshot != null && snapshot.FloodRisk)
            _overlay.color = Color.Lerp(_overlay.color, new Color(0.17f, 0.34f, 0.43f, 0.16f), 0.5f);
    }
}
