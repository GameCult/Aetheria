/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameCult.Caching;
using MessagePack;

// The verse grammar (aetheria-release, cut verse-grammar; ghostlight-verse rulings verse-grammar-home-ruled and
// verse-verbs-two-render-paths): the verbs a Ghostlight-simulated Aetheria world may use, each rendering by exactly one
// path the game already has, with typed participant roles. Aetheria owns which verbs exist and how each renders;
// Ghostlight owns what each does to world state. Nothing in the game runtime reads these documents.
[CultDocument("aetheria.verse_grammar", "1"), CultGlobal, MessagePackObject]
public class VerseGrammar
{
    [Key(1)] public int Revision;
    [Key(2)] public string Description;
}

// How a verb is shown: filmed in-engine as a ship action, or presented as an Aetheria conversation.
public enum VerseRenderPath
{
    ShipAction = 1,
    Conversation = 2,
}

// What kind of thing a role binds.
public enum VerseReferentKind
{
    Person = 1,
    Faction = 2,
    Population = 3,
    Place = 4,
    Cargo = 5,
}

[MessagePackObject]
public class VerseRole
{
    [Key(1)] public string Name;
    [Key(2)] public VerseReferentKind Binds;
}

// The actor is the invoking subject and is never a role.
[CultDocument("aetheria.verse_verb", "1"), MessagePackObject]
public class VerseVerb
{
    [CultName, Key(1)] public string Name;
    [Key(2)] public VerseRenderPath RenderPath;
    [Key(3)] public VerseRole[] Roles = Array.Empty<VerseRole>();
    [Key(4)] public string Description;
}

public static class VerseGrammarValidation
{
    private static readonly Regex VerbName = new Regex("^[a-z][a-z0-9_]{0,47}\z", RegexOptions.CultureInvariant);
    private static readonly Regex RoleName = new Regex("^[a-z][a-z0-9_]{0,31}\z", RegexOptions.CultureInvariant);

    // Canonical names, unique role names, defined enum values. A failure names the field and the rule, and the verb once
    // its name is known canonical; it never prints a value the author wrote that is itself the fault.
    public static void Validate(VerseVerb verb)
    {
        if (verb.Name == null || !VerbName.IsMatch(verb.Name))
            throw new InvalidOperationException("VerseVerb.Name: not a canonical verb name ([a-z][a-z0-9_]{0,47}).");
        if (!Enum.IsDefined(typeof(VerseRenderPath), verb.RenderPath))
            throw new InvalidOperationException($"VerseVerb \"{verb.Name}\" RenderPath: not a defined render path.");
        if (verb.Roles == null)
            throw new InvalidOperationException($"VerseVerb \"{verb.Name}\" Roles: missing.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in verb.Roles)
        {
            if (role == null || role.Name == null || !RoleName.IsMatch(role.Name))
                throw new InvalidOperationException($"VerseVerb \"{verb.Name}\" Roles.Name: not a canonical role name ([a-z][a-z0-9_]{{0,31}}).");
            if (!seen.Add(role.Name))
                throw new InvalidOperationException($"VerseVerb \"{verb.Name}\" Roles.Name: duplicate role name.");
            if (!Enum.IsDefined(typeof(VerseReferentKind), role.Binds))
                throw new InvalidOperationException($"VerseVerb \"{verb.Name}\" Roles.Binds: not a defined referent kind.");
        }
    }
}
