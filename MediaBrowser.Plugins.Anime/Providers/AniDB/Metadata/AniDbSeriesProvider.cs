using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Plugins.Anime.Configuration;
using MediaBrowser.Plugins.Anime.Providers.AniDB.Identity;

namespace MediaBrowser.Plugins.Anime.Providers.AniDB.Metadata
{
    public class AniDbSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private const string SeriesDataFile = "series.xml";
        private const string SeriesQueryUrl = "http://api.anidb.net:9001/httpapi?request=anime&client={0}&clientver=1&protover=1&aid={1}";
        private const string ClientName = "mediabrowser";
        // AniDB has very low request rate limits, a minimum of 2 seconds between requests, and an average of 4 seconds between requests - decreased rate due to previous rate resulting in Bans. 
        public static readonly SemaphoreSlim ResourcePool = new SemaphoreSlim(1, 1);
        public static readonly RateLimiter RequestLimiter = new RateLimiter(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(8), TimeSpan.FromMinutes(5));
        private static readonly int[] IgnoredCategoryIds = {30, 36, 38, 42, 55, 155, 157, 178, 204, 215, 226, 267, 331, 355, 380, 389, 428, 456, 464, 489, 505, 509, 513, 520, 569, 587, 596, 601, 604, 627, 635, 638, 659, 736, 775, 798, 807, 859, 861, 870, 898, 904, 920, 931, 944, 975, 998, 1046, 1065, 1100, 1133, 1136, 1157, 1178, 1179, 1208, 1221, 1249, 1253, 1254, 1262, 1270, 1282, 1283, 1286, 1291, 1363, 1365, 1377, 1386, 1393, 1428, 1438, 1444, 1446, 1471, 1492, 1500, 1528, 1539, 1559, 1563, 1572, 1632, 1633, 1634, 1656, 1662, 1677, 1681, 1705, 1709, 1719, 1730, 1754, 1758, 1761, 1783, 1816, 1835, 1843, 1869, 1903, 1913, 1985, 1994, 1996, 1999, 2019, 2037, 2047, 2060, 2079, 2090, 2091, 2107, 2131, 2145, 2174, 2197, 2214, 2216, 2226, 2253, 2259, 2302, 2309, 2343, 2345, 2346, 2363, 2378, 2384, 2392, 2393, 2394, 2396, 2406, 2419, 2420, 2439, 2455, 2460, 2461, 2467, 2481, 2484, 2487, 2488, 2520, 2538, 2544, 2558, 2568, 2605, 2606, 2607, 2608, 2609, 2610, 2611, 2612, 2613, 2624, 2625, 2626, 2627, 2629, 2630, 2631, 2632, 2633, 2634, 2635, 2641, 2659, 2660, 2661, 2662, 2665, 2670, 2672, 2675, 2682, 2683, 2684, 2708, 2713, 2751, 2756, 2763, 2764, 2765, 2769, 2777, 2780, 2781, 2788, 2793, 2797, 2798, 2800, 2810, 2811, 2815, 2819, 2820, 2822, 2824, 2825, 2829, 2830, 2831, 2832, 2834, 2836, 2848, 2865, 2873, 2876, 2879, 2880, 2886, 2891, 2909, 2927, 2928, 2931, 2945, 2954, 2959, 2963, 2964, 2973, 2980, 2996, 3007, 3012, 3017, 3018, 3020, 3025, 3028, 3037, 3043, 3052, 3053, 3062, 3075, 3082, 3088, 3094, 3097, 3101, 3102, 3106, 3108, 3119, 3121, 3123, 3132, 3135, 3138, 3142, 3146, 3147, 3148, 3155, 3164, 3166, 3168, 3170, 3173, 3175, 3178, 3191, 3192, 3196, 3202, 3206, 3216, 3220, 3227, 3233, 3237, 3240, 3244, 3245, 3250, 3260, 3264, 3266, 3277, 3282, 3283, 3289, 3301, 3305, 3314, 3316, 3317, 3323, 3326, 3327, 3328, 3329, 3342, 3356, 3359, 3365, 3369, 3375, 3379, 3392, 3395, 3399, 3410, 3419, 3423, 3429, 3431, 3433, 3444, 3447, 3459, 3469, 3472, 3479, 3481, 3486, 3487, 3496, 3499, 3502, 3503, 3504, 3510, 3512, 3513, 3516, 3517, 3523, 3525, 3528, 3529, 3534, 3536, 3539, 3546, 3551, 3554, 3560, 3573, 3580, 3581, 3585, 3590, 3592, 3597, 3600, 3601, 3610, 3615, 3618, 3622, 3647, 3650, 3664, 3667, 3671, 3683, 3696, 3697, 3700, 3701, 3702, 3703, 3712, 3721, 3722, 3727, 3743, 3745, 3748, 3750, 3755, 3771, 3772, 3779, 3785, 3795, 3798, 3804, 3805, 3806, 3813, 3823, 3824, 3826, 3827, 3832, 3834, 3837, 3838, 3842, 3848, 3849, 3850, 3867, 3868, 3870, 3874, 3875, 3881, 3884, 3886, 3906, 3908, 3921, 3935, 3941, 3944, 3951, 3955, 3960, 3969, 3970, 3975, 3981, 3987, 3993, 3998, 3999, 4003, 4009, 4017, 4024, 4030, 4033, 4039, 4042, 4051, 4053, 4057, 4060, 4064, 4069, 4073, 4075, 4085, 4089, 4090, 4095, 4110, 4112, 4115, 4116, 4117, 4138, 4148, 4149, 4151, 4154, 4164, 4168, 4171, 4172, 4176, 4182, 4184, 4192, 4193, 4199, 4200, 4206, 4213, 4217, 4225, 4227, 4228, 4236, 4237, 4244, 4248, 4254, 4264, 4268, 4274, 4278, 4280, 4281, 4290, 4296, 4298, 4299, 4301, 4304, 4311, 4312, 4314, 4323, 4328, 4330, 4337, 4343, 4344, 4348, 4349, 4352, 4353, 4356, 4363, 4364, 4380, 4383, 4385, 4386, 4389, 4405, 4411, 4413, 4421, 4427, 4459, 4460, 4462, 4465, 4472, 4475, 4477, 4479, 4481, 4496, 4499, 4506, 4509, 4510, 4515, 4524, 4538, 4542, 4543, 4552, 4555, 4560, 4564, 4566, 4570, 4572, 4573, 4575, 4576, 4582, 4586, 4587, 4590, 4596, 4598, 4606, 4609, 4610, 4616, 4640, 4646, 4664, 4682, 4702, 4719, 4722, 4730, 4753, 4761, 4786, 4804, 4809, 4811, 4814, 4830, 4849, 4860, 4874, 4887, 4898, 4907, 4908, 4916, 4927, 4933, 4935, 4939, 4943, 4945, 4946, 4947, 4951, 4968, 4992, 5023, 5030, 5046, 5047, 5052, 5053, 5064, 5077, 5093, 5095, 5104, 5119, 5132, 5147, 5161, 5162, 5164, 5172, 5186, 5188, 5205, 5227, 5244, 5255, 5257, 5284, 5290, 5307, 5321, 5335, 5343, 5345, 5368, 5385, 5388, 5407, 5409, 5417, 5420, 5428, 5439, 5455, 5458, 5499, 5521, 5529, 5530, 5546, 5553, 5554, 5561, 5583, 5585, 5586, 5594, 5600, 5601, 5613, 5616, 5633, 5644, 5654, 5665, 5679, 5686, 5700, 5705, 5708, 5717, 5720, 5725, 5727, 5733, 5735, 5746, 5762, 5763, 5767, 5769, 5787, 5788, 5802, 5803, 5804, 5805, 5814, 5818, 5820, 5822, 5838, 5840, 5846, 5852, 5865, 5867, 5868, 5872, 5876, 5878, 5882, 5883, 5890, 5897, 5900, 5907, 5910, 5911, 5915, 5918, 5937, 5939, 5943, 5944, 5947, 5962, 5965, 5967, 5980, 5984, 5988, 5992, 5994, 5996, 5998, 6003, 6023, 6035, 6037, 6039, 6041, 6046, 6050, 6052, 6054, 6063, 6066, 6069, 6074, 6076, 6077, 6093, 6097, 6106, 6111, 6115, 6137, 6148, 6149, 6151, 6153, 6173, 6184, 6185, 6186, 6191, 6222, 6230, 6232, 6233, 6234, 6242, 6243, 6246, 6247, 6248, 6249, 6332, 6347, 6348, 6349, 6358, 6371, 6397, 6400, 6415, 6446, 6453, 6494, 6528, 6537, 6557, 6563, 6596, 6610, 6640, 6717, 6850, 6901};
        private static readonly Regex AniDbUrlRegex = new Regex(@"http://anidb.net/\w+ \[(?<name>[^\]]*)\]");
        private readonly IApplicationPaths _appPaths;
        private readonly IHttpClient _httpClient;

