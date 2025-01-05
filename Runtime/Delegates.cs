using Cysharp.Threading.Tasks;

namespace JordanTama.StateMachine
{
    public delegate void StateEnter(string from);

    public delegate void StateExit(string to);
    
    public delegate void StateChange(string from, string to);

    public delegate UniTask StateEnterAsync(string from);

    public delegate UniTask StateExitAsync(string to);

    public delegate UniTask StateChangeAsync(string from, string to);
}
