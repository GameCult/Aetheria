/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CultMath;
using Xunit;

// Ruling tactical-input-hands-off: ship input pauses in the tactical view, view input stays live, and no action is left out.
public sealed class TacticalInputTests
{
    private static TacticalView Flight()
    {
        var view = new TacticalView(1.25f, 250, 1000, .01f, 2, .6f, .4f, .15f);
        view.Reset(20000, 30, new float2(0, 0));
        return view;
    }

    private static TacticalView Tactical()
    {
        var view = Flight();
        for (var i = 0; i < 30 && !view.Tactical; i++) { view.Tick(.05f); view.Scroll(1); }
        Assert.True(view.Tactical);
        return view;
    }

    private static List<string> Actions()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Assets", "Resources", "Aetheria.inputactions")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir.FullName, "Assets", "Resources", "Aetheria.inputactions")));
        return doc.RootElement.GetProperty("maps").EnumerateArray()
            .Where(m => m.GetProperty("name").GetString() is "Player" or "Global")
            .SelectMany(m => m.GetProperty("actions").EnumerateArray()
                .Select(a => m.GetProperty("name").GetString() + "/" + a.GetProperty("name").GetString()))
            .ToList();
    }

    [Fact]
    public void EveryPlayerAndGlobalActionIsClassifiedAsShipOrViewInput()
    {
        var actions = Actions();
        Assert.True(actions.Count > 20);
        var listed = TacticalInput.Classified.ToList();
        Assert.Empty(actions.Except(listed));
        Assert.Empty(listed.Except(actions));
        Assert.Equal(listed.Count, listed.Distinct().Count());
    }

    [Fact]
    public void ShipInputPausesInTacticalAndViewInputStaysLive()
    {
        var flight = Flight();
        var tactical = Tactical();
        foreach (var action in TacticalInput.Classified)
        {
            var map = action.Split('/')[0];
            var name = action.Split('/')[1];
            Assert.True(TacticalInput.Live(flight, map, name), action);
            Assert.Equal(!TacticalInput.IsShip(map, name), TacticalInput.Live(tactical, map, name));
        }
        foreach (var live in new[] { "Zoom", "Pan", "Point", "Minimap Zoom", "Hide UI" })
            Assert.True(TacticalInput.Live(tactical, "Player", live), live);
        foreach (var paused in new[] { "Move", "Look", "Turn", "Tractor Beam", "Ping", "Toggle Shield" })
            Assert.False(TacticalInput.Live(tactical, "Player", paused), paused);
        Assert.False(TacticalInput.Live(tactical, "Global", "Interact"));
    }

    [Fact]
    public void AnUnclassifiedActionIsNotLiveEvenInFlight()
    {
        Assert.False(TacticalInput.Live(Flight(), "Player", "Warp Drive"));
    }

    [Fact]
    public void ShipInputIsLiveAgainOnReturnToTheFlightView()
    {
        var view = Tactical();
        Assert.False(TacticalInput.Live(view, "Player", "Move"));
        for (var i = 0; i < 40 && view.Tactical; i++) { view.Tick(.05f); view.Scroll(-1); }
        Assert.False(view.Tactical);
        Assert.True(TacticalInput.Live(view, "Player", "Move"));
    }
}
