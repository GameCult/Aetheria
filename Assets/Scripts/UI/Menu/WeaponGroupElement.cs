using System.Collections;
using System.Collections.Generic;
using TMPro;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.UI;

public class WeaponGroupElement : MonoBehaviour
{
    public TextMeshProUGUI Label;
    public Button Button;
    public ObservableBeginDragTrigger BeginDragTrigger;
    public ObservableDragTrigger DragTrigger;
    public ObservableEndDragTrigger EndDragTrigger;
}
