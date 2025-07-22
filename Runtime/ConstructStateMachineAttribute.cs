using System;

namespace JordanTama.StateMachine
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ConstructStateMachineAttribute : Attribute
    {
        public string StateId { get; }
        public int Priority { get; }
        public bool IgnoreInTests { get; }
        
        public ConstructStateMachineAttribute(string stateId = Constants.ROOT_STATE_NAME, int priority = 0, bool ignoreInTests = false)
        {
            StateId = stateId;
            Priority = priority;
            IgnoreInTests = ignoreInTests;
        }
    }
}