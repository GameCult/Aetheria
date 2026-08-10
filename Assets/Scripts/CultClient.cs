/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LiteNetLib;
using MessagePack;
using MessagePack.Resolvers;
using UniRx;
using UnityEngine;

public static class CultClient {
    public static Action<string> Logger = s => 
        MainThreadDispatcher.Post(_=>Debug.Log(s),null);

    public static event Action<string> OnError;
    public static int Ping;

    private static NetManager _client;
    private static NetPeer _peer;
    private static Dictionary<Type, NotAnActionCollection> _messageCallbacks = new Dictionary<Type, NotAnActionCollection>();
    private static Guid _token;
    private static MD5 _hasher = MD5.Create();
    private static string _lastHost;
    private static int _lastPort;

    public static bool Verified => _token != Guid.Empty;
    public static bool Connected => _client != null;

    public static void Send<T>(T m) where T : Message
    {
        if (Connected)
        {
            if (Verified || m is LoginMessage || m is RegisterMessage || m is VerifyMessage)
            {
                Logger($"Sending message {MessagePackSerializer.SerializeToJson(m as Message)}");
                _peer.Send(m);
            }
            else OnError?.Invoke("Cannot send, client is not verified!");
        }
        else OnError?.Invoke("Cannot send, client is not connected!");
    }

    public static void ClearMessageListeners()
    {
        _messageCallbacks.Clear();
    }

    public static void AddMessageListener<T>(Action<T> callback) where T : Message
    {
        if (!_messageCallbacks.ContainsKey(typeof(T)))
            _messageCallbacks[typeof(T)] = new ActionCollection<T>();
        ((ActionCollection<T>)_messageCallbacks[typeof(T)]).Add(callback);
    }

    public static void Login(string auth, string password)
    {
        if (auth.Length>0)
        {
            if (8 <= password.Length && password.Length < 32)
                Send(new LoginMessage
                {
                    Auth = auth,
                    Password = _hasher.ComputeHash(Encoding.UTF8.GetBytes(password))
                });
            else OnError?.Invoke("Password Invalid: needs to be at least 8 characters");
        }
        else OnError?.Invoke("Email/Username Empty");
    }

    public static void Register(string email, string username, string password)
    {
        if (email.Length > 0)
        {
            if (username.Length > 0)
            {
                if (8 <= password.Length && password.Length < 32)
                    Send(new RegisterMessage
                    {
                        Email = email,
                        Name = username,
                        Password = _hasher.ComputeHash(Encoding.UTF8.GetBytes(password))
                    });
                else OnError?.Invoke("Password Invalid: needs to be at least 8 characters");
            }
            else OnError?.Invoke("Username Empty");
        }
        else OnError?.Invoke("Email Empty");
    }

    public static void Connect(string host = "localhost", int port = 3075)
    {
        _lastHost = host;
        _lastPort = port;
        // Set extensions to default resolver.
        var resolver = CompositeResolver.Create(
            MathResolver.Instance,
            NativeGuidResolver.Instance,
            StandardResolver.Instance
        );
        var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);
        MessagePackSerializer.DefaultOptions = options;
        
        EventBasedNetListener listener = new EventBasedNetListener();
        _client = new NetManager(listener)
        {
//            UnsyncedEvents = true,
            NatPunchEnabled = true
        };
        _client.Start(3074);
        _peer = _client.Connect(host, port, "aetheria-cc65a44d");
        Observable.EveryUpdate().Subscribe(_ => _client.PollEvents());
        listener.NetworkErrorEvent += (point, code) => Logger($"{point.Address}: Error {code}");
        listener.NetworkReceiveEvent += (peer, reader, method) =>
        {
            var bytes = reader.GetRemainingBytes();
            Logger($"Received message: {MessagePackSerializer.ConvertToJson(new ReadOnlyMemory<byte>(bytes))}");
            var message = MessagePackSerializer.Deserialize<Message>(bytes);
            var type = message.GetType();
            
            if (!Verified)
            {
                switch (message)
                {
                    case LoginSuccessMessage loginSuccess:
                        _token = loginSuccess.Session;
                        break;
                    case NetErrorMessage error:
                        OnError?.Invoke(error.Error);
                        break;
                }
            }
            
            if (_messageCallbacks.ContainsKey(type))
                typeof(ActionCollection<>).MakeGenericType(new[] {type}).GetMethod("Invoke")
                    .Invoke(_messageCallbacks[type], new object[] {message});
            else
                Logger(
                    $"Received {type.Name} message but no one is listening for it so I'll just leave it here " +
                    $"¯\\_(ツ)_/¯\n{MessagePackSerializer.ConvertToJson(new ReadOnlyMemory<byte>(bytes))}");

        };
        
        listener.PeerConnectedEvent += peer =>
        {
            Logger($"Peer {peer.EndPoint.Address}:{peer.EndPoint.Port} connected.");
            _peer = peer;
            if(Verified)
                peer.Send(new VerifyMessage{Session = _token});
        };
        
        listener.PeerDisconnectedEvent += (peer, info) =>
        {
            Logger($"Peer {peer.EndPoint.Address}:{peer.EndPoint.Port} disconnected: {info.Reason}.");
            _peer = null;
            Connect(_lastHost, _lastPort);
        };
        
        listener.NetworkLatencyUpdateEvent +=
            (peer, latency) => Ping = latency; //Logger($"Ping received: {latency} ms");
    }
}
