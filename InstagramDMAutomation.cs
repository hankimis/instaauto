using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace InstagramDMSender.Avalonia;

public class InstagramDMAutomation
{
    private readonly IWebDriver _driver;

    public InstagramDMAutomation(IWebDriver driver)
    {
        _driver = driver;
    }

    public void Stop()
    {
        try { _driver.Quit(); } catch { }
    }

    public bool Login(string username, string password, int maxRetries = 3)
    {
        int retry = 0;
        while (retry < maxRetries)
        {
            try
            {
                _driver.Navigate().GoToUrl("https://www.instagram.com/accounts/login/");
                var wait = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(300));
                wait.Until(ExpectedConditions.ElementExists(By.XPath("//input[@name='username']")));

                var usernameField = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//input[@name='username']")));
                usernameField.Click();
                foreach (var ch in username) { usernameField.SendKeys(ch.ToString()); Thread.Sleep(TimeSpan.FromMilliseconds(RandomMs(50, 150))); }

                var pwdField = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//input[@name='password']")));
                pwdField.Click();
                foreach (var ch in password) { pwdField.SendKeys(ch.ToString()); Thread.Sleep(TimeSpan.FromMilliseconds(RandomMs(50, 150))); }

                var loginBtn = wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath("//button[@type='submit']")));
                loginBtn.Click();

