using System.Collections.Generic;

namespace StateMachine
{
    public class StateConstructor
    {
        private readonly List<StateConstructor> _children;
        
        public string Id { get; }
        
        public StateConstructor Parent { get; private set; }
        
        public StateEnter OnEnter { get; private set; }
        public StateExit OnExit { get; private set; }
        public StateEnterAsync OnEnterAsync { get; private set; }
        public StateExitAsync OnExitAsync { get; private set; }

        internal IReadOnlyList<StateConstructor> Children => _children.AsReadOnly();

        public StateConstructor(string id, StateEnter onEnter = null, StateExit onExit = null, StateEnterAsync onEnterAsync = null, StateExitAsync onExitAsync = null)
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
