using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static CultMath.math;

public class TextButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Button Button;
    public TextMeshProUGUI Label;
    public float AnimationDuration;
    public float HoverSpacing;

    private float _lerp;
    private bool _hovering;

    // Update is called once per frame
    void Update()
    {
        _lerp = saturate(_lerp + Time.deltaTime / AnimationDuration * (Button.interactable && _hovering ? 1 : -1));
        Label.characterSpacing = HoverSpacing * _lerp;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovering = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _hovering = false;
    }
}
