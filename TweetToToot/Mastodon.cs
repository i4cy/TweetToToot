using Mastonet;
using Mastonet.Entities;

namespace TweetToToot
{
    public class Mastodon
    {
        public Mastodon(string appName, string instance, string email, string password)
        {
            this.appName = appName;
            this.instance = instance;
            this.email = email;
            this.password = password;
        }

        private MastodonClient Connect()
        {
            if (this.client == null)
            {
                var authClient = new AuthenticationClient(instance);
                var appRegistration = authClient.CreateApp(this.appName, null, null, GranularScope.Read, GranularScope.Write, GranularScope.Follow).Result;
                var auth = authClient.ConnectWithPassword(this.email, this.password).Result;

                client = new MastodonClient(this.instance, auth.AccessToken);

                client.RateLimitsUpdated += (sender, e) =>
                {
                    this.limitRemaining = Math.Min(e.Remaining, this.limitRemaining);
                };
            }

            return client;
        }

        public bool UploadToot(string text, List<string> imagePaths, string privacy)
        {
            client = Connect();

            if (this.limitRemaining > 0)
            {
                var attachments = new List<Attachment>();

                int i = 0;
                foreach (var imagePath in imagePaths)
                {
                    using (var fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                    {
                        var attachment = client.UploadMedia(fs, imagePath).Result;
                        attachments.Add(attachment);
                    }
                    if (++i > 3)
                        break;
                }

                Visibility visibility = (Visibility)Enum.Parse(typeof(Visibility), privacy, true);
                var status = client.PublishStatus(text, visibility, mediaIds: attachments.Select(a => a.Id)).Result;
                status = client.GetStatus(status.Id).Result;

                Thread.Sleep(1000);
            }

            return true;
        }

        public bool CheckRateLimit(Action RateLimitReached)
        {
            if (this.limitRemaining <= 1)
            {
                RateLimitReached();

                int cursorLeft = Console.CursorLeft;

                DateTime restart = DateTime.Now + TimeSpan.FromMinutes(ResetLimitWaitTime);
                TimeSpan remaining;
                do
                {                    
                    Console.SetCursorPosition(cursorLeft, Console.CursorTop);
                    remaining = restart - DateTime.Now;
                    Console.Write("{0}.", remaining.ToString(@"hh\:mm\:ss"));

                    Thread.Sleep(500);
                }
                while (remaining > TimeSpan.Zero);

                this.limitRemaining = ResetLimitBatchSize;

                Console.WriteLine("");
            }

            return true;
        }

        private string appName;
        private string instance;
        private string email;
        private string password;

        private const int ResetLimitBatchSize = 30;                  
        private const double ResetLimitWaitTime = 40.0F;              

        private int limitRemaining = ResetLimitBatchSize;

        private MastodonClient client = null;
    }
}
