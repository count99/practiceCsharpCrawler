using Badminton;

class Program
{
    static async Task Main(string[] args)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew(); 
        IGetFreeSet beitou = new BeitouDepot(false);
        await beitou.Run();
        watch.Stop();
        var elapsedMs = watch.ElapsedMilliseconds;
        Console.WriteLine($"程式執行{elapsedMs/1000}秒");
    }
}