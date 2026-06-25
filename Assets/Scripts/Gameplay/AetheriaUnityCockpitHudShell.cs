/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

public sealed class AetheriaUnityCockpitHudShell
{
    public InventoryPanel ShipPanel { get; set; }
    public InventoryPanel TargetShipPanel { get; set; }
    public SchematicDisplay SchematicDisplay { get; set; }
    public SchematicDisplay TargetSchematicDisplay { get; set; }
    public PlaceUIElementWorldspace TargetIndicator { get; set; }

    public void SetRenderSettings(AetheriaRuntimeDaemonRenderSettings renderSettings)
    {
        SchematicDisplay?.SetRenderSettings(renderSettings);
        TargetSchematicDisplay?.SetRenderSettings(renderSettings);
    }

    public void UpdatePlayer(Entity currentEntity)
    {
        ShipPanel?.Display(currentEntity, true);
        SchematicDisplay?.ShowShip(currentEntity);
    }

    public void UpdateTarget(Entity target, Entity currentEntity)
    {
        if (TargetIndicator != null)
            TargetIndicator.gameObject.SetActive(target != null);
        if (TargetShipPanel != null)
            TargetShipPanel.gameObject.SetActive(target != null);

        if (target == null)
            return;

        TargetShipPanel?.Display(target, true);
        TargetSchematicDisplay?.ShowShip(target, currentEntity);
    }
}
