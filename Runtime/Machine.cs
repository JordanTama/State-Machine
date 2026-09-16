using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Services;
using UnityEngine;

namespace JordanTama.StateMachine
{
    public class Machine : IService
    {
        private State _currentState;
        private readonly Dictionary<string, State> _states = new();

        private string _queued;
        private bool _isTransitioning;

        private bool _isInitialized;
        private Initialized _initialized;
        public event Initialized Initialized
        {
            add
            {
                if (_isInitialized)
                {
                    value?.Invoke();
                    return;
                }

                _initialized += value;
            }
            remove => _initialized -= value;
        }
        
        public string CurrentStateId => _currentState?.Id ?? "";
        
        private bool TestMode { get; }

        public Machine(bool testMode = false)
        {
            TestMode = testMode;
        }
        
        // ================================
        // IService
        // ================================

        public async UniTask OnRegistered()
        {
            // Create the initial state
            var baseStateConstructor = new StateConstructor(Constants.ROOT_STATE_NAME);

            // Get assemblies
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Get all types
            var types = assemblies.SelectMany(GetTypes);

            // Get all methods with the correct binding flags
            const BindingFlags methodFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var methods = types.SelectMany(type => type.GetMethods(methodFlags));

            // Get the methods with the construction attribute
            var attributedMethods = methods.Where(HasAttribute).ToList();

            // Construct the state machine
            while (attributedMethods.Count > 0)
            {
                // Get all the methods with their dependency ready, and remove from the attributedMethods
                var hasDependencies = new List<MethodInfo>();
                for (int i = attributedMethods.Count - 1; i >= 0; i--)
                {
                    var method = attributedMethods[i];
                    
                    string dependencyId = GetDependency(method);
                    if (!TryGetConstructor(dependencyId, out _))
                        continue;
                    
                    hasDependencies.Add(method);
                    attributedMethods.RemoveAt(i);
                }

                // No methods have their dependency ready - break out of the loop
                if (hasDependencies.Count <= 0)
                    break;
                
                // Order the methods by their priority
                var ordered = hasDependencies.OrderBy(GetPriority).ToArray();

                bool hasInvoked = false;
                foreach (var method in ordered)
                {
                    string dependencyName = GetDependency(method);
                    if (!TryGetConstructor(dependencyName, out var dependencyConstructor))
                        continue;

                    method.Invoke(null, new object[] {dependencyConstructor});
                    hasInvoked = true;
                }

                if (!hasInvoked)
                    break;
            }

            if (attributedMethods.Count > 0)
                Error($"Missing dependencies: {string.Join(", ", attributedMethods.Select(GetDependency))}");
            
            await Initialize(baseStateConstructor);
            return;

            IEnumerable<Type> GetTypes(Assembly assembly)
            {
                return assembly.GetTypes();
            }

            bool HasAttribute(MemberInfo method)
            {
                var attribute = method.GetCustomAttribute<ConstructStateMachineAttribute>();
                return attribute != null && !(TestMode && attribute.IgnoreInTests);
            }
            
            string GetDependency(MemberInfo method)
            {
                return method.GetCustomAttribute<ConstructStateMachineAttribute>().StateId;
            }

            bool TryGetConstructor(string id, out StateConstructor constructor)
            {
                Queue<StateConstructor> queue = new();
                queue.Enqueue(baseStateConstructor);

                while (queue.Count > 0)
                {
                    var parent = queue.Dequeue();
                    foreach (var child in parent.Children)
                        queue.Enqueue(child);

                    if (parent.Id != id)
                        continue;
                    
                    constructor = parent;
                    return true;
                }

                constructor = null;
                return false;
            }

            int GetPriority(MemberInfo method)
            {
                return method.GetCustomAttribute<ConstructStateMachineAttribute>().Priority;
            }
        }

        public async UniTask OnUnregistered()
        {
            await ChangeState(Constants.ROOT_STATE_NAME);
        }

        // ================================
        // Public Methods
        // ================================

        public int GetChildCount(string stateId)
        {
            if (TryGetState(stateId, out var state)) 
                return state.Children.Count;
            
            Error($"No state with Id {stateId} registered.");
            return 0;
        }

        public StateInfo GetStateInfo(string stateName)
        {
            if (TryGetState(stateName, out var state)) 
                return new StateInfo(state);
            
            Error($"No state with Id {stateName} registered.");
            return default;
        }

        public IEnumerable<string> GetAllStates()
        {
            return _states.Keys;
        }

