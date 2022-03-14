using System;
using DotStep.Core.States;

namespace DotStep.Core
{
    public class ErrorHandler
    {
        public TimeSpan Timeout { get; set; }

        public IState FallbackState { get; set; }
    }
}