        private readonly Dictionary<string, string> _typeMappings = new Dictionary<string, string>
        {
            {"Direction", PersonType.Director},
            {"Music", PersonType.Composer},
            {"Chief Animation Direction", "Chief Animation Director"}
        };

        public AniDbSeriesProvider(IApplicationPaths appPaths, IHttpClient httpClient)
        {
            _appPaths = appPaths;
            _httpClient = httpClient;

            TitleMatcher = AniDbTitleMatcher.DefaultInstance;

            Current = this;
        }

        internal static AniDbSeriesProvider Current { get; private set; }
        public IAniDbTitleMatcher TitleMatcher { get; set; }
        public int Order => -1;

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniDb);
            if (string.IsNullOrEmpty(aid))
                aid = await TitleMatcher.FindSeries(info.Name, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(aid))
            {
                result.Item = new Series();
                result.HasMetadata = true;

                result.Item.ProviderIds.Add(ProviderNames.AniDb, aid);

                var seriesDataPath = await GetSeriesData(_appPaths, _httpClient, aid, cancellationToken);
                FetchSeriesInfo(result, seriesDataPath, info.MetadataLanguage ?? "en");
            }

            return result;
        }

        public string Name => "AniDB";

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var metadata = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(false);

            var list = new List<RemoteSearchResult>();

            if (metadata.HasMetadata)
            {
                var res = new RemoteSearchResult
                {
                    Name = metadata.Item.Name,
                    PremiereDate = metadata.Item.PremiereDate,
                    ProductionYear = metadata.Item.ProductionYear,
                    ProviderIds = metadata.Item.ProviderIds,
                    SearchProviderName = Name
                };

                list.Add(res);
            }

            return list;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public static async Task<string> GetSeriesData(IApplicationPaths appPaths, IHttpClient httpClient, string seriesId, CancellationToken cancellationToken)
        {
            var dataPath = CalculateSeriesDataPath(appPaths, seriesId);
            var seriesDataPath = Path.Combine(dataPath, SeriesDataFile);
            var fileInfo = new FileInfo(seriesDataPath);

            // download series data if not present, or out of date
            if (!fileInfo.Exists || DateTime.UtcNow - fileInfo.LastWriteTimeUtc > TimeSpan.FromDays(7))
            {
                await DownloadSeriesData(seriesId, seriesDataPath, appPaths.CachePath, httpClient, cancellationToken).ConfigureAwait(false);
            }

            return seriesDataPath;
        }

        public static string CalculateSeriesDataPath(IApplicationPaths paths, string seriesId)
        {
            return Path.Combine(paths.CachePath, "anidb", "series", seriesId);
        }

        private void FetchSeriesInfo(MetadataResult<Series> result, string seriesDataPath, string preferredMetadataLangauge)
        {
            var series = result.Item;
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (var streamReader = File.Open(seriesDataPath, FileMode.Open, FileAccess.Read))
            using (var reader = XmlReader.Create(streamReader, settings))
            {
                reader.MoveToContent();

                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        switch (reader.Name)
                        {
                            case "startdate":
                                var val = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    DateTime date;
                                    if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date))
                                    {
                                        date = date.ToUniversalTime();
                                        series.PremiereDate = date;
                                    }
                                }

                                break;
                            case "enddate":
                                var endDate = reader.ReadElementContentAsString();

                                if (!string.IsNullOrWhiteSpace(endDate))
                                {
                                    DateTime date;
                                    if (DateTime.TryParse(endDate, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out date))
                                    {
                                        date = date.ToUniversalTime();
                                        series.EndDate = date;
                                    }
                                }

                                break;
                            case "titles":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    var title = ParseTitle(subtree, preferredMetadataLangauge);
                                    if (!string.IsNullOrEmpty(title))
                                    {
                                        series.Name = title;
                                    }
                                }

                                break;
                            case "creators":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseCreators(result, subtree);
                                }

                                break;
                            case "description":
                                series.Overview = ReplaceLineFeedWithNewLine(StripAniDbLinks(reader.ReadElementContentAsString()));

                                break;
                            case "ratings":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseRatings(series, subtree);
                                }

                                break;
                            case "resources":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseResources(series, subtree);
                                }

                                break;
                            case "characters":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseActors(result, subtree);
                                }

                                break;


                            case "tags":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseCategories(series, subtree);
                                }

                                break;

                            case "episodes":
                                using (var subtree = reader.ReadSubtree())
                                {
                                    ParseEpisodes(series, subtree);
                                }

                                break;
                        }
                    }
                }
            }

            GenreHelper.CleanupGenres(series);

        }

        private void ParseEpisodes(Series series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "episode")
                {
                    int id;
                    if (int.TryParse(reader.GetAttribute("id"), out id) && IgnoredCategoryIds.Contains(id))
                        continue;

                    using (var episodeSubtree = reader.ReadSubtree())
                    {
                        while (episodeSubtree.Read())
                        {
                            if (episodeSubtree.NodeType == XmlNodeType.Element)
                            {
                                switch (episodeSubtree.Name)
                                {
                                    case "epno":
                                        //var epno = episodeSubtree.ReadElementContentAsString();
                                        //EpisodeInfo info = new EpisodeInfo();
                                        //info.AnimeSeriesIndex = series.AnimeSeriesIndex;
                                        //info.IndexNumberEnd = string(epno);
                                        //info.SeriesProviderIds.GetOrDefault(ProviderNames.AniDb);
                                        //episodes.Add(info);
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ParseCategories(Series series, XmlReader reader)
        {
            var genres = new List<GenreInfo>();

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "tags")
                {

                    //int parentId;
                    //if (reader.GetAttribute("parentid") != null)
                    //    continue;


                    int id;
                    if (int.TryParse(reader.GetAttribute("id"), out id) && !IgnoredCategoryIds.Contains(id))
                        continue;

                    //int parentId;
                    //if (int.TryParse(reader.GetAttribute("parentid"), out parentId) && IgnoredCategoryIds.Contains(parentId))
                    //    continue;

                    int weight;
                    if (int.TryParse(reader.GetAttribute("weight"), out weight) || weight > 1)
                        continue;

                    using (var categorySubtree = reader.ReadSubtree())
                    {
                        while (categorySubtree.Read())
                        {
                            if (categorySubtree.NodeType == XmlNodeType.Element && !IgnoredCategoryIds.Contains(id) && categorySubtree.Name == "name")
                            {
                                var name = categorySubtree.ReadElementContentAsString();
                                genres.Add(new GenreInfo { Name = name, Weight = weight });
                            }
                        }
                    }
                }
            }

            series.Genres = genres.OrderBy(g => g.Weight).Select(g => g.Name).ToList();
        }

        private void ParseResources(Series series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "resource")
                {
                    var type = reader.GetAttribute("type");

                    switch (type)
                    {
                        case "2":
                            var ids = new List<int>();

                            using (var idSubtree = reader.ReadSubtree())
                            {
                                while (idSubtree.Read())
                                {
                                    if (idSubtree.NodeType == XmlNodeType.Element && idSubtree.Name == "identifier")
                                    {
                                        int id;
                                        if (int.TryParse(idSubtree.ReadElementContentAsString(), out id))
                                            ids.Add(id);
                                    }
                                }
                            }

                            if (ids.Count > 0)
                            {
                                var firstId = ids.OrderBy(i => i).First().ToString(CultureInfo.InvariantCulture);
                                series.ProviderIds.Add(ProviderNames.MyAnimeList, firstId);
//                                series.ProviderIds.Add(ProviderNames.AniList, firstId);
                            }

                            break;
                        case "4":
                            while (reader.Read())
                            {
                                if (reader.NodeType == XmlNodeType.Element && reader.Name == "url")
                                {
                                    series.HomePageUrl = reader.ReadElementContentAsString();
                                    break;
                                }
                            }

                            break;
                    }
                }
            }
        }

        private string StripAniDbLinks(string text)
        {
            return AniDbUrlRegex.Replace(text, "${name}");
        }

        public static string ReplaceLineFeedWithNewLine(string text)
        {
            return text.Replace("\n", Environment.NewLine);
        }

        private void ParseActors(MetadataResult<Series> series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "character")
                    {
                        using (var subtree = reader.ReadSubtree())
                        {
                            ParseActor(series, subtree);
                        }
                    }
                }
            }
        }

        private void ParseActor(MetadataResult<Series> series, XmlReader reader)
        {
            string name = null;
            string role = null;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "name":
                            role = reader.ReadElementContentAsString();
                            break;
                        case "seiyuu":
                            name = reader.ReadElementContentAsString();
                            break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(role)) // && series.People.All(p => p.Name != name))
            {
                series.AddPerson(CreatePerson(name, PersonType.Actor, role));
            }
        }

        private void ParseRatings(Series series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Name == "temporary")
                    {
                        float rating;
                        if (float.TryParse(
                            reader.ReadElementContentAsString(),
                            NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture,
                            out rating))
                        {
                            series.CommunityRating = (float)Math.Round(rating, 1);
                        }
                    }
                }
            }
        }

        private string ParseTitle(XmlReader reader, string preferredMetadataLangauge)
        {
            var titles = new List<Title>();

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "title")
                {
                    var language = reader.GetAttribute("xml:lang");
                    var type = reader.GetAttribute("type");
                    var name = reader.ReadElementContentAsString();

                    titles.Add(new Title
                    {
                        Language = language,
                        Type = type,
                        Name = name
                    });
                }
            }

            return titles.Localize(Plugin.Instance.Configuration.TitlePreference, preferredMetadataLangauge).Name;
        }

        private void ParseCreators(MetadataResult<Series> series, XmlReader reader)
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "name")
                {
                    var type = reader.GetAttribute("type");
                    var name = reader.ReadElementContentAsString();

                    if (type == "Animation Work")
                    {
                        series.Item.AddStudio(name);
                    }
                    else
                    {
                        series.AddPerson(CreatePerson(name, type));
                    }
                }
            }
        }

        private PersonInfo CreatePerson(string name, string type, string role = null)
        {
            // todo find nationality of person and conditionally reverse name order

            string mappedType;
            if (!_typeMappings.TryGetValue(type, out mappedType))
            {
                mappedType = type;
            }

            return new PersonInfo
            {
                Name = ReverseNameOrder(name),
                Type = mappedType,
                Role = role
            };
        }

        public static string ReverseNameOrder(string name)
        {
            return name.Split(' ').Reverse().Aggregate(string.Empty, (n, part) => n + " " + part).Trim();
        }

        private static async Task DownloadSeriesData(string aid, string seriesDataPath, string cachePath, IHttpClient httpClient, CancellationToken cancellationToken)
        {
            var directory = Path.GetDirectoryName(seriesDataPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            DeleteXmlFiles(directory);

            var requestOptions = new HttpRequestOptions
            {
                Url = string.Format(SeriesQueryUrl, ClientName, aid),
                CancellationToken = cancellationToken,
                EnableHttpCompression = false
            };

            await RequestLimiter.Tick();

            using (var stream = await httpClient.Get(requestOptions).ConfigureAwait(false))
            using (var unzipped = new GZipStream(stream, CompressionMode.Decompress))
            using (var reader = new StreamReader(unzipped, Encoding.UTF8, true))
            using (var file = File.Open(seriesDataPath, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(file))
            {
                var text = await reader.ReadToEndAsync().ConfigureAwait(false);
                text = text.Replace("&#x0;", "");

                await writer.WriteAsync(text).ConfigureAwait(false);
            }

            await ExtractEpisodes(directory, seriesDataPath);
            ExtractCast(cachePath, seriesDataPath);
        }

        private static void DeleteXmlFiles(string path)
        {
            try
            {
                foreach (var file in new DirectoryInfo(path)
                    .EnumerateFiles("*.xml", SearchOption.AllDirectories)
                    .ToList())
                {
                    file.Delete();
                }
            }
            catch (DirectoryNotFoundException)
            {
                // No biggie
            }
        }

        private static async Task ExtractEpisodes(string seriesDataDirectory, string seriesDataPath)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (var streamReader = new StreamReader(seriesDataPath, Encoding.UTF8))
            {
                // Use XmlReader for best performance
                using (var reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();

                    // Loop through each element
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name == "episode")
                            {
                                var outerXml = reader.ReadOuterXml();
                                await SaveEpsiodeXml(seriesDataDirectory, outerXml).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        private static void ExtractCast(string cachePath, string seriesDataPath)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            var cast = new List<AniDbPersonInfo>();

            using (var streamReader = new StreamReader(seriesDataPath, Encoding.UTF8))
            {
                // Use XmlReader for best performance
                using (var reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();

                    // Loop through each element
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "characters")
                        {
                            var outerXml = reader.ReadOuterXml();
                            cast.AddRange(ParseCharacterList(outerXml));
                        }

                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "creators")
                        {
                            var outerXml = reader.ReadOuterXml();
                            cast.AddRange(ParseCreatorsList(outerXml));
                        }
                    }
                }
            }

            var serializer = new XmlSerializer(typeof(AniDbPersonInfo));
            foreach (var person in cast)
            {
                var path = GetCastPath(person.Name, cachePath);
                var directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);

                if (!File.Exists(path) || person.Image != null)
                {
                    try
                    {
                        using (var stream = File.Open(path, FileMode.Create))
                            serializer.Serialize(stream, person);
                    }
                    catch (IOException)
                    {
                        // ignore
                    }
                }
            }
        }

        public static AniDbPersonInfo GetPersonInfo(string cachePath, string name)
        {
            var path = GetCastPath(name, cachePath);
            var serializer = new XmlSerializer(typeof(AniDbPersonInfo));

            try
            {
                if (File.Exists(path))
                {
                    using (var stream = File.OpenRead(path))
                        return serializer.Deserialize(stream) as AniDbPersonInfo;
                }
            }
            catch (IOException)
            {
                return null;
            }

            return null;
        }

        private static string GetCastPath(string name, string cachePath)
        {
            name = name.ToLowerInvariant();
            return Path.Combine(cachePath, "anidb-people", name[0].ToString(), name + ".xml");
        }

        private static IEnumerable<AniDbPersonInfo> ParseCharacterList(string xml)
        {
            var doc = XDocument.Parse(xml);
            var people = new List<AniDbPersonInfo>();

            var characters = doc.Element("characters");
            if (characters != null)
            {
                foreach (var character in characters.Descendants("character"))
                {
                    var seiyuu = character.Element("seiyuu");
                    if (seiyuu != null)
                    {
                        var person = new AniDbPersonInfo
                        {
                            Name = ReverseNameOrder(seiyuu.Value)
                        };

                        var picture = seiyuu.Attribute("picture");
                        if (picture != null && !string.IsNullOrEmpty(picture.Value))
                        {
                            person.Image = "http://img7.anidb.net/pics/anime/" + picture.Value;
                        }

                        var id = seiyuu.Attribute("id");
                        if (id != null && !string.IsNullOrEmpty(id.Value))
                        {
                            person.Id = id.Value;
                        }

                        people.Add(person);
                    }
                }
            }

            return people;
        }

        private static IEnumerable<AniDbPersonInfo> ParseCreatorsList(string xml)
        {
            var doc = XDocument.Parse(xml);
            var people = new List<AniDbPersonInfo>();

            var creators = doc.Element("creators");
            if (creators != null)
            {
                foreach (var creator in creators.Descendants("name"))
                {
                    var type = creator.Attribute("type");
                    if (type != null && type.Value == "Animation Work")
                    {
                        continue;
                    }

                    var person = new AniDbPersonInfo
                    {
                        Name = ReverseNameOrder(creator.Value)
                    };

                    var id = creator.Attribute("id");
                    if (id != null && !string.IsNullOrEmpty(id.Value))
                    {
                        person.Id = id.Value;
                    }

                    people.Add(person);
                }
            }

            return people;
        }

        private static async Task SaveXml(string xml, string filename)
        {
            var writerSettings = new XmlWriterSettings
            {
                Encoding = Encoding.UTF8,
                Async = true
            };

            using (var writer = XmlWriter.Create(filename, writerSettings))
            {
                await writer.WriteRawAsync(xml).ConfigureAwait(false);
            }
        }

        private static async Task SaveEpsiodeXml(string seriesDataDirectory, string xml)
        {
            var episodeNumber = ParseEpisodeNumber(xml);

            if (episodeNumber != null)
            {
                var file = Path.Combine(seriesDataDirectory, string.Format("episode-{0}.xml", episodeNumber));
                await SaveXml(xml, file);
            }
        }

        private static string ParseEpisodeNumber(string xml)
        {
            var settings = new XmlReaderSettings
            {
                CheckCharacters = false,
                IgnoreProcessingInstructions = true,
                IgnoreComments = true,
                ValidationType = ValidationType.None
            };

            using (var streamReader = new StringReader(xml))
            {
                // Use XmlReader for best performance
                using (var reader = XmlReader.Create(streamReader, settings))
                {
                    reader.MoveToContent();

                    // Loop through each element
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                        {
                            if (reader.Name == "epno")
                            {
                                var val = reader.ReadElementContentAsString();
                                if (!string.IsNullOrWhiteSpace(val))
                                {
                                    return val;
                                }
                            }
                            else
                            {
                                reader.Skip();
                            }
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        ///     Gets the series data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <param name="seriesId">The series id.</param>
        /// <returns>System.String.</returns>
        internal static string GetSeriesDataPath(IApplicationPaths appPaths, string seriesId)
        {
            var seriesDataPath = Path.Combine(GetSeriesDataPath(appPaths), seriesId);

            return seriesDataPath;
        }

        /// <summary>
        ///     Gets the series data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <returns>System.String.</returns>
        internal static string GetSeriesDataPath(IApplicationPaths appPaths)
        {
            var dataPath = Path.Combine(appPaths.CachePath, "anidb\\series");

            return dataPath;
        }

        private struct GenreInfo
        {
            public string Name;
            public int Weight;
        }
    }

    public class Title
    {
        public string Language { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
    }

    public static class TitleExtensions
    {
        public static Title Localize(this IEnumerable<Title> titles, TitlePreferenceType preference, string metadataLanguage)
        {
            var titlesList = titles as IList<Title> ?? titles.ToList();

            if (preference == TitlePreferenceType.Localized)
            {
                // prefer an official title, else look for a synonym
                var localized = titlesList.FirstOrDefault(t => t.Language == metadataLanguage && t.Type == "main") ??
                                titlesList.FirstOrDefault(t => t.Language == metadataLanguage && t.Type == "official") ??
                                titlesList.FirstOrDefault(t => t.Language == metadataLanguage && t.Type == "synonym");

                if (localized != null)
                {
                    return localized;
                }
            }

            if (preference == TitlePreferenceType.Japanese)
            {
                // prefer an official title, else look for a synonym
                var japanese = titlesList.FirstOrDefault(t => t.Language == "ja" && t.Type == "main") ??
                               titlesList.FirstOrDefault(t => t.Language == "ja" && t.Type == "official") ??
                               titlesList.FirstOrDefault(t => t.Language == "ja" && t.Type == "synonym");

                if (japanese != null)
                {
                    return japanese;
                }
            }

            // return the main title (romaji)
            return titlesList.FirstOrDefault(t => t.Language == "x-jat" && t.Type == "main") ??
                   titlesList.FirstOrDefault(t => t.Type == "main") ??
                   titlesList.FirstOrDefault();
        }

        /// <summary>
        ///     Gets the series data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <param name="seriesId">The series id.</param>
        /// <returns>System.String.</returns>
        internal static string GetSeriesDataPath(IApplicationPaths appPaths, string seriesId)
        {
            var seriesDataPath = Path.Combine(GetSeriesDataPath(appPaths), seriesId);

            return seriesDataPath;
        }

        /// <summary>
        ///     Gets the series data path.
        /// </summary>
        /// <param name="appPaths">The app paths.</param>
        /// <returns>System.String.</returns>
        internal static string GetSeriesDataPath(IApplicationPaths appPaths)
        {
            var dataPath = Path.Combine(appPaths.CachePath, "tvdb");

            return dataPath;
        }
    }
}
