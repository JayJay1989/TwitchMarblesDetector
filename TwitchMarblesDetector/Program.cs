using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using TwitchLib.Api;
using TwitchLib.Api.Interfaces;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client.Models;
using TwitchMarblesDetector.Models;
using TwitchMarblesDetector.Service;

namespace TwitchMarblesDetector
{
    class Program
    {
        private Dictionary<string, ChatService> chatServices;
        private ITwitchClient client;
        private ITwitchAPI api;
        private Config _config;
        private ILogger logger;
        private System.Timers.Timer timer;
        private string userId;

        public Program()
        {
            Console.WriteLine("╔═════════════════════════════════════════════════════════╗");
            Console.WriteLine("║               Marbles on Stream Detector                ║");
            Console.WriteLine("╠═════════════════════════════════════════════════════════╣");
            Console.WriteLine("║  By JayJay1989BE                                        ║");
            Console.WriteLine("╚═════════════════════════════════════════════════════════╝");
            string logTemplate =
                "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}";
            LogEventLevel level = LogEventLevel.Information;

            var config = new ConfigurationBuilder()
                    .SetBasePath(Environment.CurrentDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .Build();
            _config = config.GetSection("Settings").Get<Config>();

            if (_config.Debug)
                level = LogEventLevel.Debug;

            var log = new LoggerConfiguration();
            if (_config.Debug)
                log.MinimumLevel.Debug();

            logger = log.WriteTo.Console(level, logTemplate)
                .WriteTo.File("logs/log_.txt", level, logTemplate, rollingInterval: RollingInterval.Day)
                .CreateLogger();

            chatServices = new Dictionary<string, ChatService>();
        }

        static void Main(string[] args)
        {
            Console.CursorVisible = false;
            Console.Clear();
            new Program()
                .MainAsync(args)
                .GetAwaiter()
                .GetResult();
        }

        async Task MainAsync(string[] args)
        {
            api = new TwitchAPI();
            api.Settings.ClientId = _config.Credentials.Helix.ClientId;
            api.Settings.Secret = _config.Credentials.Helix.ClientSecret;
            var myChannel = await api.Helix.Users.GetUsersAsync(logins: new List<string>() {_config.Credentials.Chat.Username});
            if (myChannel != null && myChannel.Users.Length > 0) userId = myChannel.Users[0].Id;
            timer = new System.Timers.Timer(_config.SearchDelay * 60 * 1000);
            timer.Elapsed += async (obj, e) => await SearchStream();
            timer.Enabled = true;

            ConnectionCredentials credentials = new ConnectionCredentials(_config.Credentials.Chat.Username, _config.Credentials.Chat.Oauth);
            client = new TwitchClient();
            client.Initialize(credentials);
            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnConnected += ClientOnOnConnected;
            client.OnDisconnected += ClientOnDisconnected;
            client.OnConnectionError += ClientOnConnectionError;
            client.OnError += ClientOnError;
            client.Connect();

            foreach (string channel in _config.Channels)
            {
                var chatService = ChatService.ChatServiceFactory(channel.ToLower(), _config.Delay, _config.CountAmount, logger);
                chatServices.Add(channel.ToLower(), chatService);
                client.JoinChannel(channel.ToLower());
                chatService.Handler += ChatService_Handler;
                chatService.Start();
                logger.Information($"channel {channel} joined!");
            }

            await SearchStream();

            timer.Start();

            await Task.Delay(Timeout.Infinite);
        }

        #region Event Handlers
        private void ClientOnError(object sender, TwitchLib.Communication.Events.OnErrorEventArgs e) =>
            logger.Error($"Error: {e.Exception.Message}");

        private void ClientOnConnectionError(object? sender, OnConnectionErrorArgs e) =>
            logger.Error($"Error while connecting: {e.Error.Message}");

        private void ClientOnDisconnected(object sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e) =>
            logger.Warning($"Client disconnected");

        private void ClientOnOnConnected(object sender, OnConnectedArgs e) =>
            logger.Information($"Client connected");

        private void ChatService_Handler(object sender, string channel) =>
            client.SendMessage(channel, "!play");

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            var chatService = chatServices.GetValueOrDefault(e.ChatMessage.Channel);
            if (e.ChatMessage.Message.Length >= 5)
            {
                var message = e.ChatMessage.Message;
                foreach (string detectMessage in _config.DetectMessages)
                {
                    if (message.StartsWith(detectMessage))
                        chatService?.CountMessage();
                }
            }
        }

        private async Task SearchStream()
        {
            logger.Information($"Currently in {chatServices.Count} channels");
            logger.Debug("Searching new streamers..");

            var channels = await api.Helix.Streams.GetStreamsAsync(null, null, 5, new List<string> { "509511" }, null, "live");
            logger.Debug($"Found {channels.Streams.Length}...");
            int i = 0;
            foreach (var channel in channels.Streams)
            {
                if (channel.ViewerCount > 25 && !chatServices.ContainsKey(channel.UserLogin.ToLower()))
                {
                    i++;
                    await Task.Delay(100);
                    var chatService = ChatService.ChatServiceFactory(channel.UserLogin.ToLower(), _config.Delay, _config.CountAmount, logger);
                    chatServices.Add(channel.UserLogin.ToLower(), chatService);
                    client.JoinChannel(channel.UserLogin.ToLower());
                    chatService.Handler += ChatService_Handler;
                    chatService.Start();
                    logger.Information($"channel {channel.UserLogin} joined!");
                }
            }
            logger.Debug($"Joined {i} channels...");


            logger.Debug("Starting cleanup..");

            foreach (var (username, chatService) in chatServices)
            {
                await Task.Delay(100);
                if (!_config.Channels.Contains(username))
                {
                    var poke = await api.Helix.Search.SearchChannelsAsync(username);
                    var channel = poke.Channels.SingleOrDefault(x => x.BroadcasterLogin.Equals(username));
                    if (channel != null && channel.GameId != "509511")
                    {
                        chatService.Stop();
                        chatServices.Remove(username);
                        client.LeaveChannel(username);
                        logger.Information($"channel {username} left!");
                    }
                }
            }
        }

        #endregion

    }
}