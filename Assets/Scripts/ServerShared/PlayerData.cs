/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;

[LegacyCatalogGroup("Users")]
public class PlayerData : RuntimeCatalogEntry, INamedEntry
{
    public string Email;

    public string Password;

    public string Username;

    public Guid Corporation;

    public string EntryName
    {
        get => Username;
        set => Username = value;
    }
}
