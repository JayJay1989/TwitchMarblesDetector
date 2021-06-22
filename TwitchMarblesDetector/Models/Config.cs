namespace TwitchMarblesDetector.Models
{
    public class Config
    {
        public bool Debug { get; set; }
        public float Delay { get; set; }
        public float SearchDelay { get; set; }
        public int CountAmount { get; set; }
        public Credentials Credentials { get; set; }
        public string[] Channels { get; set; }
        public string[] DetectMessages { get; set; }
    }

    public class Credentials
    {
        public Chat Chat { get; set; }
        public Helix Helix { get; set; }
    }

    public class Chat
    {
        public string Username { get; set; }
        public string Oauth { get; set; }
    }

    public class Helix
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
    }
}
