using System.Collections.Generic;
using System.Linq;

namespace StateMachine
{
    internal class State
    {
        private readonly List<string> _children;
        
        public string Id { get; }
        public string Parent { get; }
        
        public StateChange OnEnter { get; }
        public StateChange OnExit { get; }
        public StateChangeAsync OnEnterAsync { get; }
        public StateChangeAsync OnExitAsync { get; }

        public IReadOnlyList<string> Children => _children;

        public State(StateConstructor constructor)
        {
            string GetId(StateConstructor stateConstructor) => stateConstructor.Id;
            _children = new List<string>(constructor.Children.Select(GetId));

            Id = constructor.Id;
            Parent = constructor.Parent?.Id ?? "";
            
            OnEnter = constructor.OnEnter;
            OnExit = constructor.OnExit;
            
            OnEnterAsync = constructor.OnEnterAsync;
            OnExitAsync = constructor.OnExitAsync;
        }

        public static implicit operator string(State state) => state.Id;
    }
}