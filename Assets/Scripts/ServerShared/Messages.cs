/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using LiteNetLib;
using MessagePack;
using Unity.Mathematics;

[Union(0, typeof(PingMessage)),
 Union(1, typeof(LoginMessage)), 
 Union(2, typeof(RegisterMessage)),
 Union(3, typeof(VerifyMessage)), 
 Union(4, typeof(NetErrorMessage)), 
 Union(5, typeof(LoginSuccessMessage)),
 Union(6, typeof(ChatMessage)), 
 Union(7, typeof(ChatBroadcastMessage)), 
 Union(8, typeof(ChangeNameMessage)),
 MessagePackObject]
public abstract class Message
{
    [IgnoreMember] public NetPeer Peer;
}

[MessagePackObject]
public class PingMessage : Message
{
    [Key(0)] public float SendTime;
}


[MessagePackObject]
public class LoginMessage : Message
{
    [Key(0)] public string Auth;
    [Key(1)] public byte[] Password;
}

[MessagePackObject]
public class RegisterMessage : Message
{
    [Key(0)] public string Name;
    [Key(1)] public string Email;
    [Key(2)] public byte[] Password;
}

[MessagePackObject]
public class VerifyMessage : Message
{
    [Key(0)] public Guid Session;
}

[MessagePackObject]
public class NetErrorMessage : Message
{
    [Key(0)] public string Error;
}

[MessagePackObject]
public class LoginSuccessMessage : Message
{
    [Key(0)] public Guid Session;
}

[MessagePackObject]
public class ChatMessage : Message
{
    [Key(0)] public string Text;
}

[MessagePackObject]
public class ChatBroadcastMessage : Message
{
    [Key(0)] public string User;
    [Key(1)] public string Text;
}

[MessagePackObject]
public class ChangeNameMessage : Message
{
    [Key(0)] public string Name;
}
