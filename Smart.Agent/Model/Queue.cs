﻿using Smart.Agent.Business;
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

        public void LoadLcmQueue()
        {
            CommandQueue.Add(new Queue
                {SequenceId = 1, CommandName = "IF", CommBytes = _command.If(), MaxRetry = 1, RetryWait= _defaultRetryWait, WaitForNext = _defaultNextCommandWait });
            CommandQueue.Add(new Queue
                { SequenceId = 2, CommandName = "POWER ON", CommBytes = _command.PowerOn(), MaxRetry = 3, RetryWait= _defaultRetryWait, WaitForNext = 20 });
            CommandQueue.Add(new Queue
                { SequenceId = 3, CommandName = "CONNECT", CommBytes = _command.Connect(), MaxRetry = 10, RetryWait = _defaultRetryWait, WaitForNext = _defaultNextCommandWait });
            CommandQueue.Add(new Queue
                { SequenceId = 4, CommandName = "READ DATA PORT CONFIG", CommBytes = _command.ReadDataPortConfig(), MaxRetry = 5, RetryWait = _defaultRetryWait, WaitForNext = _defaultNextCommandWait });
            CommandQueue.Add(new Queue
                { SequenceId = 5, CommandName = "COLLECT", CommBytes = _command.Collect(), MaxRetry = 15, RetryWait = _defaultRetryWait, WaitForNext = _defaultNextCommandWait });
            CommandQueue.Add(new Queue
                { SequenceId = 6, CommandName = "POWER OFF", CommBytes = _command.PowerOff(), MaxRetry = 3, RetryWait = _defaultRetryWait, WaitForNext = _defaultNextCommandWait });
            CommandQueue.Add(new Queue
                { SequenceId = -1, CommandName = "WRITE DATA PORT CONFIG", CommBytes = _command.WriteDataPortConfig(), MaxRetry = 1, RetryWait = _defaultRetryWait, WaitForNext = _defaultNextCommandWait });
        }
    }
}
