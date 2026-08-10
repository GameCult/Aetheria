using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RethinkDb.Driver;
using RethinkDb.Driver.Ast;
using RethinkDb.Driver.Net;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ChatApp.Server
{
    class Program
    {
        private static readonly RethinkDB R = RethinkDB.R;
        private static ILogger _logger;
        
        static async Task Main(string[] args)
        {
            var serilogger = new LoggerConfiguration()
                .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    // .AddFilter("Microsoft", LogLevel.Warning)
                    // .AddFilter("System", LogLevel.Warning)
                    // .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                    .AddSerilog(serilogger)
                    .AddConsole();
            });
            _logger = loggerFactory.CreateLogger<Program>();
            
            // Register extensions to Json.NET Serialization
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                Converters = new List<JsonConverter>
                {
                    new MathJsonConverter(),
                    Converter.DateTimeConverter,
                    Converter.BinaryConverter,
                    Converter.GroupingConverter,
                    Converter.PocoExprConverter
                }
            };
            Converter.Serializer.Converters.Add(new MathJsonConverter());
        
            // Register extensions to MessagePack Serialization
            var resolver = CompositeResolver.Create(
                MathResolver.Instance,
                NativeGuidResolver.Instance,
                StandardResolver.Instance
            );
            var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);
            MessagePackSerializer.DefaultOptions = options;
            
            var connection = R.Connection().Hostname("asgard.gamecult.games")
                .Port(RethinkDBConstants.DefaultPort).Timeout(60).Connect();
            
            var cache = new DatabaseCache();
            
            // When entries are changed locally, push the changes to RethinkDB
            cache.OnDataUpdateLocal += async entry =>
            {
                var table = entry.GetType().GetCustomAttribute<RethinkTableAttribute>()?.TableName ?? "Other";
                var result = await R.Db("Aetheria").Table(table).Update(entry).RunAsync(connection);
                _logger.Log(LogLevel.Information, $"Uploaded entry to RethinkDB: {entry.ID} result: {result}");
            };
                
            cache.OnDataInsertLocal += async entry =>
            {
                var table = entry.GetType().GetCustomAttribute<RethinkTableAttribute>()?.TableName ?? "Other";
                var inserted = false;
                for (int i = 0; i < 5 && !inserted; i++)
                {
                    try
                    {
                        var result = await R.Db("Aetheria").Table(table).Insert(entry).RunAsync(connection);
                        _logger.Log(LogLevel.Information, $"Inserted entry to RethinkDB: {entry.ID} result: {result}");
                        inserted = true;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e,e.Message);
                    }
                }
                if(!inserted)
                    _logger.LogError("Failed to insert after 5 attempts!");
            };
            
            cache.OnDataDeleteLocal += async entry =>
            {
                var table = entry.GetType().GetCustomAttribute<RethinkTableAttribute>()?.TableName ?? "Other";
                var result = await R.Db("Aetheria").Table(table).Get(entry.ID).Delete().RunAsync(connection);
                _logger.Log(LogLevel.Information, $"Deleted entry from RethinkDB: {entry.ID} result: {result}");
            };
        
            // Get data from RethinkDB
            await GetTable("Items", connection, cache);
            await GetTable("Galaxy", connection, cache);
            await GetTable("Users", connection, cache);
            
            // Subscribe to changes from RethinkDB
            SubscribeTable("Items", connection, cache);
            SubscribeTable("Galaxy", connection, cache);
            SubscribeTable("Users", connection, cache);
            
            var server = new MasterServer(cache)
            {
                Logger = _logger
            };
            server.Start();
            
            var context = new GameContext(cache, s=>_logger.Log(LogLevel.Information, s));

            context.MapLayers = cache.GetAll<GalaxyMapLayerData>().ToDictionary(m => m.Name);
            
            server.AddMessageListener<GalaxyRequestMessage>(galaxyRequest => galaxyRequest.Peer.Send(
                new GalaxyResponseMessage
                {
                    Zones = cache.GetAll<ZoneData>().Select(zd=>
                        new GalaxyResponseZone
                        {
                            Name = zd.Name,
                            Position = zd.Position,
                            ZoneID = zd.ID,
                            Links = zd.Wormholes?.ToArray() ?? Array.Empty<Guid>()
                        }).ToArray(),
                    GlobalData = context.GlobalData,
                    StarDensity = context.MapLayers["StarDensity"]
                }));
            
            server.AddMessageListener<BlueprintsRequestMessage>(blueprintRequest => blueprintRequest.Peer.Send(
                new BlueprintsResponseMessage
                {
                    Blueprints = cache.GetAll<BlueprintData>().ToArray()
                }));
            
            server.AddMessageListener<ZoneRequestMessage>(zoneRequest =>
            {
                var zone = cache.Get<ZoneData>(zoneRequest.ZoneID);
                
                // Zone has not been populated, generate the contents now!
                if (!zone.Visited)
                {
                    zone.Visited = true;
                    OrbitData[] orbits;
                    PlanetData[] planets;
                    ZoneGenerator.GenerateZone(
                        global: context.GlobalData,
                        zone: zone,
                        mapLayers: context.MapLayers.Values,
                        resources: cache.GetAll<SimpleCommodityData>().Where(i=>i.ResourceDensity.Any()),
                        orbitData: out orbits,
                        planetsData: out planets);
                    cache.AddAll(orbits);
                    cache.AddAll(planets);
                    cache.Add(zone);
                }
                zoneRequest.Peer.Send(
                    new ZoneResponseMessage
                    {
                        Zone = zone,
                        Contents = zone.Orbits.Select(id=>cache.Get(id))
                            .Concat(zone.Planets.Select(id=>cache.Get(id)))
                            .Concat(zone.Stations.Select(id=>cache.Get(id))).ToArray()
                    });
            });

            Observable.Timer(DateTimeOffset.Now, TimeSpan.FromSeconds(60)).SubscribeOn(NewThreadScheduler.Default).Subscribe(_ =>
                {
                    _logger.Log(LogLevel.Information, R.Now().Run<DateTime>(connection).ToString() as string);
                });

            while (true)
            {
                Thread.Sleep(100);
            }
        }

        private static void SubscribeTable(string table, Connection connection, DatabaseCache cache)
        {
            Task.Run(async () =>
            {
                var result = await R.Db("Aetheria").Table(table).Changes()
                    .RunChangesAsync<DatabaseEntry>(connection);
                while (await result.MoveNextAsync())
                {
                    var change = result.Current;
                    if (change.OldValue == null)
                    {
                        _logger.Log(LogLevel.Information,
                            $"Received change from RethinkDB {table} table (Entry Created): {change.NewValue.GetType()} {(change.NewValue as INamedEntry)?.EntryName ?? ""}:{change.NewValue.ID}");
                        cache.Add(change.NewValue, true);
                    }
                    else if (change.NewValue == null)
                    {
                        _logger.Log(LogLevel.Information,
                            $"Received change from RethinkDB {table} table (Entry Deleted): {change.OldValue.GetType()} {(change.OldValue as INamedEntry)?.EntryName ?? ""}:{change.OldValue.ID}");
                        cache.Delete(change.NewValue, true);
                    }
                    else
                    {
                        _logger.Log(LogLevel.Information,
                            $"Received change from RethinkDB {table} table: {change.NewValue.GetType()} {(change.NewValue as INamedEntry)?.EntryName ?? ""}:{change.NewValue.ID}");
                        cache.Add(change.NewValue, true);
                    }
                }
            });
        }

        private static async Task GetTable(string table, Connection connection, DatabaseCache cache)
        {
            var result = await R.Db("Aetheria").Table(table).RunCursorAsync<DatabaseEntry>(connection);
            while (await result.MoveNextAsync())
            {
                var entry = result.Current;
                _logger.Log(LogLevel.Information,
                    $"Received {table} entry from RethinkDB: {entry.GetType()} {(entry as INamedEntry)?.EntryName ?? ""}:{entry.ID}");
                cache.Add(entry, true);
            }
        }
    }
}
