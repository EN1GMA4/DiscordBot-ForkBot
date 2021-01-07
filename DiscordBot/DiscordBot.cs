﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Fleck;
using log4net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


    public class Discord_Bot
    {
    public Server serverr = new Server();
  public static List<IWebSocketConnection> allSockets { get; set; } = new List<IWebSocketConnection>();
    public static List<string> AliveTokens { get; set; } = new List<string>();
    public static async Task StartAsync()
        {
            await new Discord_Bot().RunAsync();
        }

        private async Task RunAsync()
        {

        //ILog logger = LogManager.GetLogger(typeof(FleckLog));
        FleckLog.Level = LogLevel.Debug;
        //FleckLog.LogAction = (level, message, ex) => {
        //    switch (level)
        //    {
        //        case LogLevel.Debug:
        //            logger.Debug(message, ex);
        //            break;
        //        case LogLevel.Error:
        //            logger.Error(message, ex);
        //            break;
        //        case LogLevel.Warn:
        //            logger.Warn(message, ex);
        //            break;
        //        default:
        //            logger.Info(message, ex);
        //            break;
        //    }
        //};
        var server = new WebSocketServer("ws://0.0.0.0:8181");
        server.Start(socket =>
        {

            socket.OnOpen = () =>
            {
                if (allSockets.Any(client => client.ConnectionInfo.ClientIpAddress == socket.ConnectionInfo.ClientIpAddress))
                {
                    var socket2 = allSockets.Find(client => client.ConnectionInfo.ClientIpAddress == socket.ConnectionInfo.ClientIpAddress);
                    try { allSockets.Remove(socket2); } catch (Exception ex) { } //Little security, dont let same ip to connect twice
                    allSockets.Add(socket);
                    
                }
                else
                {
                    try { allSockets.Remove(socket); } catch (Exception ex) { }
                    allSockets.Add(socket); 
                }
            };
            socket.OnClose = () =>
            {

                Console.WriteLine("Close!");
                allSockets.Remove(socket);
                if ((bool)serverr.CheckIfIPExist(socket.ConnectionInfo.ClientIpAddress, 0) == true)
                {
                    if (AliveTokens.Contains((string)serverr.CheckIfIPExist(socket.ConnectionInfo.ClientIpAddress, 1)))
                    {
                        AliveTokens.Remove((string)serverr.CheckIfIPExist(socket.ConnectionInfo.ClientIpAddress, 1));
                        Console.WriteLine($"{socket.ConnectionInfo.ClientIpAddress} Removed from alive tokens list");
                    }
                   
                }
            };
            socket.OnMessage = async message =>
            {
                string[] codes = (message).Split('|');
                switch (codes[0])
                {
                    case "login":
                        string token = codes[1];
                      
                        if ((!(bool)serverr.CheckAuth(token) == true) && !(bool)serverr.CheckOnhold(token,socket.ConnectionInfo.ClientIpAddress) == true)  
                        {
                            serverr.InsertOnhold(token, socket.ConnectionInfo.ClientIpAddress);
                            Console.WriteLine("Token: " + token + $" IP: {socket.ConnectionInfo.ClientIpAddress} Added to onhold list");
                           await socket.Send("status|OnHold");
                        }
                        else if ((bool)serverr.CheckAuth(token) == true && (bool)serverr.CheckAuth2(token,socket.ConnectionInfo.ClientIpAddress) == true)
                            {
                            if (!AliveTokens.Contains((string)serverr.CheckIfIPExist(socket.ConnectionInfo.ClientIpAddress, 1)))
                            {
                                AliveTokens.Add(token);
                                Console.WriteLine($"{socket.ConnectionInfo.ClientIpAddress} Added to alive tokens list");
                                await socket.Send("status|Linked");
                            }
                          
                        }
                        break;
                    case "notify":
                        if (AliveTokens.Contains((string)serverr.CheckIfIPExist(socket.ConnectionInfo.ClientIpAddress, 1)) == true)
                        {
                            string servername = codes[1];
                            string discordname = codes[2];
                            string channelid = codes[3];
                            string messageid = codes[4];
                            string eventt = codes[5];
                            string result = codes[6];



                            switch (codes[6])
                            {
                                //notify|{servername}|{discordname}|{channelid}|{messageid}|{eventt}|400|this is a test
                                case "200": //ok
                                    await Bot_Tools.NotificationControlAsync(ulong.Parse(messageid), ulong.Parse(channelid), $"Your `{EventRename(eventt)}` event for `{servername}` which executed by `{discordname}` was successful.", 200);
                                    break;

                                case "400": //error
                                    string error = codes[7];
                                    await Bot_Tools.NotificationControlAsync(ulong.Parse(messageid), ulong.Parse(channelid), $"Your `{EventRename(eventt)}` event for `{servername}` which executed by `{discordname}` was not successful, `error: {error}`", 400);
                                    break;
                            }
                        }
                        break;
                }

                //Console.WriteLine(message);

                //allSockets.ToList().ForEach(s => s.Send(message));
             
            };
        });


        string EventRename(string theevent)
        {
            switch (theevent)
            {
                case "stop":
                    return "stop";
                    break;
                    
            }
            return null;
        }
        var config = BuildConfig();
            using (var services = ConfigureServices())
            {
                var client = services.GetRequiredService<DiscordSocketClient>();
                services.GetRequiredService<LogService>();
                await client.LoginAsync(TokenType.Bot, config["token"]);
                await client.StartAsync();
                await services.GetRequiredService<CommandHandler>().InitializeAsync();
                await Task.Delay(Timeout.Infinite);
            }
             var input = Console.ReadLine();
        while (input != "exit")
        {
            foreach (var socket in allSockets.ToList())
            {
                socket.Send(input);
            }
            input = Console.ReadLine();
        }
        }

        private IConfiguration BuildConfig()
        {
            return new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("config.json").Build();
        }

        private ServiceProvider ConfigureServices()
        {
            var collection = new ServiceCollection();
            collection.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig() { MessageCacheSize = 50, AlwaysDownloadUsers = true, ExclusiveBulkDelete = true, LogLevel = LogSeverity.Verbose, GatewayIntents = GatewayIntents.DirectMessageReactions | GatewayIntents.DirectMessages | GatewayIntents.GuildBans | GatewayIntents.GuildInvites | GatewayIntents.GuildMembers | GatewayIntents.GuildMessageReactions | GatewayIntents.GuildMessages | GatewayIntents.Guilds | GatewayIntents.GuildIntegrations })); // , .TotalShards = 3}))
            collection.AddSingleton(new CommandService(new CommandServiceConfig() { LogLevel = LogSeverity.Verbose, DefaultRunMode = RunMode.Async }));
            collection.AddSingleton<CommandHandler>();
            collection.AddSingleton<LogService>();
            collection.AddSingleton<InteractiveService>();
            return collection.BuildServiceProvider();
        }
    }
