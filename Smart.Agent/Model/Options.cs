using CommandLine;

namespace Smart.Agent.Model
{
    public class Options
    {
        //[Option('q', "Request", HelpText = "Print Request Command")]
        [Option("req", Default = false, HelpText = "Print Request Command")]
        public bool PrintRequest { get; set; }
        //[Option('r', "Response", HelpText = "Print Response Payload")]
        [Option("res", Default = false, HelpText = "Print Response Payload")]
        public bool PrintResponse { get; set; }
        [Option("conf", Default = false, HelpText = "Print DataPort Config Payload")]
        public bool PrintDataPortConfig { get; set; }
    }
}
