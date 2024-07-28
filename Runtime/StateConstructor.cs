using System.Collections.Generic;

namespace StateMachine
{
    public class StateConstructor
    {
        private readonly List<StateConstructor> _children;
        
        public string Id { get; }
        
        public StateConstructor Parent { get; private set; }
        
        public StateChange OnEnter { get; private set; }
        public StateChange OnExit { get; private set; }
        public StateChangeAsync OnEnterAsync { get; private set; }
        public StateChangeAsync OnExitAsync { get; private set; }

        internal IReadOnlyList<StateConstructor> Children => _children.AsReadOnly();

        public StateConstructor(string id, StateChange onEnter = null, StateChange onExit = null, StateChangeAsync onEnterAsync = null, StateChangeAsync onExitAsync = null)
        {
            Id = id;
            _children = new List<StateConstructor>();

            OnEnter = onEnter;
            OnExit = onExit;

            OnEnterAsync = onEnterAsync;
            OnExitAsync = onExitAsync;
        }

        public void AddState(StateConstructor other)
        {
            other.Parent = this;
            _children.Add(other);
        }
        
        public static implicit operator string(StateConstructor constructor) => constructor.Id;
    }
}
