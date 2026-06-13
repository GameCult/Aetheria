/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using MessagePack;
[LegacyCatalogGroup("Users"), MessagePackObject]
public class PlayerData : DatabaseEntry, INamedEntry
{
    [Key(1)]
    public string Email;

    [Key(2)]
    public string Password;

    [Key(3)]
    public string Username;

    [Key(4)]
    public Guid Corporation;

    [IgnoreMember] public string EntryName
    {
        get => Username;
        set => Username = value;
    }
}