        public async UniTask Initialize(StateConstructor rootState)
        {
            if (_states.Count > 0)
            {
                Error($"Trying to add state '{rootState.Id}' but it is already initialized.");
                return;
            }

            RegisterConstructor(rootState);
            await ChangeState(rootState.Id);

            _isInitialized = true;
            _initialized?.Invoke();
        }

        public async UniTask<TransitionResponse> ChangeState(string to)
        {
            if (_isTransitioning)
            {
                _queued = to;
                return TransitionResponse.Pending;
            }

            if (TryGetState(to, out var toState))
                return await ChangeStateInternal(toState);
            
            Error($"Could not find state '{to}'.");
            return TransitionResponse.Rejected;
        }

        // ================================
        // Private Methods
        // ================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            var machine = new Machine();
            Locator.Register(machine).Forget();
        }

        private static void Error(string error) => Debug.LogError(error);

        private bool TryGetState(string id, out State state)
        {
            return _states.TryGetValue(id, out state);
        }
        
        private async UniTask<TransitionResponse> ChangeStateInternal(State to)
        {
            _isTransitioning = true;
            
            try
            {
                // If we're initializing
                if (_currentState == null)
                {
                    await TransitionToChild(to);
                    await TryTransitionToQueued();
                    return TransitionResponse.Completed;
                }
                
                // We're 'reloading' a state (re-entering the state we're already in)
                if (to == _currentState)
                {
                    await TransitionToParent(to);
                    await TransitionToChild(to);
                    await TryTransitionToQueued();
                    return TransitionResponse.Completed;
                }
                
                var fromState = _currentState;

                // Get the absolute paths for the current and target states
                var fromPath = GetPath(fromState).ToList();
                var toPath = GetPath(to).ToList();

                // Get the indices of the nearest shared parent
                int sharedParentIndexInFrom = fromPath.FindIndex(toPath.Contains);
                if (sharedParentIndexInFrom < 0)
                {
                    Error($"Failed transition. Could not find common parent of {fromState.Id} and {to.Id}.");
                    return TransitionResponse.Rejected;
                }

                int sharedParentIndexInTo = toPath.FindIndex(state => state.Equals(fromPath[sharedParentIndexInFrom]));

                // Remove all states above and including the shared parent
                fromPath.RemoveRange(sharedParentIndexInFrom + 1, fromPath.Count - sharedParentIndexInFrom - 1);
                toPath.RemoveRange(sharedParentIndexInTo + 1, toPath.Count - sharedParentIndexInTo - 1);

                // Reverse the 'to' path
                toPath.Reverse();

                // Traverse through parents until we're at a child of the shared parent
                for (int i = 0; i < fromPath.Count - 1; i++)
                    await TransitionToParent(fromPath[i + 1]);

                // Traverse through children until we arrive at the target state
                for (int i = 0; i < toPath.Count - 1; i++)
                    await TransitionToChild(toPath[i + 1]);

                // We've arrived
                await TryTransitionToQueued();
                return TransitionResponse.Completed;
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private async UniTask TryTransitionToQueued()
        {
            if (string.IsNullOrEmpty(_queued))
                return;

            string id = _queued;
            _queued = null;

            if (TryGetState(id, out var state))
            {
                await ChangeStateInternal(state);
                return;
            }

            Error($"Invalid queued state '{id}'.");
        }

        private IEnumerable<State> GetPath(State state)
        {
            if (state == null)
                yield break;

            while (state != null)
            {
                yield return state;
                state = GetParent(state);
            }
        }

        private State GetParent(State state)
        {
            string parentId = state.Parent;
            if (string.IsNullOrEmpty(state.Parent) || !TryGetState(parentId, out var parent))
                return null;

            return parent;
        }

        private async UniTask TransitionToParent(State state)
        {
            if (_currentState.OnExit != null)
                await _currentState.OnExit.Invoke(state.Id);
            
            _currentState = state;
        }

        private async UniTask TransitionToChild(State state)
        {
            var fromState = _currentState;
            _currentState = state;

            if (state.OnEnter != null)
            {
                bool isRootEntry = fromState == null;
                await state.OnEnter.Invoke(isRootEntry ? string.Empty : fromState.Id);
            }
        }
        
        private void RegisterConstructor(StateConstructor constructor)
        {
            foreach (var child in constructor.Children)
                RegisterConstructor(child);
                
            string id = constructor.Id;
            if (_states.ContainsKey(id))
            {
                Error($"Tried to register state with id '{id}', but it is already registered.");
                return;
            }
        
            _states[id] = new State(constructor);
        }
    }
}
