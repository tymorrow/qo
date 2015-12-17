namespace Qo.Parsing
{
    using Qo.Parsing.RelationalModel;

    public class QoPackage
    {
        internal Node Tree { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public string RelationalAlgebra { get; set; }
        public CleanNode InitialTree { get; set; }
        public CleanNode Optimization1 { get; set; }
        public CleanNode Optimization2 { get; set; }
        public CleanNode Optimization3 { get; set; }
        public CleanNode Optimization4 { get; set; }
        public CleanNode Optimization5 { get; set; }
    }
}
