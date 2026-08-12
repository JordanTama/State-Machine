using Cysharp.Threading.Tasks;

namespace JordanTama.StateMachine
{
    public delegate UniTask StateEnterAsync(string from);

    public delegate UniTask StateExitAsync(string to);

    public delegate void StateChange(string from, string to);
}
