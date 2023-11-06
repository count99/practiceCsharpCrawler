using Microsoft.Playwright;
using Tesseract;
using System.Text.RegularExpressions;

namespace Badminton;

class BeitouDepot: IGetFreeSet
{
    private static List<string> _freeDay = new List<string>();
    private static List<string> _resultTable =new List<string>();
    private bool _headless = true;

    public BeitouDepot(bool headless=true)
    {
        _headless = headless;
    }

    private static async Task<string> GetVerificationCode(byte[] bytes)
    {
        using (var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default))
        {
            using (var img = Pix.LoadFromMemory(bytes))
            {
                using (var test = engine.Process(img))
                {
                    string resultString = "";
                    string ocr_txt= test.GetText();
                    Regex rgx = new Regex(@"\d+", RegexOptions.IgnoreCase);
                    MatchCollection matches = rgx.Matches(ocr_txt);
                    
                    if (matches.Count > 0)
                    {
                        foreach (Match match in matches)
                            resultString = match.Value;
                    }
                    return resultString;
                }
            }
        }
    }

    private static async Task GetPerDayData(IBrowserContext context, string thisMonth, string thisDay)
    {
        string thisUrl =
            $"https://resortbooking.metro.taipei/MT02.aspx?module=net_booking&files=booking_place&StepFlag=2&PT=1&D={thisMonth}{thisDay}&D2="; 
        var page = await context.NewPageAsync();
        Regex rgx = new Regex(@"「(\w.+)」", RegexOptions.IgnoreCase);
        for (int i = 1; i < 5; i++)
        {
            await page.GotoAsync(thisUrl + i);
            IElementHandle? img = await page.QuerySelectorAsync("img[src=\"img/sche01.png\"]"); // 查詢底下的 img src="img/sche01.png" 的元素
            string? onclick = img != null ? await img.GetAttributeAsync("onclick") : null; // 獲取 img 元素的 onclick 屬性值
            if (onclick != null)
            {
                MatchCollection matches = rgx.Matches(onclick);
                string getResult = matches[0].Groups[1].Value;
                _resultTable.Add($"{thisMonth}{thisDay} {getResult}有場");
            }
        }
        await page.CloseAsync();
    }

    public async Task Run()
    {
        var MyIni = new IniFile("Settings.ini");
        string ID = MyIni.Read("ID");
        string PW = MyIni.Read("PW");
        string URL = MyIni.Read("URL");
        string expectedUrl = MyIni.Read("expectedUrl");
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = _headless,
            // Headless = true,
        });
        var context = await browser.NewContextAsync();

        var page = await context.NewPageAsync();

        await page.GotoAsync(URL);

        await page.GetByText("羽球").ClickAsync();

        void page_Dialog_EventHandler(object sender, IDialog dialog)
        {
            Console.WriteLine($"Dialog message: {dialog.Message}");
            dialog.DismissAsync();
            page.Dialog -= page_Dialog_EventHandler;
        }

        page.Dialog += page_Dialog_EventHandler;
        await page.GetByRole(AriaRole.Cell, new(){ Name = "羽球場使用須知" }).Locator("img").ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "OK" }).ClickAsync();

        while (page.Url == expectedUrl)
        {
            await page.Locator("#ContentPlaceHolder1_loginid").FillAsync(ID);

            await page.Locator("#loginpw").FillAsync(PW);

            // 驗證碼
            var bytes = await page.Locator("#ContentPlaceHolder1_CaptchaImage").ScreenshotAsync();

            string ocrTxt = await GetVerificationCode(bytes);
            Console.WriteLine($"first ocrTxt={ocrTxt}");

            while (ocrTxt == "")
            {
                await page.GetByText("↻").ClickAsync();
                bytes = await page.Locator("#ContentPlaceHolder1_CaptchaImage").ScreenshotAsync();
                ocrTxt = await GetVerificationCode(bytes);
                Console.WriteLine($"ocrTxt={ocrTxt}");
            }

            await page.Locator("#ContentPlaceHolder1_Captcha_text").FillAsync(ocrTxt);

            await page.Locator("#login_but").ClickAsync();
        }

        string todayDay = await page.Locator("#ContentPlaceHolder1_NowDate_Lab").InnerHTMLAsync();
        DateTime tempDate = DateTime.ParseExact(todayDay, "yyyy年MM月dd日", null);
        todayDay = tempDate.ToString("yyyy/MM/");
        var elements = await page.QuerySelectorAllAsync("td[bgcolor=\"#87C675\"]");
        foreach (var element in elements)
        {
            IElementHandle tr = await element.QuerySelectorAsync("table > tbody > tr:nth-child(1)");
            string text = await tr.TextContentAsync();
            Regex rgx = new Regex(@"\d+", RegexOptions.IgnoreCase);
            MatchCollection matches = rgx.Matches(text);
            _freeDay.Add(matches[0].Value);
        }

        foreach (string day in _freeDay)
        {
            await GetPerDayData(context, todayDay, day);
        }

        foreach (string thisDay in _resultTable)
        {
            Console.WriteLine(thisDay);
        }
        // await page.PauseAsync();
    }
}