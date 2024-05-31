using System.CommandLine;

namespace TweetToToot
{
    public class Program
    {
        static int Main(string[] args)
        {
            int returnCode = 0;

            var rootCommand = new RootCommand(
                "The i4cy TweetToToot program for uploading X (formerly known as twitter) tweets to Mastodon as new toots.");

            var appArgument = new Argument<string>("app", "Your mastodon application name.");
            var instanceArgument = new Argument<string>("instance", "Your mastodon instance name.");
            var emailArgument = new Argument<string>("email", "Your mastodon email address.");
            var passwordArgument = new Argument<string>("password", "Your mastodon password.");
            var archiveArgument = new Argument<string>("archive", "Your twitter archive local folder location.");

            var actionOption = new Option<string>(new[] { "--action", "-a" }, () => "Display", "App action to perform (Display | Upload.");
            var privacyOption = new Option<string>(new[] { "--privacy", "-p" }, () => "Public", "Toot privacy level (Direct | Private | Public | Unlisted).");
            var dateStampOption = new Option<bool>(new[] { "--dateStamp", "-d" }, () => false, "Add original creation date stamp to each toot.");
            var startIndexOption = new Option<int>(new[] { "--startIndex", "-s" }, () => 0, "Start upload at this toot index, where 0 is first index.");
            var endIndexOption = new Option<int>(new[] { "--endIndex", "-e" }, () => -1, "End upload at this toot index, where -1 is last index.");
            var omitIndicesOption = new Option<string>(new[] { "--omitIndices", "-o" }, () => "", "List of comma separated toot indices to omit.");
            var resolveUrlOption = new Option<bool>(new[] { "--resolveUrl", "-r" }, () => false, "Resolve link shortened URLs to actual URLs.");

            rootCommand.AddArgument(appArgument);
            rootCommand.AddArgument(instanceArgument);
            rootCommand.AddArgument(emailArgument);
            rootCommand.AddArgument(passwordArgument);
            rootCommand.AddArgument(archiveArgument);

            rootCommand.AddOption(actionOption);
            rootCommand.AddOption(privacyOption);
            rootCommand.AddOption(dateStampOption);
            rootCommand.AddOption(startIndexOption);
            rootCommand.AddOption(endIndexOption);
            rootCommand.AddOption(omitIndicesOption);
            rootCommand.AddOption(resolveUrlOption);

            rootCommand.SetHandler(context =>
            {
                string app = context.ParseResult.GetValueForArgument(appArgument);
                string instance = context.ParseResult.GetValueForArgument(instanceArgument);
                string email = context.ParseResult.GetValueForArgument(emailArgument);
                string password = context.ParseResult.GetValueForArgument(passwordArgument);
                string archive = context.ParseResult.GetValueForArgument(archiveArgument);

                string action = context.ParseResult.GetValueForOption(actionOption).ToLower();
                string privacy = context.ParseResult.GetValueForOption(privacyOption).ToLower();
                bool dateStamp = context.ParseResult.GetValueForOption(dateStampOption);
                int startIndex = context.ParseResult.GetValueForOption(startIndexOption);
                int endIndex = context.ParseResult.GetValueForOption(endIndexOption);
                string omitIndices = context.ParseResult.GetValueForOption(omitIndicesOption);
                bool resolveUrl = context.ParseResult.GetValueForOption(resolveUrlOption);

                var cancellationToken = context.GetCancellationToken();

                returnCode = TweetsToToots(
                    app, instance, email, password, archive,
                    action, privacy, dateStamp, startIndex, endIndex, omitIndices, resolveUrl, cancellationToken);
            });

            rootCommand.InvokeAsync(args);

            return returnCode;
        }

        private static int TweetsToToots(
            string app, string instance, string email, string password, string archive,
            string action, string privacy, bool dateStamp, int startIndex, int endIndex, string omitIndices, bool resolveUrl, CancellationToken cancellationToken)
        {
            try
            {
                if (action == "display")
                {
                    TwitterArchive twitterArchive = new TwitterArchive(archive, startIndex, endIndex, omitIndices, resolveUrl);
                    List<TwitterArchive.Post> posts = twitterArchive.GetPosts();

                    twitterArchive.DisplayPosts(posts);

                    Console.WriteLine("Done Displaying - Press any key to exit.");
                }
                else if (action == "upload")
                {
                    TwitterArchive twitterArchive = new TwitterArchive(archive, startIndex, endIndex, omitIndices, resolveUrl);
                    List<TwitterArchive.Post> posts = twitterArchive.GetPosts();

                    Mastodon mastodon = new Mastodon(app, instance, email, password);

                    foreach (var post in posts)
                    {
                        if (post.Omit == false)
                        {
                            foreach (var url in post.Urls)
                            {
                                post.Text += url.ToString() + "\n";
                            }

                            if (dateStamp == true)
                            {
                                post.Text += "\n[" + post.Created.ToShortDateString() + "]";
                            }

                            _ = mastodon.UploadToot(post.Text, post.Media, privacy);
                            Console.WriteLine("Uploaded to {0} - Toot index {1:D5}", instance, post.Index);

                            Action rateLimitReached = () => Console.Write("Paused - Rate limit reached, restarting in ");
                            mastodon.CheckRateLimit(rateLimitReached);
                        }
                    }
                    Console.WriteLine("Done Uploading - Press any key to exit.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.InnerException.Message);
                Console.WriteLine("Fatal Error - Press any key to exit.");
            }

            Console.ReadKey();
            return 0;
        }
    }
}
