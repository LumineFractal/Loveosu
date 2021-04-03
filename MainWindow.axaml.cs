using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using IniParser;
using IniParser.Model;
using Loveosu.APIv2;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Loveosu
{
    public class MainWindow : Window
    {
        string? UserID;
        string? ClientID;
        string? ClientSecret;
        int UpdateInterval;
        User? StartUser;
        DispatcherTimer? Timer;
        List<Stat> Stats = new List<Stat>();
        List<Line> Lines = new List<Line>();

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            OnStart();
        }

        private async void OnStart()
        {
            CultureInfo culture = new CultureInfo("en-US");

            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            if (ReadConfig())
            {
                StartUser = await RunAPIRequest(UserID, ClientID, ClientSecret);
                SetControlsVisibility();
                UpdateValues(StartUser);
                this.FindControl<TabItem>("TabUser").Header = StartUser.Username;
                InitTimer();
            }
        }

        private void SetControlsVisibility()
        {
            Canvas canvas = this.FindControl<Canvas>("UserCanvas");
            foreach (Line line in Lines)
            {
                canvas.Children.Remove(line);
            }

            this.FindControl<TextBlock>("AlertNoData").IsVisible = false;
            Stats.Sort((p, q) => p.Order.CompareTo(q.Order));
            int disabledControls = 0;
            foreach (Stat stat in Stats)
            {
                Canvas.SetTop(stat.MainCustomControl, 30 * stat.Order + 10);
                if (stat.Enabled)
                {
                    this.FindControl<RadioButton>(stat.Name + "CustomYes").IsChecked = true;
                    foreach (Control control in stat.UserControls)
                    {
                        Canvas.SetTop(control, 40 * (stat.Order - disabledControls) - 20);
                        control.IsVisible = true;
                    }
                    Line line = new Line();
                    line.Stroke = (SolidColorBrush)(new BrushConverter().ConvertFrom("#787878"));
                    line.StartPoint = new Point(10, 10 + 40 * (stat.Order - disabledControls));
                    line.EndPoint = new Point(580, 10 + 40 * (stat.Order - disabledControls));
                    line.StrokeThickness = 2;
                    Lines.Add(line);
                    canvas.Children.Add(line);
                }
                else
                {
                    disabledControls++;
                    this.FindControl<RadioButton>(stat.Name + "CustomNo").IsChecked = true;
                    foreach (Control control in stat.UserControls)
                    {
                        Canvas.SetTop(control, 40 * (stat.Order - disabledControls) - 20);
                        control.IsVisible = false;
                    }
                }
                foreach (Control control in stat.CustomControls)
                {
                    Canvas.SetTop(control, 30 * stat.Order + 5);
                }
            }
            canvas.Children.Remove(Lines.Last());
        }

        private void UpdateValues(User user)
        {
            this.FindControl<TextBlock>("RankedScore").Text = string.Format("{0:n}", user?.Statistics?.RankedScore).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("TotalScore").Text = string.Format("{0:n}", user?.Statistics?.TotalScore).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("Level").Text = string.Format("{0:n}", (user?.Statistics?.Level?.Current + ((float?)user?.Statistics?.Level?.Progress / 100))).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("pp").Text = string.Format("{0:n}", user?.Statistics?.Performance).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("GlobalRank").Text = string.Format("{0:n}", user?.Statistics?.GlobalRank).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("CountryRank").Text = string.Format("{0:n}", user?.Statistics?.CountryRank).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("HitAccuracy").Text = string.Format("{0:n}", user?.Statistics?.HitAccuracy).TrimEnd('0').TrimEnd('.') + "%";
            this.FindControl<TextBlock>("PlayCount").Text = string.Format("{0:n}", user?.Statistics?.PlayCount).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("CurrentMonthPlaycount").Text = string.Format("{0:n}", user?.MonthlyPlaycounts?.Last().Count).TrimEnd('0').TrimEnd('.');
            TimeSpan totalTime = TimeSpan.FromSeconds((double)(user?.Statistics?.PlayTime));
            this.FindControl<TextBlock>("PlayTime").Text = ((int)totalTime.TotalHours).ToString() + "h " + totalTime.Minutes.ToString() + "min";
            this.FindControl<TextBlock>("TotalHits").Text = string.Format("{0:n}", user?.Statistics?.TotalHits).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("ReplaysWatched").Text = string.Format("{0:n}", user?.Statistics?.ReplaysWatched).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("FirstPlacesCount").Text = string.Format("{0:n}", user?.FirstPlacesCount).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("SSHCount").Text = string.Format("{0:n}", user?.Statistics?.GradeCounts?.SSH).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("SSCount").Text = string.Format("{0:n}", user?.Statistics?.GradeCounts?.SS).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("SHCount").Text = string.Format("{0:n}", user?.Statistics?.GradeCounts?.SH).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("SCount").Text = string.Format("{0:n}", user?.Statistics?.GradeCounts?.S).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("ACount").Text = string.Format("{0:n}", user?.Statistics?.GradeCounts?.A).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("SSCombinedCount").Text = string.Format("{0:n}", user?.Statistics?.GradeCounts?.SSH + user?.Statistics?.GradeCounts?.SS).TrimEnd('0').TrimEnd('.');
            this.FindControl<TextBlock>("SCombinedCount").Text = string.Format("{0:n}", user?.Statistics?.GradeCounts?.SH + user?.Statistics?.GradeCounts?.S).TrimEnd('0').TrimEnd('.');
        }

        public void InitTimer()
        {
            if (Timer is not null)
            {
                Timer.Stop();
            }
            Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(UpdateInterval)
            };
            Timer.Tick += async (sender, e) =>
            {
                await TimerTickAsync();
            };

            Timer.Start();
        }

        private async Task TimerTickAsync()
        {
            User? user = await RunAPIRequest(UserID, ClientID, ClientSecret);
            UpdateValues(user);
            UpdateDiffValues(user);
        }

        public async void OnSaveButtonClick(object sender, RoutedEventArgs args)
        {
            this.FindControl<TextBlock>("ErrorTextBlock").IsVisible = false;
            this.FindControl<TextBlock>("SavedTextBlock").IsVisible = false;
            this.FindControl<TextBlock>("WaitTextBlock").IsVisible = true;
            UserID = this.FindControl<TextBox>("UserID").Text;
            ClientID = this.FindControl<TextBox>("ClientID").Text;
            ClientSecret = this.FindControl<TextBox>("ClientSecret").Text;
            User? user = await RunAPIRequest(UserID, ClientID, ClientSecret);
            if (user != null)
            {
                StartUser = user;
                SaveConfig();
                this.FindControl<TabItem>("TabUser").Header = StartUser.Username;
                this.FindControl<TextBlock>("SavedTextBlock").IsVisible = true;
                this.FindControl<TabItem>("TabCustomization").IsVisible = true;
                UpdateValues(StartUser);
                UpdateDiffValues(StartUser);
                InitTimer();
            }
            else
            {
                this.FindControl<TextBlock>("ErrorTextBlock").IsVisible = true;
            }

            this.FindControl<TextBlock>("WaitTextBlock").IsVisible = false;
        }

        private static async Task<User?> RunAPIRequest(string UserID, string ClientID, string ClientSecret)
        {
            using var client = new HttpClient();
            client.BaseAddress = new Uri("https://osu.ppy.sh/");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var parameters = new Dictionary<string, string>
            {
                { "client_id", ClientID },
                { "client_secret", ClientSecret },
                { "grant_type", "client_credentials" },
                { "scope", "public" }
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await client.PostAsync("https://osu.ppy.sh/oauth/token", content).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {

                var jsonString = response.Content.ReadAsStringAsync();
                AccessToken accessToken = JsonConvert.DeserializeObject<AccessToken>(jsonString.Result);

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

                string apiAddress = "api/v2/users/" + UserID + "/osu";

                response = await client.GetAsync(apiAddress).ConfigureAwait(false);

                var jsonStringUser = response.Content.ReadAsStringAsync();
                User? user = null;
                user = JsonConvert.DeserializeObject<User>(jsonStringUser.Result);

                return user;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Error during getting token");
                return null;
            }
        }

        private bool ReadConfig()
        {
            string path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\config.ini";

            var parser = new FileIniDataParser();
            IniData data;
            try
            {
                data = parser.ReadFile(path);
            }
            catch
            {
                this.FindControl<TabItem>("Settings").IsSelected = true;
                this.FindControl<TabItem>("TabCustomization").IsVisible = false;
                return false;
            }
            string configured = data["FirstUse"]["configured"];
            if (bool.Parse(configured))
            {
                UserID = data["General"]["userId"];
                this.FindControl<TextBox>("UserID").Text = UserID;
                ClientID = data["General"]["clientId"];
                this.FindControl<TextBox>("ClientID").Text = ClientID;
                ClientSecret = data["General"]["clientSecret"];
                this.FindControl<TextBox>("ClientSecret").Text = ClientSecret;
                UpdateInterval = int.Parse(data["General"]["updateInterval"]);
                if (UpdateInterval < 5 || UpdateInterval > 40000)
                {
                    UpdateInterval = 20;
                }
                this.FindControl<TextBox>("UpdateInterval").Text = UpdateInterval.ToString();
                this.FindControl<TextBlock>("AlertNoData").IsVisible = false;
                Stats.Add(new Stat("RankedScore", bool.Parse(data["RankedScore"]["Enabled"]), int.Parse(data["RankedScore"]["Order"]), this));
                Stats.Add(new Stat("TotalScore", bool.Parse(data["TotalScore"]["Enabled"]), int.Parse(data["TotalScore"]["Order"]), this));
                Stats.Add(new Stat("Level", bool.Parse(data["Level"]["Enabled"]), int.Parse(data["Level"]["Order"]), this));
                Stats.Add(new Stat("pp", bool.Parse(data["pp"]["Enabled"]), int.Parse(data["pp"]["Order"]), this));
                Stats.Add(new Stat("GlobalRank", bool.Parse(data["GlobalRank"]["Enabled"]), int.Parse(data["GlobalRank"]["Order"]), this));
                Stats.Add(new Stat("CountryRank", bool.Parse(data["CountryRank"]["Enabled"]), int.Parse(data["CountryRank"]["Order"]), this));
                Stats.Add(new Stat("HitAccuracy", bool.Parse(data["HitAccuracy"]["Enabled"]), int.Parse(data["HitAccuracy"]["Order"]), this));
                Stats.Add(new Stat("PlayCount", bool.Parse(data["PlayCount"]["Enabled"]), int.Parse(data["PlayCount"]["Order"]), this));
                Stats.Add(new Stat("CurrentMonthPlaycount", bool.Parse(data["CurrentMonthPlaycount"]["Enabled"]), int.Parse(data["CurrentMonthPlaycount"]["Order"]), this));
                Stats.Add(new Stat("PlayTime", bool.Parse(data["PlayTime"]["Enabled"]), int.Parse(data["PlayTime"]["Order"]), this));
                Stats.Add(new Stat("TotalHits", bool.Parse(data["TotalHits"]["Enabled"]), int.Parse(data["TotalHits"]["Order"]), this));
                Stats.Add(new Stat("ReplaysWatched", bool.Parse(data["ReplaysWatched"]["Enabled"]), int.Parse(data["ReplaysWatched"]["Order"]), this));
                Stats.Add(new Stat("FirstPlacesCount", bool.Parse(data["FirstPlacesCount"]["Enabled"]), int.Parse(data["FirstPlacesCount"]["Order"]), this));
                Stats.Add(new Stat("SSHCount", bool.Parse(data["SSHCount"]["Enabled"]), int.Parse(data["SSHCount"]["Order"]), this));
                Stats.Add(new Stat("SSCount", bool.Parse(data["SSCount"]["Enabled"]), int.Parse(data["SSCount"]["Order"]), this));
                Stats.Add(new Stat("SHCount", bool.Parse(data["SHCount"]["Enabled"]), int.Parse(data["SHCount"]["Order"]), this));
                Stats.Add(new Stat("SCount", bool.Parse(data["SCount"]["Enabled"]), int.Parse(data["SCount"]["Order"]), this));
                Stats.Add(new Stat("ACount", bool.Parse(data["ACount"]["Enabled"]), int.Parse(data["ACount"]["Order"]), this));
                Stats.Add(new Stat("SSCombinedCount", bool.Parse(data["SSCombinedCount"]["Enabled"]), int.Parse(data["SSCombinedCount"]["Order"]), this));
                Stats.Add(new Stat("SCombinedCount", bool.Parse(data["SCombinedCount"]["Enabled"]), int.Parse(data["SCombinedCount"]["Order"]), this));
                return true;
            }
            else
            {
                this.FindControl<TextBlock>("AlertNoData").IsVisible = true;
                Stats.Add(new Stat("RankedScore", bool.Parse(data["RankedScore"]["Enabled"]), int.Parse(data["RankedScore"]["Order"]), this));
                Stats.Add(new Stat("TotalScore", bool.Parse(data["TotalScore"]["Enabled"]), int.Parse(data["TotalScore"]["Order"]), this));
                Stats.Add(new Stat("Level", bool.Parse(data["Level"]["Enabled"]), int.Parse(data["Level"]["Order"]), this));
                Stats.Add(new Stat("pp", bool.Parse(data["pp"]["Enabled"]), int.Parse(data["pp"]["Order"]), this));
                Stats.Add(new Stat("GlobalRank", bool.Parse(data["GlobalRank"]["Enabled"]), int.Parse(data["GlobalRank"]["Order"]), this));
                Stats.Add(new Stat("CountryRank", bool.Parse(data["CountryRank"]["Enabled"]), int.Parse(data["CountryRank"]["Order"]), this));
                Stats.Add(new Stat("HitAccuracy", bool.Parse(data["HitAccuracy"]["Enabled"]), int.Parse(data["HitAccuracy"]["Order"]), this));
                Stats.Add(new Stat("PlayCount", bool.Parse(data["PlayCount"]["Enabled"]), int.Parse(data["PlayCount"]["Order"]), this));
                Stats.Add(new Stat("CurrentMonthPlaycount", bool.Parse(data["CurrentMonthPlaycount"]["Enabled"]), int.Parse(data["CurrentMonthPlaycount"]["Order"]), this));
                Stats.Add(new Stat("PlayTime", bool.Parse(data["PlayTime"]["Enabled"]), int.Parse(data["PlayTime"]["Order"]), this));
                Stats.Add(new Stat("TotalHits", bool.Parse(data["TotalHits"]["Enabled"]), int.Parse(data["TotalHits"]["Order"]), this));
                Stats.Add(new Stat("ReplaysWatched", bool.Parse(data["ReplaysWatched"]["Enabled"]), int.Parse(data["ReplaysWatched"]["Order"]), this));
                Stats.Add(new Stat("FirstPlacesCount", bool.Parse(data["FirstPlacesCount"]["Enabled"]), int.Parse(data["FirstPlacesCount"]["Order"]), this));
                Stats.Add(new Stat("SSHCount", bool.Parse(data["SSHCount"]["Enabled"]), int.Parse(data["SSHCount"]["Order"]), this));
                Stats.Add(new Stat("SSCount", bool.Parse(data["SSCount"]["Enabled"]), int.Parse(data["SSCount"]["Order"]), this));
                Stats.Add(new Stat("SHCount", bool.Parse(data["SHCount"]["Enabled"]), int.Parse(data["SHCount"]["Order"]), this));
                Stats.Add(new Stat("SCount", bool.Parse(data["SCount"]["Enabled"]), int.Parse(data["SCount"]["Order"]), this));
                Stats.Add(new Stat("ACount", bool.Parse(data["ACount"]["Enabled"]), int.Parse(data["ACount"]["Order"]), this));
                Stats.Add(new Stat("SSCombinedCount", bool.Parse(data["SSCombinedCount"]["Enabled"]), int.Parse(data["SSCombinedCount"]["Order"]), this));
                Stats.Add(new Stat("SCombinedCount", bool.Parse(data["SCombinedCount"]["Enabled"]), int.Parse(data["SCombinedCount"]["Order"]), this));
                return false;
            }
        }

        private void SaveConfig()
        {
            UpdateInterval = int.Parse(this.FindControl<TextBox>("UpdateInterval").Text);

            FileIniDataParser parser = new FileIniDataParser();
            IniData config = new IniData();

            config.Sections.AddSection("FirstUse");
            config["FirstUse"].AddKey("configured", "true");
            config.Sections.AddSection("General");
            config["General"].AddKey("userId", UserID);
            config["General"].AddKey("clientId", ClientID);
            config["General"].AddKey("clientSecret", ClientSecret);
            config["General"].AddKey("updateInterval", UpdateInterval.ToString());
            if (Stats.Count == 0)
            {
                InitializeOrder();
            }
            foreach (Stat stat in Stats)
            {
                config.Sections.AddSection(stat.Name);
                config[stat.Name].AddKey("Order", stat.Order.ToString());
                config[stat.Name].AddKey("Enabled", stat.Enabled.ToString());
            }
            
            parser.WriteFile("config.ini", config);

            SetControlsVisibility();
        }

        private void UpdateDiffValues(User? user)
        {
            if ((user?.Statistics?.RankedScore - StartUser?.Statistics?.RankedScore) > 0)
            {
                this.FindControl<TextBlock>("RankedScoreDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.RankedScore - StartUser?.Statistics?.RankedScore)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                this.FindControl<TextBlock>("RankedScoreDiff").Text = "";
            }
            if ((user?.Statistics?.TotalScore - StartUser?.Statistics?.TotalScore) > 0)
            {
                this.FindControl<TextBlock>("TotalScoreDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.TotalScore - StartUser?.Statistics?.TotalScore)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                this.FindControl<TextBlock>("TotalScoreDiff").Text = "";
            }
            if (((user?.Statistics?.Level?.Current + ((float?)user?.Statistics?.Level?.Progress / 100))
                    - (StartUser?.Statistics?.Level?.Current + ((float?)StartUser?.Statistics?.Level?.Progress / 100))) > 0)
            {
                this.FindControl<TextBlock>("LevelDiff").Text = "+" + string.Format("{0:n}", (((user?.Statistics?.Level?.Current + ((float?)user?.Statistics?.Level?.Progress / 100))
                    - (StartUser?.Statistics?.Level?.Current + ((float?)StartUser?.Statistics?.Level?.Progress / 100))))).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                this.FindControl<TextBlock>("LevelDiff").Text = "";
            }
            if ((user?.Statistics?.Performance - StartUser?.Statistics?.Performance) > 0)
            {
                this.FindControl<TextBlock>("ppDiff").Foreground = Brushes.LightGreen;
                this.FindControl<TextBlock>("ppDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.Performance - StartUser?.Statistics?.Performance)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                if ((user?.Statistics?.Performance - StartUser?.Statistics?.Performance) == 0)
                {
                    this.FindControl<TextBlock>("ppDiff").Text = "";
                }
                else
                {
                    this.FindControl<TextBlock>("ppDiff").Foreground = Brushes.LightCoral;
                    this.FindControl<TextBlock>("ppDiff").Text = string.Format("{0:n}", (user?.Statistics?.Performance - StartUser?.Statistics?.Performance)).TrimEnd('0').TrimEnd('.');
                }
            }
            if ((user?.Statistics?.GlobalRank - StartUser?.Statistics?.GlobalRank) > 0)
            {
                this.FindControl<TextBlock>("GlobalRankDiff").Foreground = Brushes.LightCoral;
                this.FindControl<TextBlock>("GlobalRankDiff").Text = "-" + string.Format("{0:n}", (user?.Statistics?.GlobalRank - StartUser?.Statistics?.GlobalRank)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                if ((user?.Statistics?.GlobalRank - StartUser?.Statistics?.GlobalRank) == 0)
                {
                    this.FindControl<TextBlock>("GlobalRankDiff").Text = "";
                }
                else
                {
                    this.FindControl<TextBlock>("GlobalRankDiff").Foreground = Brushes.LightGreen;
                    this.FindControl<TextBlock>("GlobalRankDiff").Text = "+" + string.Format("{0:n}", (StartUser?.Statistics?.GlobalRank - user?.Statistics?.GlobalRank)).TrimEnd('0').TrimEnd('.');
                }
            }
            if ((user?.Statistics?.CountryRank - StartUser?.Statistics?.CountryRank) > 0)
            {
                this.FindControl<TextBlock>("CountryRankDiff").Foreground = Brushes.LightCoral;
                this.FindControl<TextBlock>("CountryRankDiff").Text = "-" + string.Format("{0:n}", (user?.Statistics?.CountryRank - StartUser?.Statistics?.CountryRank)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                if ((user?.Statistics?.CountryRank - StartUser?.Statistics?.CountryRank) == 0)
                {
                    this.FindControl<TextBlock>("CountryRankDiff").Text = "";
                }
                else
                {
                    this.FindControl<TextBlock>("CountryRankDiff").Foreground = Brushes.LightGreen;
                    this.FindControl<TextBlock>("CountryRankDiff").Text = "+" + string.Format("{0:n}", (StartUser?.Statistics?.CountryRank - user?.Statistics?.CountryRank)).TrimEnd('0').TrimEnd('.');
                }
            }
            if ((user?.Statistics?.HitAccuracy - StartUser?.Statistics?.HitAccuracy) > 0)
            {
                this.FindControl<TextBlock>("HitAccuracyDiff").Foreground = Brushes.LightGreen;
                this.FindControl<TextBlock>("HitAccuracyDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.HitAccuracy - StartUser?.Statistics?.HitAccuracy)).TrimEnd('0').TrimEnd('.') + "%";
            }
            else
            {
                if ((user?.Statistics?.HitAccuracy - StartUser?.Statistics?.HitAccuracy) < 0)
                {
                    this.FindControl<TextBlock>("HitAccuracyDiff").Foreground = Brushes.LightCoral;
                    this.FindControl<TextBlock>("HitAccuracyDiff").Text = string.Format("{0:n}", (user?.Statistics?.HitAccuracy - StartUser?.Statistics?.HitAccuracy)).TrimEnd('0').TrimEnd('.') + "%";
                }
                else
                {
                    this.FindControl<TextBlock>("HitAccuracyDiff").Text = "";
                }
            }
            if ((user?.Statistics?.PlayCount - StartUser?.Statistics?.PlayCount) > 0)
            {
                this.FindControl<TextBlock>("PlayCountDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.PlayCount - StartUser?.Statistics?.PlayCount)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                this.FindControl<TextBlock>("PlayCountDiff").Text = "";
            }
            if ((user?.MonthlyPlaycounts?.Last().Count - StartUser.MonthlyPlaycounts?.Last().Count) > 0)
            {
                this.FindControl<TextBlock>("CurrentMonthPlaycountDiff").Text = "+" + string.Format("{0:n}", (user?.MonthlyPlaycounts?.Last().Count - StartUser?.MonthlyPlaycounts?.Last().Count)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                this.FindControl<TextBlock>("CurrentMonthPlaycountDiff").Text = "";
            }
            if ((user?.Statistics?.PlayTime - StartUser?.Statistics?.PlayTime) > 60)
            {
                TimeSpan timeDiff = TimeSpan.FromSeconds((double)(user?.Statistics?.PlayTime - StartUser?.Statistics?.PlayTime));
                if((int)timeDiff.TotalHours >= 1) 
                {
                    this.FindControl<TextBlock>("PlayTimeDiff").Text = "+" + (int)timeDiff.TotalHours + "h " + timeDiff.Minutes + "min";
                }
                else
                {
                    this.FindControl<TextBlock>("PlayTimeDiff").Text = "+" + timeDiff.Minutes + "min";
                }
            }
            else
            {
                this.FindControl<TextBlock>("PlayTimeDiff").Text = "";
            }
            if ((user?.Statistics?.TotalHits - StartUser?.Statistics?.TotalHits) > 0)
            {
                this.FindControl<TextBlock>("TotalHitsDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.TotalHits - StartUser?.Statistics?.TotalHits)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                this.FindControl<TextBlock>("TotalHitsDiff").Text = "";
            }
            if ((user?.Statistics?.ReplaysWatched - StartUser?.Statistics?.ReplaysWatched) > 0)
            {
                this.FindControl<TextBlock>("ReplaysWatchedDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.ReplaysWatched - StartUser?.Statistics?.ReplaysWatched)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                this.FindControl<TextBlock>("ReplaysWatchedDiff").Text = "";
            }
            if ((user?.Statistics?.GradeCounts?.SSH + user?.Statistics?.GradeCounts?.SS) 
                - (StartUser?.Statistics?.GradeCounts?.SSH + StartUser?.Statistics?.GradeCounts?.SS) > 0)
            {
                this.FindControl<TextBlock>("SSCombinedCountDiff").Foreground = Brushes.LightGreen;
                this.FindControl<TextBlock>("SSCombinedCountDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.GradeCounts?.SSH + user?.Statistics?.GradeCounts?.SS)
                - (StartUser?.Statistics?.GradeCounts?.SSH + StartUser?.Statistics?.GradeCounts?.SS)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                if((user?.Statistics?.GradeCounts?.SSH + user?.Statistics?.GradeCounts?.SS)
                    - (StartUser?.Statistics?.GradeCounts?.SSH + StartUser?.Statistics?.GradeCounts?.SS) == 0)
                {
                    this.FindControl<TextBlock>("SSCombinedCountDiff").Text = "";
                }
                else
                {
                    this.FindControl<TextBlock>("SSCombinedCountDiff").Foreground = Brushes.LightCoral;
                    this.FindControl<TextBlock>("SSCombinedCountDiff").Text = string.Format("{0:n}", (user?.Statistics?.GradeCounts?.SSH + user?.Statistics?.GradeCounts?.SS)
                    - (StartUser?.Statistics?.GradeCounts?.SSH + StartUser?.Statistics?.GradeCounts?.SS)).TrimEnd('0').TrimEnd('.');
                }
            }
            if ((user?.Statistics?.GradeCounts?.SH + user?.Statistics?.GradeCounts?.S)
                - (StartUser?.Statistics?.GradeCounts?.SH + StartUser?.Statistics?.GradeCounts?.S) > 0)
            {
                this.FindControl<TextBlock>("SCombinedCountDiff").Foreground = Brushes.LightGreen;
                this.FindControl<TextBlock>("SCombinedCountDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.GradeCounts?.SH + user?.Statistics?.GradeCounts?.S)
                - (StartUser?.Statistics?.GradeCounts?.SH + StartUser?.Statistics?.GradeCounts?.S)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                if ((user?.Statistics?.GradeCounts?.SH + user?.Statistics?.GradeCounts?.S)
                    - (StartUser?.Statistics?.GradeCounts?.SH + StartUser?.Statistics?.GradeCounts?.S) == 0)
                {
                    this.FindControl<TextBlock>("SCombinedCountDiff").Text = "";
                }
                else
                {
                    this.FindControl<TextBlock>("SCombinedCountDiff").Foreground = Brushes.LightCoral;
                    this.FindControl<TextBlock>("SCombinedCountDiff").Text = string.Format("{0:n}", (user?.Statistics?.GradeCounts?.SH + user?.Statistics?.GradeCounts?.S)
                    - (StartUser?.Statistics?.GradeCounts?.SH + StartUser?.Statistics?.GradeCounts?.S)).TrimEnd('0').TrimEnd('.');
                }
            }
            if ((user?.Statistics?.GradeCounts?.SSH - StartUser?.Statistics?.GradeCounts?.SSH) > 0)
            {
                this.FindControl<TextBlock>("SSHCountDiff").Foreground = Brushes.LightGreen;
                this.FindControl<TextBlock>("SSHCountDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.GradeCounts?.SSH - StartUser?.Statistics?.GradeCounts?.SSH)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                if ((user?.Statistics?.GradeCounts?.SSH - StartUser?.Statistics?.GradeCounts?.SSH) == 0)
                {
                    this.FindControl<TextBlock>("SSHCountDiff").Text = "";
                }
                else
                {
                    this.FindControl<TextBlock>("SSHCountDiff").Foreground = Brushes.LightCoral;
                    this.FindControl<TextBlock>("SSHCountDiff").Text = string.Format("{0:n}", (user?.Statistics?.GradeCounts?.SSH - StartUser?.Statistics?.GradeCounts?.SSH)).TrimEnd('0').TrimEnd('.');
                }
            }
            if ((user?.Statistics?.GradeCounts?.SS - StartUser?.Statistics?.GradeCounts?.SS) > 0)
            {
                this.FindControl<TextBlock>("SSCountDiff").Foreground = Brushes.LightGreen;
                this.FindControl<TextBlock>("SSCountDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.GradeCounts?.SS - StartUser?.Statistics?.GradeCounts?.SS)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                if ((user?.Statistics?.GradeCounts?.SS - StartUser?.Statistics?.GradeCounts?.SS) == 0)
                {
                    this.FindControl<TextBlock>("SSCountDiff").Text = "";
                }
                else
                {
                    this.FindControl<TextBlock>("SSCountDiff").Foreground = Brushes.LightCoral;
                    this.FindControl<TextBlock>("SSCountDiff").Text = string.Format("{0:n}", (user?.Statistics?.GradeCounts?.SS - StartUser?.Statistics?.GradeCounts?.SS)).TrimEnd('0').TrimEnd('.');
                }
            }
            if ((user?.Statistics?.GradeCounts?.SH - StartUser?.Statistics?.GradeCounts?.SH) > 0)
            {
                this.FindControl<TextBlock>("SHCountDiff").Foreground = Brushes.LightGreen;
                this.FindControl<TextBlock>("SHCountDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.GradeCounts?.SH - StartUser?.Statistics?.GradeCounts?.SH)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                if ((user?.Statistics?.GradeCounts?.SH - StartUser?.Statistics?.GradeCounts?.SH) == 0)
                {
                    this.FindControl<TextBlock>("SHCountDiff").Text = "";
                }
                else
                {
                    this.FindControl<TextBlock>("SHCountDiff").Foreground = Brushes.LightCoral;
                    this.FindControl<TextBlock>("SHCountDiff").Text = string.Format("{0:n}", (user?.Statistics?.GradeCounts?.SH - StartUser?.Statistics?.GradeCounts?.SH)).TrimEnd('0').TrimEnd('.');
                }
            }
            if ((user?.Statistics?.GradeCounts?.S - StartUser?.Statistics?.GradeCounts?.S) > 0)
            {
                this.FindControl<TextBlock>("SCountDiff").Foreground = Brushes.LightGreen;
                this.FindControl<TextBlock>("SCountDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.GradeCounts?.S - StartUser?.Statistics?.GradeCounts?.S)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                if ((user?.Statistics?.GradeCounts?.S - StartUser?.Statistics?.GradeCounts?.S) == 0)
                {
                    this.FindControl<TextBlock>("SCountDiff").Text = "";
                }
                else
                {
                    this.FindControl<TextBlock>("SCountDiff").Foreground = Brushes.LightCoral;
                    this.FindControl<TextBlock>("SCountDiff").Text = string.Format("{0:n}", (user?.Statistics?.GradeCounts?.S - StartUser?.Statistics?.GradeCounts?.S)).TrimEnd('0').TrimEnd('.');
                }
            }
            
            if ((user?.Statistics?.GradeCounts?.A - StartUser?.Statistics?.GradeCounts?.A) > 0)
            {
                this.FindControl<TextBlock>("ACountDiff").Foreground = Brushes.LightGreen;
                this.FindControl<TextBlock>("ACountDiff").Text = "+" + string.Format("{0:n}", (user?.Statistics?.GradeCounts?.A - StartUser?.Statistics?.GradeCounts?.A)).TrimEnd('0').TrimEnd('.');
            }
            else
            {
                if ((user?.Statistics?.GradeCounts?.A - StartUser?.Statistics?.GradeCounts?.A) == 0)
                {
                    this.FindControl<TextBlock>("ACountDiff").Text = "";
                }
                else
                {
                    this.FindControl<TextBlock>("ACountDiff").Foreground = Brushes.LightCoral;
                    this.FindControl<TextBlock>("ACountDiff").Text = string.Format("{0:n}", (user?.Statistics?.GradeCounts?.A - StartUser?.Statistics?.GradeCounts?.A)).TrimEnd('0').TrimEnd('.');
                }
            }
        }

        private void InitializeOrder()
        {
            Stats.Add(new Stat("RankedScore", true, 1, this));
            Stats.Add(new Stat("TotalScore", true, 2, this));
            Stats.Add(new Stat("Level", true, 3, this));
            Stats.Add(new Stat("pp", true, 4, this));
            Stats.Add(new Stat("GlobalRank", true, 5, this));
            Stats.Add(new Stat("CountryRank", true, 6, this));
            Stats.Add(new Stat("HitAccuracy", true, 7, this));
            Stats.Add(new Stat("PlayCount", true, 8, this));
            Stats.Add(new Stat("CurrentMonthPlaycount", false, 9, this));
            Stats.Add(new Stat("PlayTime", true, 10, this));
            Stats.Add(new Stat("TotalHits", true, 11, this));
            Stats.Add(new Stat("ReplaysWatched", true, 12, this));
            Stats.Add(new Stat("FirstPlacesCount", false, 13, this));
            Stats.Add(new Stat("SSHCount", true, 14, this));
            Stats.Add(new Stat("SSCount", true, 15, this));
            Stats.Add(new Stat("SHCount", true, 16, this));
            Stats.Add(new Stat("SCount", true, 17, this));
            Stats.Add(new Stat("ACount", true, 18, this));
            Stats.Add(new Stat("SSCombinedCount", false, 19, this));
            Stats.Add(new Stat("SCombinedCount", false, 20, this));
        }

        #region order buttons
        public void OnRankedScoreCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("RankedScore");
        }

        public void OnRankedScoreCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("RankedScore");
        }

        public void OnTotalScoreCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("TotalScore");
        }

        public void OnTotalScoreCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("TotalScore");
        }

        public void OnLevelCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("Level");
        }

        public void OnLevelCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("Level");
        }

        public void OnppCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("pp");
        }

        public void OnppCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("pp");
        }

        public void OnGlobalRankCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("GlobalRank");
        }

        public void OnGlobalRankCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("GlobalRank");
        }

        public void OnCountryRankCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("CountryRank");
        }

        public void OnCountryRankCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("CountryRank");
        }

        public void OnHitAccuracyCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("HitAccuracy");
        }

        public void OnHitAccuracyCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("HitAccuracy");
        }

        public void OnPlayCountCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("PlayCount");
        }

        public void OnPlayCountCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("PlayCount");
        }

        public void OnCurrentMonthPlaycountCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("CurrentMonthPlaycount");
        }

        public void OnCurrentMonthPlaycountCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("CurrentMonthPlaycount");
        }

        public void OnPlayTimeCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("PlayTime");
        }

        public void OnPlayTimeCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("PlayTime");
        }

        public void OnTotalHitsCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("TotalHits");
        }

        public void OnTotalHitsCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("TotalHits");
        }

        public void OnReplaysWatchedCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("ReplaysWatched");
        }

        public void OnReplaysWatchedCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("ReplaysWatched");
        }

        public void OnFirstPlacesCountCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("FirstPlacesCount");
        }

        public void OnFirstPlacesCountCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("FirstPlacesCount");
        }

        public void OnSSHCountCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("SSHCount");
        }

        public void OnSSHCountCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("SSHCount");
        }

        public void OnSSCountCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("SSCount");
        }

        public void OnSSCountCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("SSCount");
        }

        public void OnSHCountCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("SHCount");
        }

        public void OnSHCountCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("SHCount");
        }

        public void OnSCountCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("SCount");
        }

        public void OnSCountCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("SCount");
        }

        public void OnACountCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("ACount");
        }

        public void OnACountCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("ACount");
        }

        public void OnSSCombinedCountCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("SSCombinedCount");
        }

        public void OnSSCombinedCountCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("SSCombinedCount");
        }

        public void OnSCombinedCountCustomUpClick(object sender, RoutedEventArgs args)
        {
            UpClick("SCombinedCount");
        }

        public void OnSCombinedCountCustomDownClick(object sender, RoutedEventArgs args)
        {
            DownClick("SCombinedCount");
        }

        private void UpClick(string name)
        {
            int height = (int)this.FindControl<Control>(name + "CustomUp").GetValue(Canvas.TopProperty);
            if (height != 35)
            {
                foreach (Stat stat in Stats)
                {
                    Control mainControl = stat.MainCustomControl;
                    if (mainControl.GetValue(Canvas.TopProperty) == height - 25)
                    {
                        stat.Order++;
                        Canvas.SetTop(mainControl, (int)mainControl.GetValue(Canvas.TopProperty) + 30);
                        foreach (Control control in stat.CustomControls)
                        {
                            Canvas.SetTop(control, (int)control.GetValue(Canvas.TopProperty) + 30);
                        }
                        break;
                    }
                }

                foreach (Stat stat in Stats)
                {
                    if (stat.Name == name)
                    {
                        stat.Order--;
                        Canvas.SetTop(stat.MainCustomControl, height - 25);
                        foreach (Control control in stat.CustomControls)
                        {
                            Canvas.SetTop(control, height - 30);
                        }
                        break;
                    }
                }
            }
        }

        private void DownClick(string name)
        {
            int height = (int)this.FindControl<Control>(name + "CustomDown").GetValue(Canvas.TopProperty);
            if (height != 605)
            {
                foreach (Stat stat in Stats)
                {
                    Control mainControl = stat.MainCustomControl;
                    if (mainControl.GetValue(Canvas.TopProperty) == height + 35)
                    {
                        stat.Order--;
                        Canvas.SetTop(mainControl, (int)mainControl.GetValue(Canvas.TopProperty) - 30);
                        foreach (Control control in stat.CustomControls)
                        {
                            Canvas.SetTop(control, (int)control.GetValue(Canvas.TopProperty) - 30);
                        }
                        break;
                    }
                }

                foreach (Stat stat in Stats)
                {
                    if (stat.Name == name)
                    {
                        stat.Order++;
                        Canvas.SetTop(stat.MainCustomControl, height + 35);
                        foreach (Control control in stat.CustomControls)
                        {
                            Canvas.SetTop(control, height + 30);
                        }
                        break;
                    }
                }
            }
        }
        #endregion

        private void OnSaveCustomClick(object sender, RoutedEventArgs args)
        {
            foreach (Stat stat in Stats)
            {
                if ((bool)this.FindControl<RadioButton>(stat.Name + "CustomYes").IsChecked)
                {
                    stat.Enabled = true;
                }
                else
                {
                    stat.Enabled = false;
                }
            }

            SaveConfig();
            SetControlsVisibility();
        }
    }
}
