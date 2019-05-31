using Smart.Agent.Business;
using System.Collections.Generic;

namespace Smart.Agent.Model
{
    public class Queue
    {
        private readonly int _defaultNextCommandWait = 5;
        private readonly int _defaultRetryWait = 0;
        public List<Queue> CommandQueue;
        private readonly Command _command;
        public Queue()
        {
        }
        public Queue(byte boardId)
        {
            CommandQueue = new List<Queue>();
            _command = new Command(boardId);
        }

        public int SequenceId { get; set; }
        public string CommandName { get; set; }
        public byte[] CommBytes { get; set; }
        public int WaitForNext { get; set; }
        public int MaxRetry { get; set; }
        public int RetryWait { get; set; }
        public bool MoveNext { get; set; }
        public int ExpectedPacketSize { get; set; }
        public int CommandType { get; set; }

        public void LoadLcmQueue()
        {
            CommandQueue.Add(new Queue
            { SequenceId = 1, CommandName = "IF", CommandType = Constant.CommandType.If, ExpectedPacketSize = 4, CommBytes = _command.If(), MaxRetry = 1, RetryWait = _defaultRetryWait, WaitForNext = _defaultNextCommandWait, MoveNext = true });
            CommandQueue.Add(new Queue
                { SequenceId = 2, CommandName = "POWER ON", CommandType = Constant.CommandType.PowerOn, ExpectedPacketSize = 5, CommBytes = _command.PowerOn(), MaxRetry = 3, RetryWait= _defaultRetryWait, WaitForNext = 10, MoveNext = true });
            CommandQueue.Add(new Queue
                { SequenceId = 3, CommandName = "CONNECT", CommandType = Constant.CommandType.Connect, ExpectedPacketSize = 15,  CommBytes = _command.Connect(), MaxRetry = 10, RetryWait = _defaultRetryWait, WaitForNext = _defaultNextCommandWait, MoveNext = true });
            CommandQueue.Add(new Queue
                { SequenceId = -5, CommandName = "READ DATA PORT CONFIG", CommandType = Constant.CommandType.ReadDataPort, ExpectedPacketSize = 197, CommBytes = _command.ReadDataPortConfig(), MaxRetry = 5, RetryWait = _defaultRetryWait, WaitForNext = _defaultNextCommandWait, MoveNext = false });
            CommandQueue.Add(new Queue
                { SequenceId = -4, CommandName = "WRITE DATA PORT CONFIG", CommandType = Constant.CommandType.WriteDataPort, ExpectedPacketSize = 14, CommBytes = _command.WriteDataPortConfig(), MaxRetry = 5, RetryWait = _defaultRetryWait, WaitForNext = _defaultNextCommandWait, MoveNext = false });
            CommandQueue.Add(new Queue
                { SequenceId = -3, CommandName = "COLLECT", CommandType = Constant.CommandType.Collect, ExpectedPacketSize = 277, CommBytes = _command.Collect(), MaxRetry = 15, RetryWait = _defaultRetryWait, WaitForNext = _defaultNextCommandWait, MoveNext = false });
            CommandQueue.Add(new Queue
                { SequenceId = -2, CommandName = "POWER OFF", CommandType = Constant.CommandType.PowerOff, CommBytes = _command.PowerOff(), MaxRetry = 3, RetryWait = _defaultRetryWait, WaitForNext = _defaultNextCommandWait, MoveNext = false });
           
        }
    }
}
