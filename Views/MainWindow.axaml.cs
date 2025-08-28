using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Layout;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
// Removed WebDriverManager, Selenium Manager will resolve drivers automatically

namespace InstagramDMSender.Avalonia;

public partial class MainWindow : Window
{
    private ChromeDriver? _driver;
    private InstagramDMAutomation? _automation;
    private readonly List<TargetUser> _users = new();
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        WireEvents();
        RefreshUsersUI();
    }

    private void WireEvents()
    {
        AddButton.Click += OnAddClicked;
        DeleteSelectedButton.Click += OnDeleteSelectedClicked;
        StartBrowserButton.Click += OnStartBrowser;
        StartSendingButton.Click += OnStartSending;
        StopButton.Click += OnStop;
    }

    private void AppendLog(string line)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            LogText.Text += line;
            LogText.CaretIndex = LogText.Text?.Length ?? 0;
        });
    }

    private void SetStatus(string text) => Dispatcher.UIThread.InvokeAsync(() => StatusLabel.Text = text);
    private void SetCurrentUser(string text) => Dispatcher.UIThread.InvokeAsync(() => CurrentUserLabel.Text = text);
    private void SetProgress(double value) => Dispatcher.UIThread.InvokeAsync(() => ProgressBar.Value = value);

    private async void OnStartBrowser(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_driver != null)
            {
                await ShowInfo("안내", "Chrome 브라우저가 이미 실행 중입니다.");
                return;
            }

            var options = new ChromeOptions();
            options.AddArgument("--no-sandbox");
            options.AddArgument("--disable-dev-shm-usage");
            options.AddArgument("--window-size=800,800");
            options.AddExcludedArgument("enable-automation");
            options.AddAdditionalOption("useAutomationExtension", false);

            _driver = new ChromeDriver(options);
            _driver.Manage().Window.Size = new System.Drawing.Size(800, 800);

            _automation = new InstagramDMAutomation(_driver);

            var username = LoginUsername.Text?.Trim() ?? "";
            var password = LoginPassword.Text?.Trim() ?? "";

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                var ok = await Task.Run(() => _automation.Login(username, password));
                if (ok) await ShowInfo("안내", $"{username} 계정으로 로그인되었습니다.");
                else await ShowWarn("경고", $"{username} 계정 로그인에 실패했습니다.");
            }
            else
            {
                _driver.Navigate().GoToUrl("https://www.instagram.com/direct/inbox/");
                await ShowInfo("안내", "인스타그램에 로그인하시거나 상단에 아이디/비밀번호 입력 후 다시 시도하세요.");
            }
        }
        catch (Exception ex)
        {
            await ShowError("오류", $"브라우저 실행 실패: {ex.Message}");
            try { _driver?.Quit(); } catch { }
            _driver = null;
            _automation = null;
        }
    }

    private async void OnStartSending(object? sender, RoutedEventArgs e)
    {
        SetStatus("상태: 전송 시작 클릭됨");
        AppendLog($"[{DateTime.Now:HH:mm:ss}] 전송 시작 요청됨.\n");
        if (_driver is null || _automation is null)
        {
            SetStatus("상태: 브라우저 미실행");
            AppendLog($"[{DateTime.Now:HH:mm:ss}] 브라우저가 실행되지 않았습니다. 먼저 브라우저 시작을 눌러주세요.\n");
            await ShowError("오류", "브라우저를 먼저 실행해주세요.");
            return;
        }
        if (_users.Count == 0)
        {
            SetStatus("상태: 사용자 목록 비어 있음");
            AppendLog($"[{DateTime.Now:HH:mm:ss}] 전송 대상 사용자가 없습니다. 사용자를 추가하세요.\n");
            await ShowError("오류", "전송할 사용자를 추가해주세요.");
            return;
        }
        var template = (MessageTemplate.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(template))
        {
            SetStatus("상태: 템플릿 미입력");
            AppendLog($"[{DateTime.Now:HH:mm:ss}] 메시지 템플릿이 비어 있습니다.\n");
            await ShowError("오류", "메시지 템플릿을 입력해주세요.");
            return;
        }

        if (!double.TryParse(IntervalMin.Text, out var intervalMin)) intervalMin = 3;
        if (!double.TryParse(IntervalMax.Text, out var intervalMax)) intervalMax = 5;
        if (!int.TryParse(PauseUsers.Text, out var pauseUsers)) pauseUsers = 20;
        if (!int.TryParse(PauseDuration.Text, out var pauseDuration)) pauseDuration = 20;
        if (!int.TryParse(MaxUsers.Text, out var maxUsers)) maxUsers = 100;
        if (!int.TryParse(PostsMin.Text, out var postsMin)) postsMin = 1;
        if (!int.TryParse(PostsMax.Text, out var postsMax)) postsMax = 100000;
        if (!int.TryParse(FollowersMin.Text, out var followersMin)) followersMin = 0;
        if (!int.TryParse(FollowersMax.Text, out var followersMax)) followersMax = 10000000;
        var privacy = PrivacyCombo.SelectedIndex; // 0:모든, 1:공개만, 2:비공개만
        var useFiltersFlag = UseFiltersCheck.IsChecked ?? true; // UI 스레드에서 캡처

        foreach (var u in _users) u.Status = UserStatus.Waiting;
        RefreshUsersUI();

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                var sentCount = 0;
                var processed = 0;
                var total = Math.Min(_users.Count, maxUsers);
                var useFilters = useFiltersFlag;
                var minPosts = postsMin; var maxPosts = postsMax; var minFollowers = followersMin; var maxFollowers = followersMax; var privacyCaptured = privacy;

                for (int i = 0; i < total; i++)
                {
                    AppendLog($"[{DateTime.Now:HH:mm:ss}] 진행: {i + 1}/{total} 단계 시작.\n");
                    if (ct.IsCancellationRequested)
                    {
                        SetStatus("상태: 사용자에 의해 중지됨");
                        break;
                    }

                    var user = _users[i];
                    AppendLog($"[{DateTime.Now:HH:mm:ss}] {user.Username}: 상태 갱신 준비.\n");
                    UpdateUserStatus(user, UserStatus.InProgress);
                    SetCurrentUser($"현재 사용자: {user.Username}");

                    if (useFilters)
                    {
                        SetStatus($"상태: {user.Username} 필터링 중...");
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] {user.Username}: 필터링 호출.\n");
                        var info = _automation.FilterAccount(user.Username);
                        if (info is null)
                        {
                            AppendLog($"[{DateTime.Now:HH:mm:ss}] {user.Username}: 계정 정보 필터링 실패. 건너뜁니다.\n");
                            UpdateUserStatus(user, UserStatus.Failed);
                            processed++;
                            SetProgress(processed * 100.0 / total);
                            SetStatus($"상태: {sentCount}/{total} 전송 완료 (다음 사용자 대기 중)");
                            await Task.Delay(TimeSpan.FromSeconds(RandomShared(1, 3)), ct);
                            continue;
                        }
                        if (!CheckFilters(info, minPosts, maxPosts, minFollowers, maxFollowers, privacyCaptured))
                        {
                            AppendLog($"[{DateTime.Now:HH:mm:ss}] {user.Username}: 필터 조건 미부합. 건너뜁니다.\n");
                            UpdateUserStatus(user, UserStatus.Failed);
                            processed++;
                            SetProgress(processed * 100.0 / total);
                            SetStatus($"상태: {sentCount}/{total} 전송 완료 (다음 사용자 대기 중)");
                            await Task.Delay(TimeSpan.FromSeconds(RandomShared(1, 3)), ct);
                            continue;
                        }
                    }
                    else
                    {
                        SetStatus($"상태: {user.Username} 필터링 건너뜀");
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] {user.Username}: 필터링 비활성화됨. 바로 전송 시도.\n");
                    }

                    if (new Random().NextDouble() < 0.3)
                    {
                        SetStatus($"상태: {user.Username} 계정 웜업 활동 중...");
                        _automation.PerformRandomActivity();
                    }

                    SetStatus($"상태: {user.Username}에게 DM 전송 시도 중...");
                    AppendLog($"[{DateTime.Now:HH:mm:ss}] {user.Username}: 전송 호출.\n");
                    var ok = _automation.SendDm(user.Username, new List<string> { template });
                    if (ok)
                    {
                        sentCount++;
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] {user.Username}에게 DM 전송 성공.\n");
                        UpdateUserStatus(user, UserStatus.Completed);
                    }
                    else
                    {
                        AppendLog($"[{DateTime.Now:HH:mm:ss}] {user.Username}에게 DM 전송 실패.\n");
                        UpdateUserStatus(user, UserStatus.Failed);
                    }

                    processed++;
                    SetProgress(processed * 100.0 / total);
                    SetStatus($"상태: {sentCount}/{total} 전송 완료 (다음 사용자 대기 중)");

                    if (ct.IsCancellationRequested) break;

                    if (processed % pauseUsers == 0 && processed > 0 && sentCount < total)
                    {
                        SetStatus($"상태: {pauseDuration}분 휴식중...");
                        await Task.Delay(TimeSpan.FromMinutes(pauseDuration), ct);
                    }

                    if (i < total - 1)
                    {
                        var waitSeconds = RandomShared(intervalMin * 60, intervalMax * 60);
                        try { _automation.PerformRandomActivity(); } catch { }
                        SetStatus($"상태: 다음 전송까지 {Math.Max(1, (int)(waitSeconds / 60))}분 대기 중...");
                        await Task.Delay(TimeSpan.FromSeconds(waitSeconds), ct);
                    }
                }

                if (!ct.IsCancellationRequested)
                {
                    SetStatus($"상태: 완료. 총 {sentCount}개의 메시지 전송됨");
                    SetCurrentUser("현재 사용자: -");
                    SetProgress(100);
                    await ShowInfo("완료", $"총 {sentCount}개의 메시지 전송이 완료되었습니다.");
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                AppendLog($"[{DateTime.Now:HH:mm:ss}] 오류: {ex.Message}\n");
                await ShowError("오류", ex.Message);
            }
        }, ct);
    }

    private void OnStop(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        SetStatus("상태: 중지됨");
        _automation?.Stop();
        foreach (var u in _users)
            if (u.Status == UserStatus.InProgress) u.Status = UserStatus.Waiting;
        RefreshUsersUI();
    }

    private void UpdateUserStatus(TargetUser user, UserStatus status)
    {
        user.Status = status;
        RefreshUsersUI();
    }

    private void RefreshUsersUI()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            UsersPanel.Children.Clear();
            foreach (var u in _users)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var cb = new CheckBox { IsChecked = u.Selected };
                cb.IsCheckedChanged += (_, __) => u.Selected = cb.IsChecked ?? false;

                var name = new TextBlock { Text = u.Username, Width = 200, VerticalAlignment = VerticalAlignment.Center };
                var status = new TextBlock { Text = $"{StatusIcon(u.Status)} {u.Status.StatusText()}", VerticalAlignment = VerticalAlignment.Center };

                row.Children.Add(cb);
                row.Children.Add(name);
                row.Children.Add(status);
                UsersPanel.Children.Add(row);
            }
        });
    }

    private static string StatusIcon(UserStatus s) => s switch
    {
        UserStatus.Waiting => "⌛",
        UserStatus.InProgress => "⏳",
        UserStatus.Completed => "✅",
        UserStatus.Failed => "❌",
        _ => ""
    };

    private async void OnAddClicked(object? sender, RoutedEventArgs e)
    {
        var raw = (UsernameEntry.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            await ShowWarn("경고", "사용자 이름을 입력하세요.");
            return;
        }
        var parts = raw.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        int added = 0;
        foreach (var p in parts)
        {
            var name = p.Trim();
            if (name.Length == 0) continue;
            if (_users.Exists(x => x.Username.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
            _users.Add(new TargetUser { Username = name, Status = UserStatus.Waiting });
            added++;
        }
        _users.Sort((a, b) => string.Compare(a.Username, b.Username, StringComparison.OrdinalIgnoreCase));
        RefreshUsersUI();
        UsernameEntry.Text = string.Empty;

        if (added > 0) await ShowInfo("성공", $"{added}개 사용자 추가됨.");
        else await ShowWarn("경고", "이미 목록에 있는 사용자입니다.");
    }

    private void OnDeleteSelectedClicked(object? sender, RoutedEventArgs e)
    {
        _users.RemoveAll(x => x.Selected);
        RefreshUsersUI();
    }

    private bool CheckFilters(AccountInfo info, int postsMin, int postsMax, int followersMin, int followersMax, int privacyIndex)
    {
        if (info.PostsCount < postsMin || info.PostsCount > postsMax) return false;
        if (info.FollowersCount < followersMin || info.FollowersCount > followersMax) return false;
        if (privacyIndex == 1 && info.IsPrivate) return false;        // 공개만
        if (privacyIndex == 2 && !info.IsPrivate) return false;       // 비공개만
        return true;
    }

    private static int RandomShared(double min, double max)
    {
        var r = new Random();
        return (int)Math.Round(min + r.NextDouble() * (max - min));
    }

    private Task ShowInfo(string title, string msg)
    {
        Dispatcher.UIThread.Post(() => { _ = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(title, msg).ShowAsync(); });
        return Task.CompletedTask;
    }
    private Task ShowWarn(string title, string msg)
    {
        Dispatcher.UIThread.Post(() => { _ = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(title, msg).ShowAsync(); });
        return Task.CompletedTask;
    }
    private Task ShowError(string title, string msg)
    {
        Dispatcher.UIThread.Post(() => { _ = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(title, msg).ShowAsync(); });
        return Task.CompletedTask;
    }
}

public enum UserStatus { Waiting, InProgress, Completed, Failed }

public static class UserStatusExt
{
    public static string StatusText(this UserStatus s) => s switch
    {
        UserStatus.Waiting => "waiting",
        UserStatus.InProgress => "in progress",
        UserStatus.Completed => "completed",
        UserStatus.Failed => "failed",
        _ => ""
    };
}

public class TargetUser
{
    public string Username { get; set; } = "";
    public bool Selected { get; set; }
    public UserStatus Status { get; set; } = UserStatus.Waiting;
}

public class AccountInfo
{
    public int PostsCount { get; set; }
    public int FollowersCount { get; set; }
    public bool IsPrivate { get; set; }
}
