/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;

// The one place that decides, per input action, whether it is ship input or view input (ruling tactical-input-hands-off).
// Ship input drives the ship and pauses in the tactical view, which holds the ship's last commands; view input steers the
// view and the interface and stays live. TacticalView.Direct is the only source of the mode. An action absent from the table
// is not live (it fails closed), and a test requires every action of the Player and Global maps to be listed.
public static class TacticalInput
{
    private static readonly HashSet<string> Ship = new HashSet<string>
    {
        "Player/Move", "Player/Look", "Player/Turn", "Player/Face Aim", "Player/Toggle Face Aim", "Player/Tractor Beam",
        "Player/Target Reticle", "Player/Target Previous", "Player/Target Next", "Player/Target Nearest",
        "Player/Cycle Target Item", "Player/Toggle Heatsinks", "Player/Toggle Shield", "Player/Toggle Stance",
        "Player/Override Shutdown", "Player/Ping", "Global/Interact",
    };

    private static readonly HashSet<string> View = new HashSet<string>
    {
        "Player/Zoom", "Player/Pan", "Player/Point", "Player/Minimap Zoom", "Player/Hide UI",
        "Global/Zone Map", "Global/Galaxy Map", "Global/Inventory", "Global/Main Menu", "Global/Input Screen",
    };

    public static IEnumerable<string> Classified
    {
        get
        {
            foreach (var a in Ship) yield return a;
            foreach (var a in View) yield return a;
        }
    }

    public static bool IsShip(string map, string action) => Ship.Contains(map + "/" + action);

    // Whether the action's input is taken now.
    public static bool Live(TacticalView view, string map, string action)
    {
        var key = map + "/" + action;
        return View.Contains(key) || (Ship.Contains(key) && view.Direct);
    }
}
