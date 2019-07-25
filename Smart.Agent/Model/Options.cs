using CommandLine;

namespace Smart.Agent.Model
{
    public class Options
    {
        //[Option('q', "Request", HelpText = "Print Request Command")]
        [Option("ifb",  HelpText = "Enter IFB Board Id")]
        public string BoardId { get; set; }
        [Option("mdl", HelpText = "Enter Model 1 or 2", Default = 1)]
        public int Model { get; set; }
        [Option("req", Default = false, HelpText = "Print Request Command")]
        public bool PrintRequest { get; set; }
        //[Option('r', "Response", HelpText = "Print Response Payload")]
        [Option("res", Default = false, HelpText = "Print Response Payload")]
        public bool PrintResponse { get; set; }
        [Option("conf", Default = false, HelpText = "Print DataPort Config Payload")]
        public bool PrintDataPortConfig { get; set; }

        [Option("cmd", HelpText = "Enter Single command")]
        public string Command { get; set; }
        [Option("memsCycle", Default = 8, HelpText = "Number of cycles for Mems Test")]
        public int MemsTestCycle { get; set; }
        [Option("memsMin", Default = 16, HelpText = "MEMS test default Min Value")]
        public double MemsTestMin { get; set; }
        [Option("memsMax", Default = 18, HelpText = "MEMS test default Max Value")]
        public double MemsTestMax { get; set; }
    }
}
