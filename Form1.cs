using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using NAudio.Wave;
using System.Collections.Concurrent;
using System.Threading;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Tab;
using System.Linq;


namespace MyLoginApp
{
    public partial class Form1 : Form
    {
        private readonly string _codePath;
        private readonly string _admin;
        private readonly string _functionPath;
        private readonly string _notificationPath;
        private readonly string _musicPath = "";
        private WaveOutEvent? _musicOut;
        private string _musicFilePath = "";       // file mp3 đã chọn

        private AudioFileReader? _musicReader;

        private string MusicCfgPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "YourApp", "music_path.txt");
        private readonly string _settimePath;
        private const bool UseDpapiForPassword = true;

        private string AdminConfigPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "YourApp", "admin_config.json");
        public class AdminConfigRow
        {
            public string company { get; set; } = "";
            public string url { get; set; } = "";
            public string username { get; set; } = "";
            public string password { get; set; } = ""; 
        }
        private bool _isRunning = true;
        private bool _playN8N = false;
        private Dictionary<string, string> _routes;

        // ===== Notification / Voice Settings =====
        private readonly string _settingsPath;
        private string _notifLanguage = "vi";
        private string _currentVoice = "alloy";
        private int _currentRate = 50;
        private string inputText = "";

        private static int ReadIntFromJson(JsonElement root, string prop, int fallback, int min = 0, int max = 100)
        {
            int val = fallback;

            if (root.TryGetProperty(prop, out var el))
            {
                if (el.ValueKind == JsonValueKind.Number)
                {
                    if (!el.TryGetInt32(out val))
                        val = (int)Math.Round(el.GetDouble());
                }
                else if (el.ValueKind == JsonValueKind.String)
                {
                    if (int.TryParse(el.GetString(), out var tmp))
                        val = tmp;
                }
            }

            return Math.Clamp(val, min, max);
        }
        private static double RateToSpeed(int rate)
        {
            // rate 0..100 -> speed 0.75..1.35 (dải dễ nghe, đủ thấy khác biệt)
            rate = Math.Clamp(rate, 0, 100);
            return 0.75 + (rate / 100.0) * (1.35 - 0.75);
        }
        // ===== OpenAI =====
        private readonly HttpClient _http = new HttpClient();
        private string _openAiKey = "";
        private int _pollAfterMs = 1200; // hết tin thì ngủ 1.2s (tùy bạn: 3000/5000)
        // ===== Audio player (NAudio) =====
        private IWavePlayer? _waveOut;
        private AudioFileReader? _audioReader;
        // ===== N8N / Queue TTS =====
        private const string DEFAULT_N8N_WEBHOOK_URL =
    "";

        private string _n8nWebhookUrl = DEFAULT_N8N_WEBHOOK_URL;


        private readonly ConcurrentQueue<string> _ttsQueue = new();
        private readonly SemaphoreSlim _ttsSignal = new(0);
        private CancellationTokenSource? _ttsCts;
        private Task? _ttsWorker;

        private CancellationTokenSource? _pollCts;
        private Task? _pollWorker;
        private string _lastN8nText = "";
        private DateTime _lastN8nAt = DateTime.MinValue;
        private CancellationTokenSource? _musicCts;

        ///settime
        public class ScheduleItem
        {
            public string id { get; set; } = "";
            public string time { get; set; } = "";   // "HH:MM"
            public string type { get; set; } = "";   // "co-dinh" | "san-luong" (tuỳ JS bạn dùng)
            public string note { get; set; } = "";
            public bool enabled { get; set; } = true;
        }

        private List<ScheduleItem> _schedules = new();
        // ===== Settime Realtime Timer =====
        private System.Windows.Forms.Timer? _scheduleTimer;

        // chống bắn lặp trong cùng 1 phút: yyyyMMdd|id|HH:mm
        private readonly HashSet<string> _fired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private string SchedulesPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "YourApp", "schedules.json");

        private readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };
        private string _adminCompany = "";
        private string _adminUrl = "";
        private string _adminUsername = "";
        private string _adminPassword = "";

        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
            _admin = Path.Combine(Application.StartupPath, "UI", "admin.html");
            _codePath = Path.Combine(Application.StartupPath, "UI", "code.html");
            _functionPath = Path.Combine(Application.StartupPath, "UI", "function.html");
            _notificationPath = Path.Combine(Application.StartupPath, "UI", "notification.html");
            _musicPath = Path.Combine(Application.StartupPath, "UI", "music.html");
            _settimePath = Path.Combine(Application.StartupPath, "UI", "settime.html");

            _routes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["code"] = _codePath,
                ["function"] = _functionPath,
                ["notification"] = _notificationPath,
                ["music"] = _musicPath,
                ["admin"] = _admin,
                ["settime"] = _settimePath

            };

            _settingsPath = Path.Combine(Application.StartupPath, "settings.json");
            TryLoadAdminConfigToVars();

        }

        private void webViewLogin_Click(object sender, EventArgs e)
        {

        }

        private async void Form1_Load(object? sender, EventArgs e)
        {
            await InitWebView2Async();
            if (webViewLogin.CoreWebView2 == null) return;

            // Load settings.json (language/voice) ngay khi mở app
            LoadSettingsFromFile();
            LoadSchedulesFromFile();
            StartScheduleTimer();
            webViewLogin.CoreWebView2.NavigationCompleted -= Core_NavigationCompleted;
            webViewLogin.CoreWebView2.NavigationCompleted += Core_NavigationCompleted;
            LoadMusicPathFromFile();

            Nav("code");
            LoadOpenAiKey();
            StartPolling();

        }
        /// Khởi tạo WebView2: EnsureCoreWebView2Async, bật WebMessage, gắn WebMessageReceived.
        private async Task InitWebView2Async()
        {
            await webViewLogin.EnsureCoreWebView2Async();
            if (webViewLogin.CoreWebView2 == null)
            {
                MessageBox.Show("WebView2 chưa khởi tạo được CoreWebView2.");
                return;
            }

            webViewLogin.CoreWebView2.Settings.IsWebMessageEnabled = true;

            webViewLogin.CoreWebView2.WebMessageReceived -= WebMessageReceived;
            webViewLogin.CoreWebView2.WebMessageReceived += WebMessageReceived;
        }

        /// Nhận message từ JavaScript (window.chrome.webview.postMessage):
        /// - log: ghi log từ JS
        /// - login: xử lý đăng nhập
        /// - nav: điều hướng sang trang khác theo target
        /// - set_running: cập nhật trạng thái chạy/dừng và bắn về function.html
        private string CurrentUri() => webViewLogin.Source?.ToString() ?? "";

        private bool IsNotificationHtml()
            => CurrentUri().Contains("notification.html", StringComparison.OrdinalIgnoreCase);

        private bool IsFunctionHtml()
            => CurrentUri().Contains("function.html", StringComparison.OrdinalIgnoreCase);
        private bool MusicHtml()
            => CurrentUri().Contains("music.html", StringComparison.OrdinalIgnoreCase);
        private bool SETTIMEHtml()
            => CurrentUri().Contains("settime.html", StringComparison.OrdinalIgnoreCase);
        private bool IsAdminHtml()
    => CurrentUri().Contains("admin.html", StringComparison.OrdinalIgnoreCase);

        private void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var json = e.WebMessageAsJson;
            Debug.WriteLine("WebMessageReceived: " + json);

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var action = root.TryGetProperty("action", out var a) ? a.GetString() : null;
                if (string.IsNullOrWhiteSpace(action)) return;

                // ✅ Ưu tiên handler cho notification.html
                if (IsNotificationHtml())
                {
                    HandleNotificationActions(action!, root);
                    return;
                }
                if(MusicHtml())
                {
                    HandleMusicActions(action!,root);
                    return;
                }
                if (SETTIMEHtml())
                {
                    HandleSettimeActions(action!, root);
                    return;
                }
                if (IsAdminHtml())
                {
                    HandleAdminActions(action!, root);
                    return;
                }

                switch (action)
                {
                    case "log":
                        Debug.WriteLine("JS LOG: " + (root.TryGetProperty("msg", out var m) ? m.GetString() : ""));
                        break;

                    case "login":
                        HandleLogin(root);
                        break;
                    case "set_running":
                        {
                            if (root.TryGetProperty("running", out var r) &&
                                (r.ValueKind == JsonValueKind.True || r.ValueKind == JsonValueKind.False))
                            {
                                _isRunning = r.GetBoolean();
                                Debug.WriteLine($"[C#] set_running = {_isRunning}");
                            }
                            else
                            {
                                Debug.WriteLine("[C#] set_running ignored (missing/invalid 'running')");
                            }
                            break;
                        }

                    case "get_running":
                        {
                            Debug.WriteLine("[C#] get_running -> send state");
                            BeginInvoke(new Action(SendRunningToFunctionPage));
                            break;
                        }

                    case "start":
                        {
                            StartPolling();
                            break;
                        }
                    case "stop":
                        {
                            StopPolling();
                            break;
                        }

                    default:
                        Debug.WriteLine("Unknown action: " + action);
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WebMessageReceived ERROR: " + ex);
            }
        }
        /// Xử lý đăng nhập:
        /// - kiểm tra user/pass
        /// - trả kết quả về JS ({ok:true/false})3
        /// - nếu OK thì chuyển sang function.html
        private void HandleLogin(JsonElement root)
        {
            string user = root.TryGetProperty("user", out var u) ? (u.GetString() ?? "").Trim() : "";
            string pass = root.TryGetProperty("pass", out var p) ? (p.GetString() ?? "") : "";

            
            var cfg = LoadAdminConfigFile()
                .FirstOrDefault(x =>
                    string.Equals((x.username ?? "").Trim(), user, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.password ?? "", pass, StringComparison.Ordinal));

         
            bool okDefaultAdmin = string.Equals(user, "admin", StringComparison.OrdinalIgnoreCase)
                                  && string.Equals(pass, "123456", StringComparison.Ordinal);

            bool ok = (cfg != null) || okDefaultAdmin;

            if (webViewLogin.CoreWebView2 == null) return;

            if (!ok)
            {
                webViewLogin.CoreWebView2.PostWebMessageAsJson(
                    "{\"ok\":false,\"error\":\"Sai tài khoản hoặc mật khẩu\"}");
                return;
            }

            
            bool isAdmin =
                okDefaultAdmin ||
                (cfg != null && string.Equals((cfg.username ?? "").Trim(), "admin", StringComparison.OrdinalIgnoreCase));

         
            if (cfg != null)
            {
                _adminCompany = (cfg.company ?? "").Trim();
                _adminUrl = (cfg.url ?? "").Trim();
                _adminUsername = (cfg.username ?? "").Trim();
                _adminPassword = (cfg.password ?? "");

                if (Uri.TryCreate(_adminUrl, UriKind.Absolute, out _))
                    _n8nWebhookUrl = _adminUrl;
                else
                    _n8nWebhookUrl = DEFAULT_N8N_WEBHOOK_URL;
            }
            else
            {
                // login admin mặc định
                _adminCompany = "DEFAULT";
                _adminUrl = DEFAULT_N8N_WEBHOOK_URL;
                _adminUsername = "admin";
                _adminPassword = "123456";
                _n8nWebhookUrl = DEFAULT_N8N_WEBHOOK_URL;
            }

            webViewLogin.CoreWebView2.PostWebMessageAsJson("{\"ok\":true}");

            BeginInvoke(new Action(() =>
            {
                if (isAdmin) Nav("admin");
                else Nav("function");
            }));
        }




        /// Điều hướng theo key route ("code", "function", "notification", "music", "settime").
        /// Nếu không có key thì mặc định về function.html.
        private void Nav(string target)
        {
            if (!_routes.TryGetValue(target, out var path))
                path = _functionPath;

            NavigateLocalFile(path);
        }
       
        private void NavigateLocalFile(string path)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show("Không thấy file: " + path);
                return;
            }

            var uri = "file:///" + path.Replace("\\", "/");
            Debug.WriteLine("NAV -> " + uri);
            webViewLogin.Source = new Uri(uri);
        }
        /// Event NavigationCompleted:
        /// - sau khi load xong trang
        /// - nếu đang ở function.html thì bắn trạng thái running/dừng sang JS để UI khóa/mở đúng.
        private void Core_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                if (webViewLogin.CoreWebView2 == null) return;

                var uri = CurrentUri();

                if (uri.Contains("function.html", StringComparison.OrdinalIgnoreCase))
                {
                    SendRunningToFunctionPage();
                }
                if (uri.Contains("notification.html", StringComparison.OrdinalIgnoreCase))
                {
                    // ✅ vào notification thì bắn settings xuống để set dropdown
                    SendNotificationSettingsToPage();
                }
                if (uri.Contains("settime.html", StringComparison.OrdinalIgnoreCase))
                {
                    // vào settime thì gửi schedules xuống để JS render
                    SendSchedulesToSettimePage();
                }
                if (uri.Contains("admin.html", StringComparison.OrdinalIgnoreCase))
                {
                    SendAdminConfigToAdminPage();
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine("NavigationCompleted ERROR: " + ex);
            }
        }



      
        private void SendRunningToFunctionPage()
        {
            if (webViewLogin.CoreWebView2 == null) return;

            void send()
            {
                try
                {
                    var payload = JsonSerializer.Serialize(new { action = "set_running", running = _isRunning });
                    webViewLogin.CoreWebView2.PostWebMessageAsJson(payload);
                    Debug.WriteLine("[C#] -> JS set_running sent: " + payload);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SendRunningToFunctionPage ERROR: " + ex);
                }
            }

            if (InvokeRequired) BeginInvoke((Action)send);
            else send();
        }
        private void LoadSettingsFromFile()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return;

                var json = File.ReadAllText(_settingsPath, Encoding.UTF8);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("language", out var l) && l.ValueKind == JsonValueKind.String)
                    _notifLanguage = l.GetString() ?? "vi";

                if (root.TryGetProperty("voice", out var v) && v.ValueKind == JsonValueKind.String)
                    _currentVoice = v.GetString() ?? "alloy";
                if (root.TryGetProperty("text", out var tt) && tt.ValueKind == JsonValueKind.String)
                    inputText = tt.GetString() ?? "";
                if (root.TryGetProperty("rate", out var rr))
                {
                    if (rr.ValueKind == JsonValueKind.Number)
                    {
                        if (!rr.TryGetInt32(out _currentRate))
                            _currentRate = (int)Math.Round(rr.GetDouble());
                    }
                    else if (rr.ValueKind == JsonValueKind.String && int.TryParse(rr.GetString(), out var tmp))
                    {
                        _currentRate = tmp;
                    }
                    _currentRate = Math.Clamp(_currentRate, 0, 100);
                }
                Debug.WriteLine($"[C#] Loaded settings: lang={_notifLanguage}, voice={_currentVoice}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadSettingsFromFile ERROR: " + ex);
            }
        }

        private void SaveSettingsToFile()
        {
            try
            {
                var payload = JsonSerializer.Serialize(new
                {
                    language = _notifLanguage,
                    voice = _currentVoice,
                    text = inputText,
                    rate = _currentRate
                }, new JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(_settingsPath, payload, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveSettingsToFile ERROR: " + ex);
            }
        }

        private void HandleNotificationActions(string action, JsonElement root)
        {
            switch (action)
            {
                // JS backHome đang gửi: post("nav", { target:"function" })
                // => vẫn sẽ vào case "nav" bên ngoài nếu bạn không return ở trên.
                // Nhưng vì mình đã "return" khi IsNotificationHtml(),
                // nên cần xử lý nav tại đây luôn:
                case "nav":
                    {
                        Nav("function");
                        break;
                    }

                case "get_notification_settings":
                    {
                        SendNotificationSettingsToPage();
                        break;
                    }

                case "save_notification_settings":
                    {
                        if (root.TryGetProperty("language", out var l) && l.ValueKind == JsonValueKind.String)
                            _notifLanguage = l.GetString() ?? _notifLanguage;

                        if (root.TryGetProperty("voice", out var v) && v.ValueKind == JsonValueKind.String)
                            _currentVoice = v.GetString() ?? _currentVoice;

                        if (root.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                            inputText = (t.GetString() ?? "").Trim();

                      
                        _currentRate = ReadIntFromJson(root, "rate", _currentRate, 0, 100);

                        SaveSettingsToFile();
                        SendNotificationSettingsToPage();

                        Debug.WriteLine($"[C#] Saved settings: lang={_notifLanguage}, voice={_currentVoice}, rate={_currentRate}, text={inputText}");
                        break;
                    }
                case "set_voice":
                    {
                        var voice = root.GetProperty("voice").GetString() ?? "alloy";
                        _currentVoice = voice;
                        SendNotificationSettingsToPage();

                        break;
                    }

                case "test_voice":
                    {
                        var voice = root.TryGetProperty("voice", out var v) ? (v.GetString() ?? _currentVoice) : _currentVoice;
                        var text = root.TryGetProperty("text", out var t) ? (t.GetString() ?? "") : "";

                        int rate = ReadIntFromJson(root, "rate", _currentRate, 0, 100);

                        inputText = text;
                        _currentVoice = voice;
                        _currentRate = rate;

                        // test xem có nhận không
                        // MessageBox.Show($"voice={voice}\nrate={rate}\ntext={text}", "TEST");

                        _ = SpeakWithOpenAiAsync(inputText);
                        break;
                    }
                case "log":
                    {
                        Debug.WriteLine("JS LOG: " + (root.TryGetProperty("msg", out var m) ? m.GetString() : ""));
                        break;
                    }
                case "mic":
                    {
                        int rate = ReadIntFromJson(root, "rate", _currentRate, 0, 100);
                        _currentRate = rate;

                        MessageBox.Show(rate.ToString(), "MIC");
                        break;
                    }


                default:
                    Debug.WriteLine("[C#] Notification unknown action: " + action);
                    break;
            }
        }
        private void LoadMusicPathFromFile()
        {
            try
            {
                if (!File.Exists(MusicCfgPath)) return;

                var p = File.ReadAllText(MusicCfgPath, Encoding.UTF8).Trim();
                if (!string.IsNullOrWhiteSpace(p) && File.Exists(p))
                    _musicFilePath = p;

                Debug.WriteLine("[C#] Loaded music mp3: " + _musicFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadMusicPathFromFile ERROR: " + ex);
            }
        }


        private void SendNotificationSettingsToPage()
        {
            if (webViewLogin.CoreWebView2 == null) return;

            var payload = JsonSerializer.Serialize(new
            {
                action = "set_notification_settings",
                language = _notifLanguage,
                voice = _currentVoice,
                text = inputText,
                rate = _currentRate   
            });

            webViewLogin.CoreWebView2.PostWebMessageAsJson(payload);
            Debug.WriteLine("[C#] -> JS set_notification_settings sent: " + payload);
        }
        
        private async Task SpeakWithOpenAiAsync(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return;

            if (string.IsNullOrWhiteSpace(_openAiKey))
            {
                MessageBox.Show("Thiếu OpenAI API key. Hãy nạp _openAiKey trước.");
                return;
            }

            try
            {
                string mp3Path = await OpenAiTtsToMp3Async(text, _currentVoice, _currentRate);
                PlayMusic(mp3Path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SpeakWithOpenAiAsync] ERROR: " + ex);
                MessageBox.Show(ex.Message, "TTS lỗi");
            }
        }

        private async Task<string> OpenAiTtsToMp3Async(string text, string voice, int rate)
        {
            const string url = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxx";///enter api kpi of your

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string tempDir = Path.Combine(baseDir, "temp");
            Directory.CreateDirectory(tempDir);

            string outMp3 = Path.Combine(tempDir, $"openai_tts_{DateTime.Now:yyyyMMdd_HHmmss_fff}.mp3");

            double speed = RateToSpeed(rate);

            var payloadObj = new
            {
                model = "gpt-4o-mini-tts",
                voice = voice,
                input = text,

                
                response_format = "mp3",

                
                speed = speed,

                
                instructions = $"Nói tiếng Việt rõ ràng, tốc độ khoảng {speed:0.00}x."
            };

            string json = JsonSerializer.Serialize(payloadObj);

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _openAiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req);
            byte[] data = await resp.Content.ReadAsByteArrayAsync();

            if (!resp.IsSuccessStatusCode)
            {
                string err = Encoding.UTF8.GetString(data);
                throw new Exception("OpenAI TTS lỗi:\n" + err);
            }

            File.WriteAllBytes(outMp3, data);

            Debug.WriteLine($"[TTS] rate={rate} => speed={speed:0.00} voice={voice}");
            return outMp3;
        }
        private void PlayAudio(string path)
        {
            StopAudio();

            _audioReader = new AudioFileReader(path);
            _waveOut = new WaveOutEvent();
            _waveOut.Init(_audioReader);
            _waveOut.Play();
        }

        private void StopAudio()
        {
            try { _waveOut?.Stop(); } catch { }

            try { _audioReader?.Dispose(); _audioReader = null; } catch { }
            try { _waveOut?.Dispose(); _waveOut = null; } catch { }
        }
        private void LoadOpenAiKey()
        {
            try
            {
                var keyPath = Path.Combine(Application.StartupPath, "openai_key.txt");
                if (!File.Exists(keyPath))
                {
                    Debug.WriteLine("[C#] Không thấy: " + keyPath);
                    return;
                }

                _openAiKey = File.ReadAllText(keyPath, Encoding.UTF8).Trim();
                Debug.WriteLine("[C#] openai key loaded, len=" + _openAiKey.Length);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadOpenAiKey ERROR: " + ex);
            }
        }
        
        private void HandleMusicActions(string action, JsonElement root)
        {
            
            switch (action)
            {
                case "nav":
                    {
                        Nav("function");
                        break;
                    }

                case "music_upload":
                    {
                        ChangeMusic();
                        break;
                    }
                case "music_resume":
                    {
                        ResumeMusic();
                        break;
                    }

                case "music_play":
                    {
                        PlayMusic(_musicFilePath);
                        break;
                    }
                case "music_pause":
                    {
                        PauseMusic();
                        break;
                    }
                case "music_save":
                    {
                        StopMusic();
                        break;
                    }
                default:
                    Debug.WriteLine("[C#] Notification unknown action: " + action);
                    break;
            }
        }
        public void ChangeMusic(bool playImmediately = true)
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "MP3 files (*.mp3)|*.mp3";
            ofd.Multiselect = false;

            if (ofd.ShowDialog() != DialogResult.OK) return;

            _musicFilePath = ofd.FileName;

            var dir = Path.GetDirectoryName(MusicCfgPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(MusicCfgPath, _musicFilePath, Encoding.UTF8);

            if (playImmediately) PlayMusic(_musicFilePath);
        }

        private void PlayMusic(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    Debug.WriteLine("[PlayMusic] file not found: " + filePath);
                    return;
                }

                StopMusic();

                _musicReader = new AudioFileReader(filePath);
                _musicOut = new WaveOutEvent();
                _musicOut.Init(_musicReader);
                _musicOut.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PlayMusic] ERROR: " + ex);
                StopMusic();
            }
        }


        private void StopMusic()
        {
            try { _musicOut?.Stop(); } catch { /* ignore */ }

            _musicOut?.Dispose();
            _musicOut = null;

            _musicReader?.Dispose();
            _musicReader = null;
        }
        public void PauseMusic()
        {
            if (_musicOut == null) return;
            if (_musicOut.PlaybackState == PlaybackState.Playing)
                _musicOut.Pause();
        }

        public void ResumeMusic()
        {
            if (_musicOut == null) return;
            if (_musicOut.PlaybackState == PlaybackState.Paused)
                _musicOut.Play();
        }
        
        private void HandleSettimeActions(string action, JsonElement root)
        {
            switch (action)
            {
                case "nav":
                    {
                        _isRunning = true;
                        Nav("function");
                        break;
                    }

                case "get_schedules":
                    {
                        SendSchedulesToSettimePage();
                        break;
                    }

                case "sync_schedules":
                    {
                        if (root.TryGetProperty("schedules", out var arr) && arr.ValueKind == JsonValueKind.Array)
                        {
                            try
                            {
                                var list = JsonSerializer.Deserialize<List<ScheduleItem>>(arr.GetRawText(), _jsonOpts);
                                _schedules = list ?? new List<ScheduleItem>();

                                SaveSchedulesToFile();
                                SendSchedulesToSettimePage();

                                Debug.WriteLine($"[C#] sync_schedules ok: {_schedules.Count}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine("[C#] sync_schedules parse ERROR: " + ex);
                            }
                        }
                        else
                        {
                            Debug.WriteLine("[C#] sync_schedules missing schedules[]");
                        }
                        break;
                    }

                
                case "toggle_schedule":
                    {
                        var id = root.TryGetProperty("id", out var i) ? (i.GetString() ?? "") : "";

                        var enabled =
                            root.TryGetProperty("enabled", out var en) &&
                            (en.ValueKind == JsonValueKind.True || en.ValueKind == JsonValueKind.False)
                            ? en.GetBoolean()
                            : true;

                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            SetScheduleEnabled(id, enabled);
                            SaveSchedulesToFile();
                            SendSchedulesToSettimePage();

                            Debug.WriteLine($"[C#] toggle_schedule: {id} => {enabled}");
                        }
                        break;
                    }

                
                case "delete_schedule":
                    {
                        var id = root.TryGetProperty("id", out var i) ? (i.GetString() ?? "") : "";
                        if (string.IsNullOrWhiteSpace(id)) break;

                        RemoveScheduleById(id);

                        SaveSchedulesToFile();
                        SendSchedulesToSettimePage();

                        Debug.WriteLine($"[C#] delete_schedule: {id}");
                        break;
                    }

                default:
                    Debug.WriteLine("[C#] Settime unknown action: " + action);
                    break;
            }
        }


        public class N8nTtsResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("text")]
            public string? Text { get; set; }
        }
        private async Task<int> PollN8nOnce(CancellationToken ct)
        {

            int pollAfterMs = _pollAfterMs;

            try
            {
                var url = string.IsNullOrWhiteSpace(_n8nWebhookUrl) ? DEFAULT_N8N_WEBHOOK_URL : _n8nWebhookUrl;
                if (string.IsNullOrWhiteSpace(url))
                    return 5000; // chưa có URL thì ngủ lâu, tránh spam lỗi

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("test_speaker");

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

                // 204 = queue rỗng
                if (resp.StatusCode == System.Net.HttpStatusCode.NoContent)
                    return pollAfterMs;

                // status lỗi (4xx/5xx)
                if (!resp.IsSuccessStatusCode)
                    return 5000;

                var json = await resp.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(json))
                    return pollAfterMs;

                N8nTtsResponse? data;
                try
                {
                    data = JsonSerializer.Deserialize<N8nTtsResponse>(json, _jsonOpts);
                }
                catch
                {
                    return 5000; // JSON lỗi -> ngủ lâu hơn
                }

                var text = (data?.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return pollAfterMs;

                // Chặn trùng (phòng worker gửi lại cùng nội dung)
                if (text == _lastN8nText && (DateTime.Now - _lastN8nAt).TotalSeconds < 3)
                    return pollAfterMs;

                _lastN8nText = text;
                _lastN8nAt = DateTime.Now;

                BeginInvoke(new Action(() =>
                {
                    _ = SpeakWithOpenAiAsync(text);
                }));

                return 100; // có tin -> poll nhanh để lấy tiếp
            }
            catch (OperationCanceledException)
            {
                throw; // để vòng while thoát ngay khi StopPolling()
            }
            catch
            {
                return 5000; // lỗi mạng -> ngủ 5s
            }
        }
        private void StartPolling()
        {
            // đã chạy rồi thì thôi
            if (_pollWorker != null && !_pollWorker.IsCompleted) return;

            _pollCts = new CancellationTokenSource();
            var token = _pollCts.Token;

            _pollWorker = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    // Mặc định ngủ 5 giây (5000ms) đề phòng mạng lỗi
                    int sleepTimeMs = 5000;

                    try
                    {
                        // Sửa hàm PollN8nOnce() để nó return về số mili-giây cần ngủ
                        // - Nếu có tin: return 100; (Ngủ 0.1s rồi vòng lại lấy tin tiếp)
                        // - Nếu hết tin: return 1200; (hoặc 3000, 5000 lấy từ biến pollAfterMs)
                        sleepTimeMs = await PollN8nOnce(token);
                    }
                    catch
                    {
                        // Nếu lỗi mạng, vẫn giữ sleepTimeMs = 5000 để ngủ 5 giây rồi thử lại
                    }

                    try
                    {
                        // Cho vòng lặp ngủ đúng số mili-giây đã nhận được
                        await Task.Delay(sleepTimeMs, token);
                    }
                    catch { }
                }
            }, token);
        }
        private void StopPolling()
        {
            try { _pollCts?.Cancel(); } catch { }
            _pollCts = null;
        }
        /////settime
        private void LoadSchedulesFromFile()
        {
            try
            {
                var dir = Path.GetDirectoryName(SchedulesPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(SchedulesPath))
                {
                    _schedules = new List<ScheduleItem>();
                    return;
                }

                var json = File.ReadAllText(SchedulesPath, Encoding.UTF8);
                var list = JsonSerializer.Deserialize<List<ScheduleItem>>(json, _jsonOpts);
                _schedules = list ?? new List<ScheduleItem>();

                Debug.WriteLine($"[C#] Loaded schedules: {_schedules.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadSchedulesFromFile ERROR: " + ex);
                _schedules = new List<ScheduleItem>();
            }
        }

        private void SaveSchedulesToFile()
        {
            try
            {
                var dir = Path.GetDirectoryName(SchedulesPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_schedules, _jsonOpts);
                File.WriteAllText(SchedulesPath, json, Encoding.UTF8);

                Debug.WriteLine($"[C#] Saved schedules: {_schedules.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveSchedulesToFile ERROR: " + ex);
            }
        }

        private void SendSchedulesToSettimePage()
        {
            if (webViewLogin.CoreWebView2 == null) return;

            void send()
            {
                try
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        action = "set_schedules",
                        schedules = _schedules
                    }, _jsonOpts);

                    webViewLogin.CoreWebView2.PostWebMessageAsJson(payload);
                    Debug.WriteLine("[C#] -> JS set_schedules sent");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("SendSchedulesToSettimePage ERROR: " + ex);
                }
            }

            if (InvokeRequired) BeginInvoke((Action)send);
            else send();
        }
        private void StartScheduleTimer()
        {
            if (_scheduleTimer != null) return;

            _scheduleTimer = new System.Windows.Forms.Timer();
            _scheduleTimer.Interval = 1000; // 1 giây
            _scheduleTimer.Tick += (s, e) => CheckSchedulesRealtime();
            _scheduleTimer.Start();

            Debug.WriteLine("[C#] Schedule timer started");
        }

        private void CheckSchedulesRealtime()
        {
            try
            {
                if (_schedules == null || _schedules.Count == 0) return;

                var now = DateTime.Now;
                string nowHHmm = now.ToString("HH:mm");
                string today = now.ToString("yyyyMMdd");

                // dọn cache key cũ cho nhẹ
                _fired.RemoveWhere(k => !k.StartsWith(today + "|", StringComparison.OrdinalIgnoreCase));

                foreach (var sch in _schedules)
                {
                    if (sch == null) continue;
                    if (!sch.enabled) continue;
                    if (string.IsNullOrWhiteSpace(sch.time)) continue;

                    if (!TimeSpan.TryParse(sch.time.Trim(), out var ts)) continue;
                    string schHHmm = $"{ts.Hours:00}:{ts.Minutes:00}";

                    if (!string.Equals(schHHmm, nowHHmm, StringComparison.Ordinal)) continue;

                    string id = string.IsNullOrWhiteSpace(sch.id) ? schHHmm : sch.id.Trim();
                    string key = $"{today}|{id}|{schHHmm}";
                    if (_fired.Contains(key)) continue; // đã báo trong phút này

                    _fired.Add(key);

                    BeginInvoke(new Action(() =>
                    {
                        if (string.Equals(sch.type, "âm nhạc", StringComparison.OrdinalIgnoreCase))
                        {
                            // ✅ lấy 15s/30s/45s từ sch.note
                            int sec = ParseDurationSeconds(sch.note, 30);

                            // ✅ phát nhạc đúng thời lượng rồi tự stop
                            _ = PlayMusicForSecondsAsync(_musicFilePath, sec);
                        }
                        if (string.Equals(sch.type, "thông báo", StringComparison.OrdinalIgnoreCase))
                        {
                            _ = SpeakWithOpenAiAsync(inputText);
                        }
                        if (string.Equals(sch.type, "tinh chỉnh giọng đọc", StringComparison.OrdinalIgnoreCase))
                        {
                            _ = SpeakWithOpenAiAsync(sch.note);
                        }
                    }));

                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CheckSchedulesRealtime ERROR: " + ex);
            }
        }

        private void SetScheduleEnabled(string id, bool enabled)
        {
            if (_schedules == null) return;

            for (int i = 0; i < _schedules.Count; i++)
            {
                if (string.Equals(_schedules[i]?.id, id, StringComparison.OrdinalIgnoreCase))
                {
                    _schedules[i].enabled = enabled;
                    return;
                }
            }
        }

        private void RemoveScheduleById(string id)
        {
            if (_schedules == null) return;
            _schedules.RemoveAll(s => string.Equals(s?.id, id, StringComparison.OrdinalIgnoreCase));
        }

        private static int ParseDurationSeconds(string? note, int fallback = 30)
        {
            if (string.IsNullOrWhiteSpace(note)) return fallback;

            // nhận "15s" hoặc "15"
            var s = note.Trim().ToLowerInvariant().Replace("s", "");
            if (int.TryParse(s, out var sec) && sec > 0) return sec;

            return fallback;
        }

        private async Task PlayMusicForSecondsAsync(string filePath, int seconds)
        {
            try
            {
                // hủy lần phát trước nếu có
                _musicCts?.Cancel();
                _musicCts = new CancellationTokenSource();
                var token = _musicCts.Token;

                PlayMusic(filePath); // dùng hàm bạn đang có

                await Task.Delay(TimeSpan.FromSeconds(seconds), token);

                StopMusic(); // dừng sau X giây
            }
            catch (TaskCanceledException)
            {
                // bị hủy vì có lịch mới phát -> bỏ qua
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PlayMusicForSecondsAsync] ERROR: " + ex);
            }
        }
        //// ADMIN
        // ===================== ADMIN CONFIG HANDLER =====================

        private void HandleAdminActions(string action, JsonElement root)
        {
            switch (action)
            {
                case "nav":
                    {
                        // JS có thể gửi: post("nav",{target:"function"})
                        var target = root.TryGetProperty("target", out var t) ? (t.GetString() ?? "function") : "function";
                        Nav(target);
                        break;
                    }

                case "config_get":
                    {
                        SendAdminConfigToAdminPage();
                        break;
                    }

                case "config_save":
                    {
                        if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Array)
                        {
                            ReplyToJs(new { action = "config_saved", ok = false, error = "Missing data[]" });
                            return;
                        }

                        try
                        {
                            var items = JsonSerializer.Deserialize<List<AdminConfigRow>>(dataEl.GetRawText(), _jsonOpts)
                                        ?? new List<AdminConfigRow>();

                            // mã hóa password trước khi lưu (khuyến nghị)
                            if (UseDpapiForPassword)
                            {
                                foreach (var it in items)
                                {
                                    if (!string.IsNullOrWhiteSpace(it.password))
                                        it.password = Protect(it.password);
                                }
                            }

                            SaveAdminConfigFile(items);

                            ReplyToJs(new { action = "config_saved", ok = true, path = AdminConfigPath });
                        }
                        catch (Exception ex)
                        {
                            ReplyToJs(new { action = "config_saved", ok = false, error = ex.Message });
                        }
                        break;
                    }

                default:
                    Debug.WriteLine("[C#] Admin unknown action: " + action);
                    break;
            }
        }

        private void SaveAdminConfigFile(List<AdminConfigRow> items)
        {
            var dir = Path.GetDirectoryName(AdminConfigPath);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(items, _jsonOpts);
            File.WriteAllText(AdminConfigPath, json, Encoding.UTF8);

            Debug.WriteLine("[C#] Saved admin config: " + items.Count);
        }

        private List<AdminConfigRow> LoadAdminConfigFile()
        {
            try
            {
                var dir = Path.GetDirectoryName(AdminConfigPath);
                if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(AdminConfigPath)) return new List<AdminConfigRow>();

                var json = File.ReadAllText(AdminConfigPath, Encoding.UTF8);
                var items = JsonSerializer.Deserialize<List<AdminConfigRow>>(json, _jsonOpts) ?? new List<AdminConfigRow>();

                // giải mã để đổ xuống UI (để user sửa lại)
                if (UseDpapiForPassword)
                {
                    foreach (var it in items)
                    {
                        if (!string.IsNullOrWhiteSpace(it.password))
                            it.password = UnprotectSafe(it.password);
                    }
                }

                Debug.WriteLine("[C#] Loaded admin config: " + items.Count);
                return items;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("LoadAdminConfigFile ERROR: " + ex);
                return new List<AdminConfigRow>();
            }
        }

        private void SendAdminConfigToAdminPage()
        {
            if (webViewLogin.CoreWebView2 == null) return;

            void send()
            {
                try
                {
                    var items = LoadAdminConfigFile();
                    var payload = JsonSerializer.Serialize(new
                    {
                        action = "config_load",
                        ok = true,
                        data = items
                    }, _jsonOpts);

                    webViewLogin.CoreWebView2.PostWebMessageAsJson(payload);
                    Debug.WriteLine("[C#] -> JS config_load sent");
                }
                catch (Exception ex)
                {
                    var payload = JsonSerializer.Serialize(new
                    {
                        action = "config_load",
                        ok = false,
                        error = ex.Message,
                        data = new List<AdminConfigRow>()
                    }, _jsonOpts);

                    webViewLogin.CoreWebView2.PostWebMessageAsJson(payload);
                }
            }

            if (InvokeRequired) BeginInvoke((Action)send);
            else send();
        }

        private void ReplyToJs(object payload)
        {
            if (webViewLogin.CoreWebView2 == null) return;

            void send()
            {
                try
                {
                    var json = JsonSerializer.Serialize(payload, _jsonOpts);
                    webViewLogin.CoreWebView2.PostWebMessageAsJson(json);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ReplyToJs ERROR: " + ex);
                }
            }

            if (InvokeRequired) BeginInvoke((Action)send);
            else send();
        }

        // ===== DPAPI encrypt/decrypt (mã hóa theo Windows user) =====
        private static string Protect(string plain)
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }

        private static string UnprotectSafe(string maybeBase64)
        {

            try
            {
                var enc = Convert.FromBase64String(maybeBase64);
                var dec = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch
            {
                return maybeBase64; // nếu file cũ lưu plain thì giữ nguyên
            }
        }
        private bool TryLoadAdminConfigToVars()
        {
            var cfg = LoadAdminConfigFile().FirstOrDefault(); // lấy dòng đầu tiên
            if (cfg == null) return false;

            _adminCompany = (cfg.company ?? "").Trim();
            _adminUrl = (cfg.url ?? "").Trim();
            _adminUsername = (cfg.username ?? "").Trim();
            _adminPassword = (cfg.password ?? ""); // LoadAdminConfigFile đã Unprotect nếu bật DPAPI

            return true;
        }


    }
}