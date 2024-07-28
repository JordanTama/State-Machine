using Cysharp.Threading.Tasks;

namespace StateMachine
{
    public delegate void StateChange(string from, string to);
    
    public delegate UniTask StateChangeAsync(string from, string to);
}
