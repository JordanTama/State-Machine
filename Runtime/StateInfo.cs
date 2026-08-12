using System.Linq;

namespace JordanTama.StateMachine
{
    public readonly struct StateInfo
    {
        public string Name { get; }
        public string Parent { get; }
        public string[] Children { get; }

        internal StateInfo(State state)
        {
            Name = state.Id;
            Parent = state.Parent;
            Children = state.Children.ToArray();
        }
    }
}
