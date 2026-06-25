/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class AetheriaUnityMenuShell
{
    public MainMenu MainMenu { get; set; }
    public GameObject HelpScreen { get; set; }
    public InputDisplayLayout InputDisplayLayout { get; set; }
    public GameObject UiRoot { get; set; }
    public MenuPanel Menu { get; set; }
    public CanvasGroup GameplayUI { get; set; }
    public Func<Entity> ResolveCurrentEntity { get; set; }
    public Func<bool> IsCurrentEntityUndocked { get; set; }
    public Func<Entity, Entity> ResolveObservedTarget { get; set; }
    public Action<bool> SetPaused { get; set; }
    public Action EnablePlayerInput { get; set; }
    public Action DisablePlayerInput { get; set; }
    public Action UpdatePlayerPanel { get; set; }
    public Action<Entity> UpdateTargetPanel { get; set; }

    private bool _menuShownBeforeFullscreen;

    public bool CanOpenRuntimeInputScreen()
    {
        return ResolveCurrentEntity?.Invoke() != null &&
               HelpScreen != null &&
               InputDisplayLayout != null;
    }

    public void ShowRuntimeInputScreen()
    {
        ShowFullscreenMenu(HelpScreen);
    }

    public void ToggleFullscreenMenu(GameObject menu)
    {
        if (IsTextInputFocused() || menu == null || ResolveCurrentEntity?.Invoke() == null)
            return;

        if (menu.activeSelf)
            HideFullscreenMenu(menu);
        else
            ShowFullscreenMenu(menu);
    }

    public void ToggleMenuTab(MenuTab tab)
    {
        if (IsTextInputFocused() || MainMenu == null || Menu == null)
            return;
        if (MainMenu.gameObject.activeSelf)
            return;

        if (Menu.gameObject.activeSelf && Menu.CurrentTab == tab)
        {
            Menu.gameObject.SetActive(false);
            if (IsCurrentEntityUndocked?.Invoke() == true)
            {
                EnablePlayerInput?.Invoke();
                RefreshCurrentPanels();
                if (GameplayUI != null)
                    GameplayUI.gameObject.SetActive(true);
            }

            return;
        }

        DisablePlayerInput?.Invoke();
        Menu.ShowTab(tab);
        if (GameplayUI != null)
            GameplayUI.gameObject.SetActive(false);
    }

    private void ShowFullscreenMenu(GameObject menu)
    {
        if (IsTextInputFocused() || menu == null || ResolveCurrentEntity?.Invoke() == null)
            return;

        if (MainMenu != null && MainMenu.gameObject != menu)
            MainMenu.gameObject.SetActive(false);
        if (HelpScreen != null && HelpScreen != menu)
            HelpScreen.SetActive(false);

        SetPaused?.Invoke(true);
        menu.SetActive(true);
        if (UiRoot != null)
            UiRoot.SetActive(false);

        _menuShownBeforeFullscreen = Menu != null && Menu.gameObject.activeSelf;
        if (!_menuShownBeforeFullscreen)
            DisablePlayerInput?.Invoke();
    }

    private void HideFullscreenMenu(GameObject menu)
    {
        if (menu == null || ResolveCurrentEntity?.Invoke() == null)
            return;

        SetPaused?.Invoke(false);
        menu.SetActive(false);
        if (UiRoot != null)
            UiRoot.SetActive(true);

        if (!_menuShownBeforeFullscreen)
        {
            EnablePlayerInput?.Invoke();
            RefreshCurrentPanels();
        }
    }

    private void RefreshCurrentPanels()
    {
        var currentEntity = ResolveCurrentEntity?.Invoke();
        UpdatePlayerPanel?.Invoke();
        UpdateTargetPanel?.Invoke(ResolveObservedTarget?.Invoke(currentEntity));
    }

    private static bool IsTextInputFocused()
    {
        return EventSystem.current != null &&
               EventSystem.current.currentSelectedGameObject != null &&
               EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null;
    }
}
