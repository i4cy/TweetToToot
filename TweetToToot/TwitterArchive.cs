using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TweetToToot
{
    public class TwitterArchive
    {
        public class Post
        {
            public int Index { get; internal set; }
            public bool Omit { get; internal set; }
            public DateTime Created { get; internal set; }
            public string Text { get; internal set; }
            public List<string> Media { get; internal set; } = new List<string>();
            public List<Uri> Urls { get; internal set; } = new List<Uri>();
        }

        public TwitterArchive(string archivePath, int startIndex, int endIndex, string omitIndexes, bool resolveUrls)
        {
            this.archivePath = archivePath;
            this.startIndex = startIndex;
            this.endIndex = endIndex;
            this.omitIndexes = omitIndexes;
            this.resolveUrls = resolveUrls;
        }

        public List<Post> GetPosts()
        {
            Console.WriteLine("Please wait - Reading twitter archive repository.");

            posts = new List<Post>();

            string[] lines = File.ReadAllLines(this.archivePath + @"\data\tweets.js");

            lines[0] = "[";
            string jsonString = string.Join("", lines);

            List<TweetOuter> tweets = JsonSerializer.Deserialize<List<TweetOuter>>(jsonString);

            foreach (TweetOuter tweet in tweets)
            {
                if (tweet.Tweet.InReplyToUserId != null)
                    continue;

                if (tweet.Tweet.FullText.StartsWith("RT @"))
                    continue;

                String str;
                Post post = new Post();

                str = tweet.Tweet.CreatedAt;
                post.Created = DateTime.ParseExact(
                    str, "ddd MMM dd HH:mm:ss zzzzz yyyy", CultureInfo.InvariantCulture);

                str = tweet.Tweet.FullText;
                post.Text = str;

                if (tweet.Tweet.Entities.Media != null)
                {
                    foreach (var media in tweet.Tweet.ExtendedEntities.Media)
                    {
                        str = media.MediaUrl;
                        str = Path.GetFileName(str);

                        string[] filename = Directory.GetFiles(this.archivePath + @"\data\tweets_media", "*" + str);
                        if (filename.Length == 0)
                        {
                            continue;
                        }
                        else
                        {
                            str = filename[0];
                            post.Media.Add(str);
                        }
                    }
                }

                if (tweet.Tweet.Entities.Urls != null)
                {
                    foreach (var url in tweet.Tweet.Entities.Urls)
                    {
                        str = url.ExpandedUrl;
                        post.Urls.Add(new Uri(str));
                    }
                }

                posts.Add(post);
            }

            posts = posts.OrderBy(x => x.Created).ToList();

            posts = posts.Select((x, index) => { x.Index = index; return x; }).ToList();

            if (!string.IsNullOrEmpty(this.omitIndexes))
            {
                int[] omit = this.omitIndexes.Split(',').Select(int.Parse).ToArray();
                posts.ForEach(x => x.Omit = omit.Any(index => index == x.Index));
            }

            if (this.endIndex == -1)
                this.endIndex = posts.Count - 1;
            this.endIndex = Math.Min(this.endIndex, posts.Count - 1);
            posts = posts.Skip(this.startIndex).Take(this.endIndex - this.startIndex + 1).ToList();

            if (this.resolveUrls == true)
            {
                Console.WriteLine("Please Wait - Resolving link shortened URLs to actual URLs.");

                foreach (Post post in posts)
                {
                    post.Text = DeTwitterText(post.Text);
                    Console.Write(".");
                }

                Console.WriteLine();
            }
            return posts;
        }

        public void DisplayPosts(List<Post> posts)
        {
            foreach (Post post in posts)
            {
                Console.WriteLine("{1} Index {0:D5} {1}", post.Index, (post.Omit == true) ? "XXX" : "---");
                Console.WriteLine(post.Created);

                foreach (string media in post.Media)
                    Console.WriteLine(media);

                foreach (Uri url in post.Urls)
                    Console.WriteLine(url);

                Console.WriteLine(post.Text);
                Console.WriteLine("");
            }
        }

        private string DeTwitterText(string text)
        {
            Regex regx = new Regex(@"http[^\s]+", RegexOptions.IgnoreCase);
            MatchCollection urls = regx.Matches(text);
            foreach (Match url in urls)
            {
                Uri newUrl = GetRedirectedUrl(new Uri(url.Value)).Result;
                if (newUrl != null)
                {
                    if (newUrl.Host == "x.com" || newUrl.Host == "twitter.com" || newUrl.Host == "t.co")
                    {
                        text = text.Replace(url.Value, "");
                    }
                    else
                    {
                        text = text.Replace(url.Value, newUrl.ToString());
                    }
                }
            }
            return text;
        }

        private async Task<Uri> GetRedirectedUrl(Uri url)
        {
            var client = new HttpClient();
            var response = await client.GetAsync(url);

            return response.RequestMessage.RequestUri;
        }

        private List<Post> posts;
        private string archivePath;
        private int startIndex;
        private int endIndex;
        private string omitIndexes;
        private bool resolveUrls;
        #region Tweets Json Class Schema.

        private class TweetOuter
        {
            [JsonPropertyName("tweet")]
            public Tweet Tweet { get; set; }
        }

        private class Tweet
        {
            [JsonPropertyName("full_text")]
            public string FullText { get; set; }

            [JsonPropertyName("in_reply_to_user_id_str")]
            public string InReplyToUserId { get; set; }            

            [JsonPropertyName("entities")]
            public Entities Entities { get; set; }

            [JsonPropertyName("extended_entities")]
            public Entities ExtendedEntities { get; set; }

            [JsonPropertyName("created_at")]
            public string CreatedAt { get; set; }
        }

        private class Entities
        {
            [JsonPropertyName("media")]
            public List<Medium> Media { get; set; }

            [JsonPropertyName("urls")]
            public List<Url> Urls { get; set; }
        }

        private class Medium
        {
            [JsonPropertyName("media_url")]
            public string MediaUrl { get; set; }
        }

        private class Url
        {
            [JsonPropertyName("expanded_url")]
            public string ExpandedUrl { get; set; }
        }

        #endregion
    }
}
