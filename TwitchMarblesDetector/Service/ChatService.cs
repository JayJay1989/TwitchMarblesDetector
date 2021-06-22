using System;
using System.Timers;
using Serilog;

namespace TwitchMarblesDetector.Service
{
    internal class ChatService
    {
        private string _channel;
        private float _minutes;
        private ILogger _logger;
        private Timer _timer;
        private int _counter;
        private bool _event;
        private int _amount = 3;

        public event EventHandler<string> Handler;

        private ChatService(string channel, float minutes, ILogger logger = null)
        {
            _channel = channel;
            _minutes = minutes;
            _logger = logger;

            _timer = new Timer(minutes * 60 * 1000);
            _timer.Elapsed += timer_Elapsed;
            _timer.Enabled = true;
        }

        public static ChatService ChatServiceFactory(string channel, float minutes, int amount = 3, ILogger logger = null)
        {
            return new ChatService(channel, minutes, logger).SetAmount(amount);
        }

        public void Start()
        {
            _timer.Start();
            _logger?.Debug($"Started service for channel {_channel} with a timer of {_minutes} minutes and a count of {_amount}...");
        }

        private ChatService SetAmount(int countAmount)
        {
            if (countAmount <= 2) throw new ArgumentException("countArgument");
            _amount = countAmount;
            return this;
        }

        public void Stop()
        {
            _timer.Stop();
            _logger?.Information($"Stopped service for channel {_channel}!");
        }

        public void Reset()
        {
            _logger?.Information($"Resetting service for channel {_channel}...");
            Stop();
            Start();
        }

        void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _logger?.Debug($"Timer elapsed for channel: {_channel}, they had about {_counter} players");
            _counter = 0;
            _event = false;
        }

        public void CountMessage()
        {
            _counter++;
            _logger?.Debug($"{_channel}: {_counter}");
            if (_counter > _amount && !_event)
            {
                Handler?.Invoke(this, _channel);
                _event = true;
            }
        }
    }
}