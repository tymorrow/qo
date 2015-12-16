namespace Qo.Parsing
{
    using System.Diagnostics;

    internal class Console : IConsole
    {
        public void WriteToConsole(string text)
        {
            Debug.WriteLine(text);
        }
    }
}