                var ok = WaitLoginSuccess(wait, TimeSpan.FromSeconds(30));
                if (ok)
                {
                    try
                    {
                        var notNow = _driver.FindElements(By.XPath("//button[contains(text(), '나중에 하기') or contains(text(), 'Not Now')]")).FirstOrDefault();
                        notNow?.Click();
                    } catch { }
                    return true;
                }
                retry++;
                try { _driver.Navigate().Refresh(); } catch { }
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
            catch
            {
                retry++;
                Thread.Sleep(TimeSpan.FromSeconds(2));
                try { _driver.Navigate().Refresh(); } catch { }
            }
        }
        return false;
    }

    private bool WaitLoginSuccess(WebDriverWait wait, TimeSpan total)
    {
        var end = DateTime.UtcNow + total;
        while (DateTime.UtcNow < end)
        {
            try
            {
                if (_driver.Url.Contains("/direct/inbox") ||
                    _driver.FindElements(By.XPath("//a[contains(@href, '/direct/inbox/')]")).Any() ||
                    _driver.FindElements(By.XPath("//svg[@aria-label='홈' or @aria-label='Home']")).Any())
                    return true;
            }
            catch { }
            Thread.Sleep(1000);
        }
        return false;
    }

    public bool SendDm(string username, List<string> templates)
    {
        try
        {
            _driver.Navigate().GoToUrl($"https://www.instagram.com/{username}/");
            var wait = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(300));
            wait.Until(ExpectedConditions.ElementExists(By.XPath("//main")));

            // display name
            string displayName = username;
            try
            {
                var xpaths = new[]
                {
                    "//h2/span[@title]",
                    "//h2/span",
                    "//span[@title]"
                };
                var candidates = new List<string>();
                foreach (var xp in xpaths)
                {
                    foreach (var el in _driver.FindElements(By.XPath(xp)))
                    {
                        var val = (el.GetAttribute("title") ?? el.Text ?? "").Trim();
                        if (!string.IsNullOrEmpty(val) && !Regex.IsMatch(val.Replace(",", ""), @"^\d+$"))
                            candidates.Add(val);
                    }
                }
                if (candidates.Count > 0) displayName = candidates.OrderByDescending(x => x.Length).First();
            }
            catch { }

            // template pick + spintax
            var msg = RenderSpintax(templates[0].Replace("<Username>", displayName));

            // Message button - try direct message button first
            var messageButtonXPaths = new[]
            {
                "//div[@role='button' and (text()='메시지 보내기' or text()='Message')]",
                "//div[@role='button' and (contains(text(), '메시지') or contains(text(), 'Message'))]",
                "//button[contains(text(), '메시지') or contains(text(), 'Message')]",
                "//a[contains(@href, '/direct/t/') and (contains(.,'메시지') or contains(.,'Message'))]"
            };
            IWebElement? messageButton = null;
            foreach (var xp in messageButtonXPaths)
            {
                try
                {
                    messageButton = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(7), TimeSpan.FromMilliseconds(200))
                        .Until(ExpectedConditions.ElementToBeClickable(By.XPath(xp)));
                    if (messageButton != null) break;
                }
                catch { }
            }
            
            // If direct message button not found, try option button approach
            if (messageButton is null)
            {
                try
                {
                    IWebElement? optionBtn = null;
                    var optionButtonXPaths = new[]
                    {
                        "//div[@role='button' and .//svg[@aria-label='옵션' or @aria-label='Options']]",
                        "//button[.//svg[@aria-label='옵션' or @aria-label='Options']]",
                        "//div[contains(@class,'x1i10hfl') and contains(@class,'x972fbf') and @role='button' and .//svg[@aria-label='옵션' or @aria-label='Options']]",
                        "//div[contains(@class,'x1q0g3np')]//div[@role='button' and .//svg[@aria-label='옵션' or @aria-label='Options']]"
                    };
                    foreach (var xp in optionButtonXPaths)
                    {
                        try
                        {
                            optionBtn = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200))
                                .Until(ExpectedConditions.ElementToBeClickable(By.XPath(xp)));
                            if (optionBtn != null) break;
                        }
                        catch { }
                    }
                    if (optionBtn != null)
                    {
                        optionBtn.Click();
                        Thread.Sleep(TimeSpan.FromMilliseconds(RandomMs(800, 1200)));
                        try
                        {
                            var dropdownMsg = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(7), TimeSpan.FromMilliseconds(200))
                                .Until(ExpectedConditions.ElementToBeClickable(By.XPath("//button[contains(text(), '메시지 보내기') or contains(text(),'Message')]")));
                            if (dropdownMsg != null) 
                            {
                                messageButton = dropdownMsg;
                                Thread.Sleep(TimeSpan.FromMilliseconds(RandomMs(300, 600)));
                            }
                        }
                        catch { }
                    }
                }
                catch { }
                if (messageButton is null) return false;
            }
            messageButton.Click();
            Thread.Sleep(TimeSpan.FromSeconds(RandomSec(1.2, 2.0)));

            // message input (KR/EN)
            IWebElement input;
            try
            {
                input = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(200))
                    .Until(ExpectedConditions.ElementExists(By.XPath("//div[@aria-label='메시지' and @contenteditable='true' and @role='textbox']")));
            }
            catch
            {
                input = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200))
                    .Until(ExpectedConditions.ElementExists(By.XPath("//div[@aria-label='Message' and @contenteditable='true' and @role='textbox']")));
            }

            foreach (var line in msg.Split('\n'))
            {
                foreach (var ch in line)
                {
                    input.SendKeys(ch.ToString());
                    Thread.Sleep(TimeSpan.FromMilliseconds(RandomMs(80, 220)));
                }
                input.SendKeys(OpenQA.Selenium.Keys.Shift + OpenQA.Selenium.Keys.Enter);
                Thread.Sleep(TimeSpan.FromMilliseconds(RandomMs(80, 180)));
            }
            Thread.Sleep(TimeSpan.FromMilliseconds(RandomMs(500, 1000)));

            // send button - multiple strategies including aria-label
            IWebElement sendBtn = null;
            var sendButtonSelectors = new[]
            {
                "//div[@role='button' and @aria-label='Send']",
                "//div[@role='button' and @aria-label='보내기']",
                "//div[@role='button' and (text()='보내기' or text()='Send')]",
                "//button[contains(text(),'보내기') or contains(text(),'Send')]",
                "//div[contains(@class, 'x1i10hfl') and @role='button']",
                "//div[@aria-label='Send' and @role='button']"
            };

            foreach (var selector in sendButtonSelectors)
            {
                try
                {
                    sendBtn = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(200))
                        .Until(ExpectedConditions.ElementToBeClickable(By.XPath(selector)));
                    if (sendBtn != null) break;
                }
                catch { }
            }

            // If still no button found, try pressing Enter key
            if (sendBtn == null)
            {
                try
                {
                    input.SendKeys(OpenQA.Selenium.Keys.Enter);
                    Thread.Sleep(TimeSpan.FromSeconds(RandomSec(2, 3.5)));
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            try
            {
                sendBtn.Click();
                Thread.Sleep(TimeSpan.FromSeconds(RandomSec(2, 3.5)));
                return true;
            }
            catch
            {
                // Fallback to Enter key if click fails
                try
                {
                    input.SendKeys(OpenQA.Selenium.Keys.Enter);
                    Thread.Sleep(TimeSpan.FromSeconds(RandomSec(2, 3.5)));
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
        catch
        {
            return false;
        }
    }

    public AccountInfo? FilterAccount(string username)
    {
        try
        {
            _driver.Navigate().GoToUrl($"https://www.instagram.com/{username}/");
            var wait = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(300));
            wait.Until(ExpectedConditions.ElementExists(By.XPath("//main")));
            Thread.Sleep(TimeSpan.FromSeconds(RandomSec(1.5, 2.5)));

            int posts = 0, followers = 0;
            try
            {
                // 인스타 UI는 수시 변경됨. 아래는 숫자 수집의 예시 패턴
                var spans = _driver.FindElements(By.XPath("//span[contains(@class, 'xdj266r')]"));
                var nums = new List<int>();
                foreach (var s in spans)
                {
                    var txt = (s.Text ?? "").Replace(",", "").Replace(".", "").Trim();
                    if (int.TryParse(txt, out var n)) nums.Add(n);
                }
                if (nums.Count >= 2) { posts = nums[0]; followers = nums[1]; }
            }
            catch { }

            bool isPrivate = false;
            try
            {
                var privateIndicators = new[]
                {
                    "//h2[contains(text(), '비공개 계정입니다') or contains(text(), 'This account is private')]",
                    "//div[contains(text(), '비공개 계정입니다') or contains(text(), 'This account is private')]"
                };
                foreach (var xp in privateIndicators)
                    if (_driver.FindElements(By.XPath(xp)).Count > 0) { isPrivate = true; break; }
            }
            catch { }

            return new AccountInfo { PostsCount = posts, FollowersCount = followers, IsPrivate = isPrivate };
        }
        catch
        {
            return null;
        }
    }

    public void PerformRandomActivity()
    {
        try
        {
            var r = new Random().Next(0, 3);
            if (r == 0) ViewReels();
            else if (r == 1) ExploreHashtags();
            else ScrollFeed();
        } catch { }
    }

    private void ViewReels()
    {
        try
        {
            _driver.Navigate().GoToUrl("https://www.instagram.com/reels/");
            var wait = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200));
            wait.Until(ExpectedConditions.ElementExists(By.TagName("video")));
            Thread.Sleep(TimeSpan.FromSeconds(RandomSec(30, 60)));
        } catch { }
    }

    private void ExploreHashtags()
    {
        try
        {
            var tags = new[] { "travel", "food", "fashion", "art", "music", "sports", "nature" };
            var r = new Random();
            var tag = tags[r.Next(tags.Length)];
            _driver.Navigate().GoToUrl($"https://www.instagram.com/explore/tags/{tag}/");
            var wait = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200));
            wait.Until(ExpectedConditions.ElementExists(By.CssSelector("a[href*='/p/']")));
            Thread.Sleep(TimeSpan.FromSeconds(RandomSec(15, 30)));
        } catch { }
    }

    private void ScrollFeed()
    {
        try
        {
            _driver.Navigate().GoToUrl("https://www.instagram.com/");
            var wait = new WebDriverWait(new SystemClock(), _driver, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200));
            wait.Until(ExpectedConditions.ElementExists(By.TagName("article")));
            var end = DateTime.UtcNow.AddSeconds(RandomSec(40, 70));
            while (DateTime.UtcNow < end)
            {
                ((IJavaScriptExecutor)_driver).ExecuteScript("window.scrollBy(0, window.innerHeight * 0.7);");
                Thread.Sleep(TimeSpan.FromSeconds(RandomSec(2.0, 3.5)));
            }
        } catch { }
    }

    private static string RenderSpintax(string text)
    {
        var rx = new Regex(@"\{([^{}]*)\}");
        while (true)
        {
            var m = rx.Match(text);
            if (!m.Success) break;
            var content = m.Groups[1].Value;
            string[] options;
            if (content.Contains('|'))
                options = content.Split('|');
            else if (content.TrimStart().StartsWith(","))
                options = new[] { "", content };
            else
                options = new[] { content };
            var replacement = options[new Random().Next(options.Length)];
            text = text.Substring(0, m.Index) + replacement + text.Substring(m.Index + m.Length);
        }
        return text;
    }

    private static int RandomMs(int min, int max) => new Random().Next(min, max + 1);
    private static double RandomSec(double min, double max) => min + new Random().NextDouble() * (max - min);
}
