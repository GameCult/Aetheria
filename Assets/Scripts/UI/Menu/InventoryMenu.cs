/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UniRx;
using UniRx.Triggers;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InventoryMenu : MonoBehaviour
{
    public InventoryPanel[] InventoryPanels;
    public PropertiesPanel PropertiesPanel;
    public ActionGameManager GameManager;
    public RectTransform DragParent;
    public ConfirmationDialog Dialog;
    // public ClickCatcher Background;

    private int _currentPanel = -1;
    
    private int2 _selectedPosition;
    private InventoryPanel _selectedPanel;
    private ItemInstance _selectedItem;
    private ItemData _selectedItemData;
    // private List<IDisposable> _backgroundSubscriptions;

    // private ItemInstance _dragItem;
    // private Transform[] _dragCells;
    // private Vector2[] _dragOffsets;
    // private int2 _dragCellOffset;
    // private ItemRotation _originalRotation;
    // //private Shape _previousFakeOccupancy;
    // private Shape _originalOccupancy;
    // private EquippedItem _originalEquippedItem;
    // private InventoryPanel _originalPanel;
    //
    // private InventoryPanel _dragTargetPanel;
    // private int2 _dragTargetPosition;
    // private int2 _lastDragPosition;
    // private bool _dragTargetValid;
    // private bool _destroyItem;

    private void OnEnable()
    {
        // Background.gameObject.SetActive(true);

        // Background.OnEnter.Subscribe(enter =>
        // {
        //     _destroyItem = true;
        // });
        // BackgroundExited.OnPointerExitAsObservable().Subscribe(enter =>
        // {
        //     _destroyItem = false;
        // });
        var cargo = GameManager.DockingBay ?? GameManager.CurrentEntity.CargoBays.FirstOrDefault();
        if (cargo!=null)
            InventoryPanels[0].Display(cargo);
        else InventoryPanels[0].Clear();
        if(GameManager.CurrentEntity != null)
            InventoryPanels[1].Display(GameManager.CurrentEntity);
        else InventoryPanels[1].Clear();
    }

    private void OnDisable()
    {
        // Background.gameObject.SetActive(false);
    }

    void Start()
    {
        PropertiesPanel.GameManager = GameManager;
        foreach (var panel in InventoryPanels)
        {
            panel.OnClickAsObservable().Subscribe(e =>
            {
                if (e.data is InventoryCargoEventData cargoEvent)
                {
                    var item = cargoEvent.CargoBay.Occupancy[cargoEvent.Position.x, cargoEvent.Position.y];
                    if(item!=null)
                    {
                        if (e.clickCount == 2)
                        {
                            var otherPanel = panel == InventoryPanels[0] ? InventoryPanels[1] : InventoryPanels[0];
                            if (otherPanel.CanDropItem(item))
                            {
                                otherPanel.DropItem(item);
                                // TODO: SFX: Equip
                                cargoEvent.CargoBay.Remove(item);
                                panel.RefreshCells();
                                otherPanel.RefreshCells();
                            }
                            // else
                            // TODO: SFX: Fail
                        }
                        else
                        {
                            if (_selectedPanel != null)
                            {
                                if (_selectedPanel.CellInstances.ContainsKey(_selectedPosition))
                                {
                                    foreach (var v in _selectedItemData.Shape.Coordinates)
                                    {
                                        var v2 = _selectedItemData.Shape.Rotate(v, _selectedItem.Rotation) + _selectedPosition;
                                        _selectedPanel.CellInstances[v2].Icon.color = _selectedPanel.GetColor(v2);
                                    }
                                }
                            }
                            _selectedPanel = panel;
                            _selectedPosition = cargoEvent.Position;
                            PropertiesPanel.Inspect(item);
                            _selectedPanel = panel;
                            _selectedPosition = cargoEvent.CargoBay.Cargo[item];
                            _selectedItem = item;
                            _selectedItemData = item.Data.Value;
                            foreach (var v in _selectedItemData.Shape.Coordinates)
                            {
                                var v2 = _selectedItemData.Shape.Rotate(v, _selectedItem.Rotation) + _selectedPosition;
                                _selectedPanel.CellInstances[v2].Icon.color = _selectedPanel.GetColor(v2, true);
                            }
                            // TODO: SFX: Success
                        }
                    }
                }
                else if (e.data is InventoryEntityEventData entityEvent)
                {
                    var item = entityEvent.Entity.GearOccupancy[entityEvent.Position.x, entityEvent.Position.y];
                    if (item != null)
                    {
                        if (e.clickCount == 2)
                        {
                            var otherPanel = panel == InventoryPanels[0] ? InventoryPanels[1] : InventoryPanels[0];
                            if (otherPanel.CanDropItem(item.EquippableItem))
                            {
                                if (!entityEvent.Entity.Active)
                                {
                                    if (entityEvent.Entity.TryUnequip(item) != null)
                                    {
                                        otherPanel.DropItem(item.EquippableItem);
                                        // TODO: SFX: Unequip
                                        panel.RefreshCells();
                                        otherPanel.RefreshCells();
                                    }
                                    // else
                                    // TODO: SFX: Fail
                                }
                                // else
                                // TODO: SFX: Fail
                            }
                        }
                        else
                        {
                            if (_selectedPanel != null)
                            {
                                if (_selectedPanel.CellInstances.ContainsKey(_selectedPosition))
                                {
                                    foreach (var v in _selectedItemData.Shape.Coordinates)
                                    {
                                        var v2 = _selectedItemData.Shape.Rotate(v, _selectedItem.Rotation) + _selectedPosition;
                                        _selectedPanel.CellInstances[v2].Icon.color = _selectedPanel.GetColor(v2);
                                    }
                                }
                            }

                            PropertiesPanel.Inspect(item);
                            _selectedPanel = panel;
                            _selectedPosition = item.Position;
                            _selectedItem = item.EquippableItem;
                            _selectedItemData = GameManager.ItemManager.GetData(item.EquippableItem);
                            foreach (var v in _selectedItemData.Shape.Coordinates)
                            {
                                var v2 = _selectedItemData.Shape.Rotate(v, _selectedItem.Rotation) + _selectedPosition;
                                _selectedPanel.CellInstances[v2].Icon.color = _selectedPanel.GetColor(v2, true);
                            }
                            // TODO: SFX: Success
                        }
                    }
                }
            });

            panel.OnBackgroundClick.Subscribe(data =>
            {
                if (GameManager.CurrentEntity == null) return; // Only show ship settings if there's a ship, duh!
                
                PropertiesPanel.Clear();
                PropertiesPanel.AddField("Shutdown Threshold",
                    () => GameManager.CurrentEntity.Settings.ShutdownPerformance,
                    f => GameManager.CurrentEntity.Settings.ShutdownPerformance = f,
                    0,
                    1);
            });
        }
    }

    void Update()
    {
        // if (Keyboard.current.qKey.wasPressedThisFrame && _dragItem != null)
        // {
        //     _dragItem.Rotation = (ItemRotation) (((int) _dragItem.Rotation + 1) % 4);
        // }
        // if (Keyboard.current.eKey.wasPressedThisFrame && _dragItem != null)
        // {
        //     _dragItem.Rotation = (ItemRotation) (((int) _dragItem.Rotation + 3) % 4);
        // }
    }
}
