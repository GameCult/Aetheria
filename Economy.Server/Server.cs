﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
 using System.Reactive.Linq;
 using System.Security.Cryptography;
 using System.Text.RegularExpressions;
using Isopoh.Cryptography.Argon2;
using LiteNetLib;
using MessagePack;
using Microsoft.Extensions.Logging;

 public class MasterServer
 {
    public ILogger Logger = null;
    
    private const string EmailPattern =
        @"^([0-9a-zA-Z]" + //Start with a digit or alphabetical
        @"([\+\-_\.][0-9a-zA-Z]+)*" + // No continuous or ending +-_. chars in email
        @")+" +
        @"@(([0-9a-zA-Z][-\w]*[0-9a-zA-Z]*\.)+[a-zA-Z0-9]{2,17})$";

    private const string UsernamePattern = @"^[A-Za-z0-9]+(?:[ _-][A-Za-z0-9]+)*$";
    private readonly Dictionary<Type, NotAnActionCollection> _messageCallbacks = new Dictionary<Type, NotAnActionCollection>();
    private readonly Dictionary<long, User> _users = new Dictionary<long, User>();
    private readonly Dictionary<Guid, Session> _sessions = new Dictionary<Guid, Session>();
    private List<User> _readyPlayers = new List<User>();
    private NetManager _netManager;
    private Stopwatch _timer;
    private Random _random = new Random();
    private readonly DatabaseCache _database;
    private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

    private float Time => (float)_timer.Elapsed.TotalSeconds;

    public bool IsValidEmail(string email) => Regex.IsMatch(email, EmailPattern);
    public bool IsValidUsername(string name) => Regex.IsMatch(name, UsernamePattern);

    public MasterServer(DatabaseCache cache)
    {
        _database = cache;
    }

    public void ClearMessageListeners()
    {
        _messageCallbacks.Clear();
    }

    public void AddMessageListener<T>(Action<T> callback) where T : Message
    {
        if (!_messageCallbacks.ContainsKey(typeof(T)))
            _messageCallbacks[typeof(T)] = new ActionCollection<T>();
        ((ActionCollection<T>)_messageCallbacks[typeof(T)]).Add(callback);
    }

    public void Stop()
    {
        _netManager.Stop();
    }

    public void Start()
    {
        _timer = new Stopwatch();
        _timer.Start();

        EventBasedNetListener listener = new EventBasedNetListener();
        _netManager = new NetManager(listener)
        {
            UnsyncedEvents = true,
            NatPunchEnabled = true,
        };
        _netManager.Start(3075);

        listener.NetworkErrorEvent += (point, code) => Logger.Log(LogLevel.Debug, $"{point.Address}: Error {code}");

        listener.ConnectionRequestEvent += request => request.AcceptIfKey("aetheria-cc65a44d");

        listener.PeerConnectedEvent += peer =>
        {
            Logger.Log(LogLevel.Debug, $"User Connected: {peer.EndPoint}"); // Show peer ip
            _users[peer.Id] = new User {Peer = peer};
        };

        listener.PeerDisconnectedEvent += (peer, info) =>
        {
            Logger.Log(LogLevel.Debug, $"User Disconnected: {peer.EndPoint}"); // Show peer ip

//            foreach (var verifiedUser in _users.Values.Where(IsVerified))
//                verifiedUser.Peer.Send("PlayerLeft", SessionData(verifiedUser.Peer).Username);

            _users.Remove(peer.Id);
        };

        listener.NetworkLatencyUpdateEvent += (peer, latency) =>
        {
//            Logger($"Received Ping: {latency}");
            _users[peer.Id].Latency = latency;
        };

        listener.NetworkReceiveEvent += (peer, reader, method) =>
        {
            var bytes = reader.GetRemainingBytes();
            var message = MessagePackSerializer.Deserialize<Message>(bytes);
            Logger.Log(LogLevel.Information, $"Received message: {MessagePackSerializer.ConvertToJson(new ReadOnlyMemory<byte>(bytes))}");
            if (message == null)
                return;
            message.Peer = peer;
            var user = _users[peer.Id];
            var type = message.GetType();

            if (type == typeof(LoginMessage) || type == typeof(RegisterMessage) || type == typeof(VerifyMessage))
            {
                if (IsVerified(user))
                {
                    peer.Send(new LoginSuccessMessage {Session = _users[peer.Id].SessionGuid});
                    return;
                }

                Guid sessionGuid;
                switch (message)
                {
                    case RegisterMessage register when !IsValidUsername(register.Name):
                        peer.Send(new ErrorMessage {Error = "Username Invalid"});
                        return;
                    case RegisterMessage register when !IsValidEmail(register.Email):
                        peer.Send(new ErrorMessage {Error = "Email Invalid"});
                        return;
                    case RegisterMessage register:
                    {
                        sessionGuid = Guid.NewGuid();
                        peer.Send(new LoginSuccessMessage {Session = sessionGuid});
                    
                        var newUserData = new Player
                        {
                            ID = Guid.NewGuid(),
                            Email = register.Email,
                            Password = Argon2.Hash(register.Password, null, memoryCost: 16384),
                            Username = register.Name
                        };
                        _database.Add(newUserData);
    
                        _sessions[sessionGuid] = new Session {Data = newUserData, LastUpdate = DateTime.Now};
                        if (!_users.ContainsKey(peer.Id))
                            return;
                        _users[peer.Id].SessionGuid = sessionGuid;
                        break;
                    }
                    case VerifyMessage verify when _sessions.ContainsKey(verify.Session):
                        _users[peer.Id].SessionGuid = verify.Session;
                        peer.Send(new LoginSuccessMessage {Session = verify.Session});
                        return;
                    case LoginMessage login:
                    {
                        var isEmail = IsValidEmail(login.Auth);
                        var userData = _database.GetAll<Player>().FirstOrDefault(x =>
                            (isEmail ? x.Email : x.Username) == login.Auth);

                        if (userData == null)
                        {
                            peer.Send(new ErrorMessage {Error = isEmail ? "Email Not Found" : "Username Not Found"});
                            return;
                        }

                        if (!Argon2.Verify(userData.Password, login.Password))
                        {
                            peer.Send(new ErrorMessage {Error = "Password Incorrect"});
                            return;
                        }

                        sessionGuid = Guid.NewGuid();
                        peer.Send(new LoginSuccessMessage {Session = sessionGuid});

                        _sessions.Add(sessionGuid, new Session { Data = userData, LastUpdate = DateTime.Now });
                        // TODO: Intermittent: users getting disconnected before getting here, check that key exists!
                        if (!_users.ContainsKey(peer.Id))
                            return;
                        _users[peer.Id].SessionGuid = sessionGuid;
                        break;
                    }
                }
            }
            else
            {
                if (IsVerified(user))
                {
                    if (_messageCallbacks.ContainsKey(type))
                        typeof(ActionCollection<>).MakeGenericType(new[] {type}).GetMethod("Invoke")
                            .Invoke(_messageCallbacks[type], new object[] {message});
                    else Logger.Log(LogLevel.Warning, $"Received {type.Name} message but no one is listening for it so I'll just leave it here ¯\\_(ツ)_/¯\n{MessagePackSerializer.ConvertToJson(new ReadOnlyMemory<byte>(bytes))}");
                    _sessions[_users[peer.Id].SessionGuid].LastUpdate = DateTime.Now;
                }
                else
                    peer.Send(new ErrorMessage {Error = "User Not Verified"});
            }
        };

        AddMessageListener<ChatMessage>(message =>
        {
            foreach (var verifiedUser in _users.Values.Where(IsVerified))
                verifiedUser.Peer.Send(new ChatBroadcastMessage{User = SessionData(message.Peer).Username, Text = message.Text});
        });
        
        AddMessageListener<ChangeNameMessage>(message =>
        {
            SessionData(message.Peer).Username = message.Name;
            _database.Add(SessionData(message.Peer));
        });

        // Observable.Timer(DateTimeOffset.Now, TimeSpan.FromSeconds(30)).Subscribe(_ =>
        // {
        //     foreach (var s in _sessions.ToArray())
        //     {
        //         if (DateTime.Now.Subtract(s.Value.LastUpdate).TotalSeconds > s.Value.DurationSeconds)
        //             _sessions.Remove(s.Key);
        //     }
        // });
            
        Logger.LogInformation("LiteNetLib is now open to new connections. Please be gentle.");
    }

    private bool IsVerified(User u) => _sessions.ContainsKey(u.SessionGuid);

    public Player SessionData(NetPeer peer) => IsVerified(_users[peer.Id])?_sessions[_users[peer.Id].SessionGuid].Data:null;
}

 public class Session
{
    public DateTime LastUpdate;
    public Player Data;
    public float DurationSeconds;
}

public class User
{
    public NetPeer Peer;
    public int Latency;
    public Guid SessionGuid;
    public float Tardiness;
}