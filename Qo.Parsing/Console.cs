namespace Qo.Parsing
{
    using System.Diagnostics;
    using System.Threading;
    internal class Console : IConsole
    {
        public void Write(string text)
        {
            Debug.Write(text);
        }

        public void WriteLine(string text)
        {
            Debug.WriteLine(text);
        }
    }
}
