using System;

namespace StateMachine
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ConstructStateMachineAttribute : Attribute
    {
        public string StateId { get; }
        public int Priority { get; }
        
        public ConstructStateMachineAttribute(string stateId = Constants.ROOT_STATE_NAME, int priority = 0)
        {
            StateId = stateId;
            Priority = priority;
        }
    }
}