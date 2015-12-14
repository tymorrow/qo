namespace Qo.Parsing
{
    using System.Diagnostics;

    public class Console : IConsole
    {
        public void WriteToConsole(string text)
        {
            Debug.WriteLine(text);
        }
    }
}
