/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public sealed class AetheriaUnityGameplayInputShell : IDisposable
{
    private readonly List<ActionBarSlot> _actionBarSlots = new List<ActionBarSlot>();
    private readonly List<InputAction> _actionBarActions = new List<InputAction>();
    private bool _uiHidden;
    private int _zoomLevelIndex;

    public AetheriaInput Input { get; private set; }
    public RuntimePlayerSettings RuntimePlayerSettings { get; set; }
    public InputDisplayLayout InputDisplayLayout { get; set; }
    public ConfirmationDialog Dialog { get; set; }
    public MainMenu MainMenu { get; set; }
    public MenuPanel Menu { get; set; }
    public MapRenderer MenuMap { get; set; }
    public CanvasGroup GameplayUI { get; set; }
    public Transform ActionBar { get; set; }
    public ActionBarSlot ActionBarSlot { get; set; }
    public ZoneRenderer ZoneRenderer { get; set; }
    public AetheriaUnityMenuShell MenuShell { get; set; }
    public AetheriaUnityDragSession DragSession { get; set; }
    public AetheriaUnityActionBarPresentation ActionBarPresentation { get; set; }
    public AetheriaUnityPilotOperationAdapter PilotOperationAdapter { get; set; }
    public Func<Entity> ResolveCurrentEntity { get; set; }
    public Action<AetheriaInput> SetInput { get; set; }

    public IReadOnlyList<ActionBarSlot> ActionBarSlots => _actionBarSlots;

    public void Bootstrap()
    {
        Input = new AetheriaInput();
        SetInput?.Invoke(Input);

        if (RuntimePlayerSettings?.InputSettings != null)
        {
            foreach (var binding in RuntimePlayerSettings.InputSettings.InputActionMap)
                Input.asset[binding.Key.action].ApplyBindingOverride(binding.Key.binding, binding.Value);
        }

        if (InputDisplayLayout != null)
            InputDisplayLayout.Input = Input.asset;

        Dialog?.SetInputGate(
            () => Input.Global.Enable(),
            () => Input.Global.Disable());
        Input.Global.Enable();

        _zoomLevelIndex = ZoneRenderer.RenderSettings.ResolveDefaultMinimapZoomIndex();
        ZoneRenderer.MinimapDistance = (float)ZoneRenderer.RenderSettings.ResolveMinimapDistance(_zoomLevelIndex);

        RegisterGlobalInput();
        RegisterPlayerInput();
        RegisterActionBarInput();
    }

    public void EnablePlayerInput()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Input.Player.Enable();
        foreach (var action in _actionBarActions)
            action.Enable();
    }

    public void DisablePlayerInput()
    {
        Cursor.lockState = CursorLockMode.None;
        Input.Player.Disable();
        foreach (var action in _actionBarActions)
            action.Disable();
    }

    public void Dispose()
    {
        foreach (var action in _actionBarActions)
            action.Dispose();
        _actionBarActions.Clear();
        _actionBarSlots.Clear();
        Input?.Dispose();
        Input = null;
    }

    private void RegisterGlobalInput()
    {
        Input.Global.ZoneMap.performed += context =>
        {
            MenuShell.ToggleMenuTab(MenuTab.Map);
            var currentEntity = ResolveCurrentEntity?.Invoke();
            if (currentEntity == null)
                return;

            MenuMap.Position = AetheriaMath.ToUnity(currentEntity.CultPositionXZ);
        };

        Input.Global.Inventory.performed += context => MenuShell.ToggleMenuTab(MenuTab.Inventory);
        Input.Global.GalaxyMap.performed += context => MenuShell.ToggleMenuTab(MenuTab.Galaxy);

        Input.Global.Interact.performed += context =>
        {
            if (IsTextInputFocused() || MainMenu.gameObject.activeSelf)
                return;

            if (ResolveCurrentEntity?.Invoke() == null)
            {
                Dialog.Clear();
                Dialog.Title.text = "Can't undock. You dont have a ship!";
                Dialog.Show();
                Dialog.MoveToCursor();
                return;
            }

            PilotOperationAdapter.RequestInteract();
        };

        Input.Global.MainMenu.performed += context =>
        {
            if (Menu.gameObject.activeSelf)
                MenuShell.ToggleMenuTab(Menu.CurrentTab);
            else
                MenuShell.ToggleFullscreenMenu(MainMenu.gameObject);
        };

        Input.Global.InputScreen.performed += context => MenuShell.ToggleFullscreenMenu(MenuShell.HelpScreen);
    }

    private void RegisterPlayerInput()
    {
        Input.Player.MinimapZoom.performed += context =>
        {
            _zoomLevelIndex = ZoneRenderer.RenderSettings.ResolveNextMinimapZoomIndex(_zoomLevelIndex);
            ZoneRenderer.MinimapDistance = (float)ZoneRenderer.RenderSettings.ResolveMinimapDistance(_zoomLevelIndex);
        };

        Input.Player.HideUI.performed += context =>
        {
            _uiHidden = !_uiHidden;
            GameplayUI.alpha = _uiHidden ? 0 : 1;
            ActionBar.gameObject.SetActive(!_uiHidden);
        };

        Input.Player.OverrideShutdown.performed += context =>
        {
            var currentEntity = ResolveCurrentEntity?.Invoke();
            if (currentEntity != null)
                PilotOperationAdapter.RequestOverrideShutdown(!currentEntity.OverrideShutdown);
        };

        Input.Player.Ping.performed += context => PilotOperationAdapter.RequestSensorPing();
        Input.Player.ToggleHeatsinks.performed += context =>
        {
            var currentEntity = ResolveCurrentEntity?.Invoke();
            if (currentEntity != null)
                PilotOperationAdapter.RequestHeatsinksEnabled(!currentEntity.HeatsinksEnabled);
        };
        Input.Player.ToggleShield.performed += context => PilotOperationAdapter.RequestShieldToggle();
        Input.Player.TargetReticle.performed += context => PilotOperationAdapter.RequestTargetReticle();
        Input.Player.TargetNearest.performed += context => PilotOperationAdapter.RequestTargetNearest();
        Input.Player.TargetNext.performed += context => PilotOperationAdapter.RequestTargetNext();
        Input.Player.TargetPrevious.performed += context => PilotOperationAdapter.RequestTargetPrevious();
    }

    private void RegisterActionBarInput()
    {
        foreach (var controlPath in (RuntimePlayerSettings?.InputSettings?.ActionBarInputs ?? Enumerable.Empty<string>()).OrderBy(input => input))
            CreateActionBarSlot(controlPath);
    }

    private void CreateActionBarSlot(string controlPath)
    {
        var action = new InputAction(binding: controlPath);
        _actionBarActions.Add(action);

        var slot = UnityEngine.Object.Instantiate(ActionBarSlot, ActionBar);
        slot.ControlPath = controlPath;
        slot.Binding = null;
        _actionBarSlots.Add(slot);

        action.started += context => slot.Binding?.Activate();
        action.canceled += context => slot.Binding?.Deactivate();

        var shortName = controlPath.Substring(controlPath.LastIndexOf('/') + 1);
        var sprite = Resources.Load<Sprite>($"Sprites/Input/{shortName}");
        if (sprite != null)
        {
            slot.InputIcon.sprite = sprite;
            slot.InputLabel.gameObject.SetActive(false);
        }
        else
        {
            slot.InputLabel.text = shortName;
            slot.InputIcon.gameObject.SetActive(false);
        }

        slot.PointerEnterTrigger.OnPointerEnterAsObservable().Subscribe(_ =>
        {
            DragSession.RegisterTarget(dragAction => ActionBarPresentation.RequestBinding(slot, dragAction));
        });
        slot.PointerExitTrigger.OnPointerExitAsObservable().Subscribe(_ => DragSession.UnregisterTarget());
    }

    private static bool IsTextInputFocused()
    {
        return EventSystem.current != null &&
               EventSystem.current.currentSelectedGameObject != null &&
               EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
    }
}
